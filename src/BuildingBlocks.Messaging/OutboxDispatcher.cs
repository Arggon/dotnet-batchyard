using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Background loop that moves outbox rows to RabbitMQ. A row is marked published only after the broker
/// confirms it, so a crash in between means the message is sent again (at least once), never lost.
/// </summary>
public sealed partial class OutboxDispatcher(
    SqlOutboxStore store, IMessagePublisher publisher, MessagingOptions options, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int sent;
            try
            {
                sent = await store.DispatchBatchAsync(options.BatchSize, m => publisher.PublishAsync(m, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogDispatchFailed(logger, ex);
                sent = 0;
            }

            if (sent < options.BatchSize)
            {
                try
                {
                    await Task.Delay(options.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox dispatch failed; will retry")]
    private static partial void LogDispatchFailed(ILogger logger, Exception ex);
}
