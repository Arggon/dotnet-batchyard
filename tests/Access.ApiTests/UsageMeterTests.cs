using System.Net;
using System.Net.Http.Json;
using Access.Api;
using Access.Data;
using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Access.ApiTests;

/// <summary>ingest.completed -> RabbitMQ -> access.usage consumer -> GET /v1/tenants/me/usage (ADR 0018).</summary>
[Collection(AccessApiTests.Name)]
public class UsageMeterTests(AccessApiFixture api)
{
    [Fact]
    public async Task Ingest_completed_events_on_rabbitmq_show_up_in_the_tenants_usage()
    {
        var (tenantId, client) = await api.NewClientAsync("Pro", admin: false);
        using var http = await api.AuthorizedClientAsync(client);
        var today = DateTime.UtcNow;

        var publisher = api.Factory.Services.GetRequiredService<IMessagePublisher>();
        await publisher.PublishAsync(Event(tenantId, created: 400, updated: 90, rejected: 10, today), CancellationToken.None);
        await publisher.PublishAsync(Event(tenantId, created: 0, updated: 50, rejected: 0, today), CancellationToken.None);

        var usage = await AccessApiFixture.EventuallyAsync(
            () => http.GetFromJsonAsync<UsageView>("/v1/tenants/me/usage"), u => u!.Days.Count == 1 && u.Days[0].Batches == 2);

        var day = usage!.Days.ShouldHaveSingleItem();
        day.Date.ShouldBe(DateOnly.FromDateTime(today));
        (day.Batches, day.ItemsCreated, day.ItemsUpdated, day.ItemsRejected).ShouldBe((2, 400, 140, 10));
        usage.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task A_redelivered_event_is_counted_once()
    {
        var (tenantId, _) = await api.NewClientAsync("Free", admin: false);
        var message = Event(tenantId, created: 5, updated: 0, rejected: 1, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IngestUsageHandler>();
            await handler.HandleAsync(message, CancellationToken.None);
        }

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IngestUsageHandler>().HandleAsync(message, CancellationToken.None);
        }

        await using var read = api.Factory.Services.CreateAsyncScope();
        var days = await read.ServiceProvider.GetRequiredService<UsageQueries>()
            .LastDaysAsync(tenantId, new DateOnly(2026, 10, 1), 1, CancellationToken.None);
        var day = days.ShouldHaveSingleItem();
        (day.Batches, day.ItemsCreated, day.ItemsRejected).ShouldBe((1, 5, 1));
    }

    [Fact]
    public async Task Usage_is_split_by_utc_day_and_scoped_to_the_tenant()
    {
        var (tenantId, _) = await api.NewClientAsync("Free", admin: false);
        var (otherTenant, _) = await api.NewClientAsync("Free", admin: false);
        await HandleAsync(Event(tenantId, 1, 0, 0, new DateTime(2026, 10, 9, 23, 59, 0, DateTimeKind.Utc)));
        await HandleAsync(Event(tenantId, 2, 0, 0, new DateTime(2026, 10, 10, 0, 1, 0, DateTimeKind.Utc)));
        await HandleAsync(Event(otherTenant, 99, 0, 0, new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc)));

        await using var read = api.Factory.Services.CreateAsyncScope();
        var days = await read.ServiceProvider.GetRequiredService<UsageQueries>()
            .LastDaysAsync(tenantId, new DateOnly(2026, 10, 10), 30, CancellationToken.None);

        days.Select(d => (d.Date, d.ItemsCreated)).ShouldBe([(new DateOnly(2026, 10, 10), 2), (new DateOnly(2026, 10, 9), 1)]);
    }

    [Fact]
    public async Task Usage_requires_a_token()
    {
        using var http = api.Factory.CreateClient();

        (await http.GetAsync(new Uri("/v1/tenants/me/usage", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task HandleAsync(IntegrationMessage message)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestUsageHandler>().HandleAsync(message, CancellationToken.None);
    }

    private static IntegrationMessage Event(Guid tenantId, int created, int updated, int rejected, DateTime at) =>
        new(Guid.NewGuid(), IngestCompleted.Type,
            System.Text.Json.JsonSerializer.Serialize(new IngestCompleted(tenantId, created, updated, rejected, at), System.Text.Json.JsonSerializerOptions.Web));
}
