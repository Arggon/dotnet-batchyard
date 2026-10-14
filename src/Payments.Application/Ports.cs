using Payments.Application.Domain;

namespace Payments.Application;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct);
    Task<Invoice?> GetAsync(Guid tenantId, Guid invoiceId, CancellationToken ct);

    /// <summary>Saves the status if <see cref="Invoice.Version"/> still matches; otherwise throws <see cref="ConcurrencyConflictException"/>.</summary>
    Task UpdateStatusAsync(Invoice invoice, CancellationToken ct);
}

public interface IPaymentRepository
{
    /// <summary>Throws <see cref="DuplicateIdempotencyKeyException"/> if the key was used by a concurrent request.</summary>
    Task AddAsync(Payment payment, CancellationToken ct);
    Task<Payment?> GetAsync(Guid tenantId, Guid paymentId, CancellationToken ct);
    Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct);
}

public sealed record GatewayResult(string Reference);

/// <summary>The payment provider. Calls carry the idempotency key, so a retried call never charges twice.</summary>
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(string idempotencyKey, Money amount, CancellationToken ct);
    Task<GatewayResult> RefundAsync(string idempotencyKey, string chargeReference, Money amount, CancellationToken ct);
}

/// <summary>Records an integration event in the same transaction as the state change (ADR 0011).</summary>
public interface IEventOutbox
{
    Task AddAsync(string type, object payload, CancellationToken ct);
}
