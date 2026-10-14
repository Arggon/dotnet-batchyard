using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging;

/// <summary>One shared connection per process, opened lazily and declared idempotently.</summary>
public sealed class RabbitMqConnection(MessagingOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public MessagingOptions Options => options;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory { Uri = new Uri(options.Uri), AutomaticRecoveryEnabled = true };
                _connection = await factory.CreateConnectionAsync(ct);
            }

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}

/// <summary>Publishes persistent messages to the topic exchange with publisher confirms.</summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(IntegrationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        // Continue the trace of the request that wrote the outbox row.
        using var activity = MessagingTelemetry.Start($"{message.Type} publish", System.Diagnostics.ActivityKind.Producer, message.TraceParent, message);
        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                var conn = await connection.GetAsync(ct);
                _channel = await conn.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await _channel.ExchangeDeclareAsync(connection.Options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
            }

            var props = new BasicProperties
            {
                MessageId = message.Id.ToString(),
                Type = message.Type,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?>
                {
                    [MessagingTelemetry.TraceParentHeader] = activity?.Id ?? message.TraceParent,
                },
            };
            // With confirmation tracking on, this throws if the broker nacks or the message is unroutable.
            await _channel.BasicPublishAsync(connection.Options.Exchange, message.Type, mandatory: false, props,
                Encoding.UTF8.GetBytes(message.Payload), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _gate.Dispose();
    }
}

/// <summary>Describes one durable queue and the routing keys bound to it.</summary>
public sealed record QueueBinding(string Name, IReadOnlyList<string> RoutingKeys);

/// <summary>
/// Consumes one queue and hands each message to a scoped <typeparamref name="THandler"/>.
/// Acks after the handler succeeds; on failure the message is rejected to the queue's dead-letter exchange.
/// </summary>
public sealed partial class RabbitMqConsumer<THandler>(
    RabbitMqConnection connection, QueueBinding queue, IServiceScopeFactory scopes, ILogger<RabbitMqConsumer<THandler>> logger)
    : BackgroundService where THandler : IMessageHandler
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Broker not up yet (compose start order): retry.
                LogConnectFailed(logger, queue.Name, ex);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var exchange = connection.Options.Exchange;
        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(cancellationToken: ct);
        await _channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await _channel.ExchangeDeclareAsync($"{exchange}.dlx", ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await _channel.QueueDeclareAsync($"{queue.Name}.dead", durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _channel.QueueBindAsync($"{queue.Name}.dead", $"{exchange}.dlx", "", cancellationToken: ct);
        await _channel.QueueDeclareAsync(queue.Name, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = $"{exchange}.dlx" }, cancellationToken: ct);
        foreach (var key in queue.RoutingKeys)
        {
            await _channel.QueueBindAsync(queue.Name, exchange, key, cancellationToken: ct);
        }

        await _channel.BasicQosAsync(0, 16, false, ct);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var traceParent = delivery.BasicProperties.Headers?.TryGetValue(MessagingTelemetry.TraceParentHeader, out var raw) == true
                ? raw switch { byte[] bytes => Encoding.UTF8.GetString(bytes), string text => text, _ => null }
                : null;
            var message = new IntegrationMessage(
                Guid.TryParse(delivery.BasicProperties.MessageId, out var id) ? id : Guid.NewGuid(),
                delivery.RoutingKey,
                Encoding.UTF8.GetString(delivery.Body.Span),
                traceParent);
            using var activity = MessagingTelemetry.Start($"{message.Type} process", System.Diagnostics.ActivityKind.Consumer, traceParent, message);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<THandler>().HandleAsync(message, ct);
                await _channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                LogHandlerFailed(logger, message.Type, message.Id, ex);
                await _channel.BasicNackAsync(delivery.DeliveryTag, false, requeue: !delivery.Redelivered, ct);
            }
        };
        await _channel.BasicConsumeAsync(queue.Name, autoAck: false, consumer, ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start consumer for {Queue}; retrying")]
    private static partial void LogConnectFailed(ILogger logger, string queue, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handler failed for {Type} {MessageId}")]
    private static partial void LogHandlerFailed(ILogger logger, string type, Guid messageId, Exception ex);
}
