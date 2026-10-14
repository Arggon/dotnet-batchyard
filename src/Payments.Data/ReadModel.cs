using System.Text.Json;
using BuildingBlocks.Messaging;
using Dapper;
using Microsoft.Data.SqlClient;
using Payments.Application.Invoices;

namespace Payments.Data;

public sealed class SqlPaymentsReadModel(PaymentsConnectionString connectionString) : IPaymentsReadModel
{
    public async Task<IReadOnlyList<PaymentHistoryItem>> ListAsync(Guid tenantId, int take, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString.Value);
        var rows = await connection.QueryAsync<PaymentHistoryItem>(new CommandDefinition(
            """
            SELECT TOP (@take) PaymentId, InvoiceId, Kind, Amount, Currency, OccurredAt
            FROM dbo.TenantPayments WHERE TenantId = @tenantId ORDER BY OccurredAt DESC
            """,
            new { tenantId, take }, cancellationToken: ct));
        return rows.AsList();
    }
}

/// <summary>
/// payments.read-model consumer: projects payment events into dbo.TenantPayments.
/// The inbox insert and the projection share one transaction, so a redelivery changes nothing.
/// </summary>
public sealed class PaymentsProjection(PaymentsConnectionString connectionString, TimeProvider clock) : IMessageHandler
{
    public static readonly QueueBinding Queue = new("payments.read-model", ["payment.*"]);

    public async Task HandleAsync(IntegrationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var evt = JsonSerializer.Deserialize<PaymentEvent>(message.Payload, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Empty payload for {message.Id}.");

        await using var connection = new SqlConnection(connectionString.Value);
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        var fresh = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.Inbox (MessageId, ProcessedAt)
            SELECT @Id, @now WHERE NOT EXISTS (SELECT 1 FROM dbo.Inbox WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = @Id)
            """,
            new { message.Id, now = clock.GetUtcNow().UtcDateTime }, tx, cancellationToken: ct));
        if (fresh == 0)
        {
            return; // already applied
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.TenantPayments (PaymentId, TenantId, InvoiceId, Kind, Amount, Currency, OccurredAt)
            SELECT @PaymentId, @TenantId, @InvoiceId, @Kind, @Amount, @Currency, @OccurredAt
            WHERE NOT EXISTS (SELECT 1 FROM dbo.TenantPayments WHERE PaymentId = @PaymentId)
            """,
            evt, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
}

/// <summary>Typed wrapper so DI can hand out the payments connection string.</summary>
public sealed record PaymentsConnectionString(string Value);
