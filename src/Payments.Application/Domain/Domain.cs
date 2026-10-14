using System.Text.RegularExpressions;

namespace Payments.Application.Domain;

/// <summary>Money is a decimal amount with an ISO 4217 currency. Never floating point.</summary>
public readonly partial record struct Money(decimal Amount, string Currency)
{
    public static IEnumerable<string> Problems(decimal amount, string? currency)
    {
        if (amount <= 0)
        {
            yield return "amount must be greater than zero";
        }

        if (decimal.Round(amount, 4) != amount || amount > 999_999_999_999_999m)
        {
            yield return "amount must fit decimal(19,4)";
        }

        if (currency is null || !CurrencyPattern().IsMatch(currency))
        {
            yield return "currency must be a 3-letter upper-case ISO 4217 code";
        }
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}

public enum InvoiceStatus
{
    Open,
    Paid,
    Refunded,
}

public enum PaymentKind
{
    Capture,
    Refund,
}

/// <summary>An invoice. <see cref="Version"/> is the optimistic concurrency token (SQL rowversion).</summary>
public sealed class Invoice
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Reference { get; init; } = "";
    public Money Total { get; init; }
    public InvoiceStatus Status { get; private set; }
    public DateTime CreatedAt { get; init; }
    public byte[] Version { get; set; } = [];

    public static Invoice Create(Guid tenantId, Money total, string reference, DateTime now) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Total = total, Reference = reference, Status = InvoiceStatus.Open, CreatedAt = now };

    public static Invoice Restore(Guid id, Guid tenantId, string reference, Money total, InvoiceStatus status, DateTime createdAt, byte[] version) =>
        new() { Id = id, TenantId = tenantId, Reference = reference, Total = total, Status = status, CreatedAt = createdAt, Version = version };

    public void MarkPaid()
    {
        if (Status != InvoiceStatus.Open)
        {
            throw new ConflictException($"Invoice {Id} is {Status}; only open invoices can be captured.");
        }

        Status = InvoiceStatus.Paid;
    }

    public void MarkRefunded()
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new ConflictException($"Invoice {Id} is {Status}; only paid invoices can be refunded.");
        }

        Status = InvoiceStatus.Refunded;
    }
}

public sealed record Payment(
    Guid Id,
    Guid TenantId,
    Guid InvoiceId,
    PaymentKind Kind,
    Money Amount,
    string IdempotencyKey,
    string GatewayReference,
    DateTime CreatedAt);

/// <summary>The request can't be applied to the current state (maps to HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Another request changed the same row first (maps to HTTP 409).</summary>
public sealed class ConcurrencyConflictException(string message) : ConflictException(message);

/// <summary>Two requests used the same idempotency key at the same time; the caller can retry and get the replay.</summary>
public sealed class DuplicateIdempotencyKeyException(string key) : Exception($"Idempotency key '{key}' is already being used.");

public sealed class NotFoundException(string message) : Exception(message);
