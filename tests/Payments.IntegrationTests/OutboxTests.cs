using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Invoices;
using Payments.Data;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Payments.IntegrationTests;

/// <summary>Full path: capture → outbox → RabbitMQ → read-model consumer → GET /v1/payments.</summary>
public sealed class OutboxFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4.1-management-alpine").Build();

    public string ConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _rabbit.StartAsync());
        ConnectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "batchyard_payments" }.ConnectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Payments", ConnectionString);
            host.UseSetting("Jwt:SigningKey", PaymentsFixture.SigningKey);
            host.UseSetting("Messaging:Uri", _rabbit.GetConnectionString());
            host.UseSetting("Messaging:PollInterval", "00:00:00.100");
        });
        _ = Factory.Server;
    }

    public HttpClient ClientFor(Guid tenantId)
    {
        var http = Factory.CreateClient();
        var token = new JwtTokenIssuer(new JwtOptions { SigningKey = PaymentsFixture.SigningKey }, TimeProvider.System)
            .Issue("test-client", tenantId, "Pro", ["tenant-admin"], ["payments:read", "payments:write"], null);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return http;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }
}

public sealed class OutboxTests(OutboxFixture api) : IClassFixture<OutboxFixture>
{
    private static async Task<T> Eventually<T>(Func<Task<T>> probe, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var value = await probe();
            if (done(value) || DateTime.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(100);
        }
    }

    private static async Task<PaymentReceipt> Pay(HttpClient http, string key)
    {
        var invoice = await (await http.PostAsJsonAsync("/v1/invoices", new { amount = 42.5m, currency = "EUR", reference = "R" }))
            .Content.ReadFromJsonAsync<InvoiceView>();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/invoices/{invoice!.Id}/capture");
        request.Headers.Add("Idempotency-Key", key);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentReceipt>())!;
    }

    [Fact]
    public async Task A_capture_reaches_the_read_model_through_RabbitMQ()
    {
        using var http = api.ClientFor(Guid.NewGuid());
        var receipt = await Pay(http, "rm-1");

        var history = await Eventually(
            () => http.GetFromJsonAsync<List<PaymentHistoryItem>>("/v1/payments")!,
            items => items!.Count > 0);

        history!.ShouldHaveSingleItem().PaymentId.ShouldBe(receipt.PaymentId);
        history[0].Kind.ShouldBe("capture");
        history[0].Amount.ShouldBe(42.5m);
    }

    [Fact]
    public async Task The_outbox_row_is_marked_published()
    {
        using var http = api.ClientFor(Guid.NewGuid());
        await Pay(http, "pub-1");

        var pending = await Eventually(async () =>
        {
            await using var connection = new SqlConnection(api.ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Outbox WHERE PublishedAt IS NULL", connection);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }, n => n == 0);

        pending.ShouldBe(0);
    }

    [Fact]
    public async Task A_redelivered_event_is_applied_once()
    {
        var tenant = Guid.NewGuid();
        var evt = new PaymentEvent(Guid.NewGuid(), tenant, Guid.NewGuid(), "capture", 10m, "USD", DateTime.UtcNow);
        var message = new IntegrationMessage(Guid.NewGuid(), "payment.captured", System.Text.Json.JsonSerializer.Serialize(evt, System.Text.Json.JsonSerializerOptions.Web));

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var projection = scope.ServiceProvider.GetRequiredService<PaymentsProjection>();
        await projection.HandleAsync(message, CancellationToken.None);
        await projection.HandleAsync(message, CancellationToken.None);

        using var http = api.ClientFor(tenant);
        (await http.GetFromJsonAsync<List<PaymentHistoryItem>>("/v1/payments"))!.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task One_trace_runs_from_the_http_request_through_RabbitMQ_to_the_consumer()
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MessagingTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var traceId = ActivityTraceId.CreateRandom();

        using var http = api.ClientFor(Guid.NewGuid());
        var invoice = await (await http.PostAsJsonAsync("/v1/invoices", new { amount = 5m, currency = "USD", reference = "T" }))
            .Content.ReadFromJsonAsync<InvoiceView>();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/invoices/{invoice!.Id}/capture");
        request.Headers.Add("Idempotency-Key", "trace-1");
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        (await http.SendAsync(request)).EnsureSuccessStatusCode();

        var mine = await Eventually(
            () => Task.FromResult(spans.Where(a => a.TraceId == traceId).Select(a => a.DisplayName).ToList()),
            names => names.Contains("payment.captured process"));

        mine.ShouldContain("payment.captured publish");
        mine.ShouldContain("payment.captured process");
    }
}
