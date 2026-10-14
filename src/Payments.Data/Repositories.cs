using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Payments.Application;
using Payments.Application.Domain;

namespace Payments.Data;

public sealed class SqlInvoiceRepository(PaymentsDb db) : IInvoiceRepository
{
    public async Task AddAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var connection = await db.ConnectionAsync(ct);
        invoice.Version = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition(
            """
            INSERT INTO dbo.Invoices (Id, TenantId, Reference, Amount, Currency, Status, CreatedAt)
            OUTPUT inserted.Version
            VALUES (@Id, @TenantId, @Reference, @Amount, @Currency, @Status, @CreatedAt)
            """,
            new
            {
                invoice.Id,
                invoice.TenantId,
                invoice.Reference,
                invoice.Total.Amount,
                invoice.Total.Currency,
                Status = StatusText(invoice.Status),
                invoice.CreatedAt,
            },
            db.Transaction,
            cancellationToken: ct)) ?? [];
    }

    public async Task<Invoice?> GetAsync(Guid tenantId, Guid invoiceId, CancellationToken ct)
    {
        var connection = await db.ConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<InvoiceRow>(new CommandDefinition(
            "SELECT Id, TenantId, Reference, Amount, Currency, Status, CreatedAt, Version FROM dbo.Invoices WHERE Id = @invoiceId AND TenantId = @tenantId",
            new { tenantId, invoiceId },
            db.Transaction,
            cancellationToken: ct));
        return row is null
            ? null
            : Invoice.Restore(row.Id, row.TenantId, row.Reference, new Money(row.Amount, row.Currency),
                Enum.Parse<InvoiceStatus>(row.Status, ignoreCase: true), row.CreatedAt, row.Version);
    }

    public async Task UpdateStatusAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var connection = await db.ConnectionAsync(ct);
        var newVersion = await connection.ExecuteScalarAsync<byte[]?>(new CommandDefinition(
            """
            UPDATE dbo.Invoices SET Status = @Status
            OUTPUT inserted.Version
            WHERE Id = @Id AND TenantId = @TenantId AND Version = @Version
            """,
            new { invoice.Id, invoice.TenantId, invoice.Version, Status = StatusText(invoice.Status) },
            db.Transaction,
            cancellationToken: ct));
        invoice.Version = newVersion
            ?? throw new ConcurrencyConflictException($"Invoice {invoice.Id} was changed by another request.");
    }

    private static string StatusText(InvoiceStatus status) => status.ToString().ToLowerInvariant();

    private sealed record InvoiceRow(
        Guid Id, Guid TenantId, string Reference, decimal Amount, string Currency, string Status, DateTime CreatedAt, byte[] Version);
}

public sealed class SqlPaymentRepository(PaymentsDb db) : IPaymentRepository
{
    private const string Columns = "Id, TenantId, InvoiceId, Kind, Amount, Currency, IdempotencyKey, GatewayReference, CreatedAt";

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payment);
        var connection = await db.ConnectionAsync(ct);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"INSERT INTO dbo.Payments ({Columns}) VALUES (@Id, @TenantId, @InvoiceId, @Kind, @Amount, @Currency, @IdempotencyKey, @GatewayReference, @CreatedAt)",
                new
                {
                    payment.Id,
                    payment.TenantId,
                    payment.InvoiceId,
                    Kind = payment.Kind == PaymentKind.Capture ? "capture" : "refund",
                    payment.Amount.Amount,
                    payment.Amount.Currency,
                    payment.IdempotencyKey,
                    payment.GatewayReference,
                    payment.CreatedAt,
                },
                db.Transaction,
                cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateIdempotencyKeyException(payment.IdempotencyKey);
        }
    }

    public Task<Payment?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken ct) =>
        QueryOne($"SELECT {Columns} FROM dbo.Payments WHERE Id = @paymentId AND TenantId = @tenantId", new { tenantId, paymentId }, ct);

    public Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
        QueryOne($"SELECT {Columns} FROM dbo.Payments WHERE TenantId = @tenantId AND IdempotencyKey = @key", new { tenantId, key }, ct);

    private async Task<Payment?> QueryOne(string sql, object args, CancellationToken ct)
    {
        var connection = await db.ConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<PaymentRow>(new CommandDefinition(sql, args, db.Transaction, cancellationToken: ct));
        return row is null
            ? null
            : new Payment(row.Id, row.TenantId, row.InvoiceId, row.Kind == "capture" ? PaymentKind.Capture : PaymentKind.Refund,
                new Money(row.Amount, row.Currency), row.IdempotencyKey, row.GatewayReference, row.CreatedAt);
    }

    private sealed record PaymentRow(
        Guid Id, Guid TenantId, Guid InvoiceId, string Kind, decimal Amount, string Currency, string IdempotencyKey, string GatewayReference, DateTime CreatedAt);
}

/// <summary>Writes events to dbo.Outbox inside the command's transaction (ADR 0011).</summary>
public sealed class SqlEventOutbox(PaymentsDb db, TimeProvider clock) : IEventOutbox
{
    public async Task AddAsync(string type, object payload, CancellationToken ct)
    {
        var connection = await db.ConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.Outbox (Id, Type, Payload, OccurredAt, TraceParent) VALUES (@Id, @type, @Payload, @OccurredAt, @TraceParent)",
            new
            {
                Id = Guid.NewGuid(),
                type,
                Payload = JsonSerializer.Serialize(payload, payload.GetType(), JsonSerializerOptions.Web),
                OccurredAt = clock.GetUtcNow().UtcDateTime,
                TraceParent = BuildingBlocks.Messaging.MessagingTelemetry.CurrentTraceParent,
            },
            db.Transaction,
            cancellationToken: ct));
    }
}
