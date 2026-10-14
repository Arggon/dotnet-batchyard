using Payments.Application.Domain;
using Payments.Application.Mediator;

namespace Payments.Application.Invoices;

public sealed record PaymentReceipt(
    Guid PaymentId, Guid InvoiceId, string Kind, decimal Amount, string Currency, string GatewayReference, bool Replayed)
{
    public static PaymentReceipt From(Payment p, bool replayed) => new(
        p.Id, p.InvoiceId, p.Kind == PaymentKind.Capture ? "capture" : "refund", p.Amount.Amount, p.Amount.Currency, p.GatewayReference, replayed);
}

public sealed record PaymentEvent(
    Guid PaymentId, Guid TenantId, Guid InvoiceId, string Kind, decimal Amount, string Currency, DateTime OccurredAt);

// ---- CreateInvoice ----

public sealed record CreateInvoice(Guid TenantId, decimal Amount, string? Currency, string? Reference) : ICommand<Guid>;

public sealed class CreateInvoiceValidator : IValidator<CreateInvoice>
{
    public IEnumerable<string> Validate(CreateInvoice request)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (var problem in Money.Problems(request.Amount, request.Currency))
        {
            yield return problem;
        }

        if (string.IsNullOrWhiteSpace(request.Reference) || request.Reference.Length > 64)
        {
            yield return "reference must be 1-64 characters";
        }
    }
}

public sealed class CreateInvoiceHandler(IInvoiceRepository invoices, TimeProvider clock) : IRequestHandler<CreateInvoice, Guid>
{
    public async Task<Guid> Handle(CreateInvoice request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = Invoice.Create(request.TenantId, new Money(request.Amount, request.Currency!), request.Reference!.Trim(), clock.GetUtcNow().UtcDateTime);
        await invoices.AddAsync(invoice, cancellationToken);
        return invoice.Id;
    }
}

// ---- CapturePayment ----

public sealed record CapturePayment(Guid TenantId, Guid InvoiceId, string? IdempotencyKey) : ICommand<PaymentReceipt>;

public sealed class IdempotencyKeyValidator : IValidator<CapturePayment>, IValidator<RefundPayment>
{
    public IEnumerable<string> Validate(CapturePayment request) => Check(request?.IdempotencyKey);

    public IEnumerable<string> Validate(RefundPayment request) => Check(request?.IdempotencyKey);

    private static IEnumerable<string> Check(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            yield return "Idempotency-Key header is required (1-128 characters)";
        }
    }
}

public sealed class CapturePaymentHandler(
    IInvoiceRepository invoices, IPaymentRepository payments, IPaymentGateway gateway, IEventOutbox outbox, TimeProvider clock)
    : IRequestHandler<CapturePayment, PaymentReceipt>
{
    public async Task<PaymentReceipt> Handle(CapturePayment request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = request.IdempotencyKey!;

        // Read the invoice BEFORE looking up the key. The invoice status and the payment row commit
        // together, so if this read already sees "paid", the key lookup below sees that payment too.
        // (Key first, invoice second lets a concurrent same-key request slip in between and get 409.)
        var invoice = await invoices.GetAsync(request.TenantId, request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {request.InvoiceId} not found.");

        // Same key again: return the original result, charge nothing.
        var existing = await payments.FindByIdempotencyKeyAsync(request.TenantId, key, cancellationToken);
        if (existing is not null)
        {
            return existing.Kind == PaymentKind.Capture && existing.InvoiceId == request.InvoiceId
                ? PaymentReceipt.From(existing, replayed: true)
                : throw new ConflictException("This Idempotency-Key was already used for a different operation.");
        }

        invoice.MarkPaid();

        // Claim the invoice before charging: the version check (and the row lock it takes) means a
        // concurrent capture fails here, before it can reach the gateway.
        await invoices.UpdateStatusAsync(invoice, cancellationToken);

        var charge = await gateway.ChargeAsync(key, invoice.Total, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var payment = new Payment(Guid.NewGuid(), request.TenantId, invoice.Id, PaymentKind.Capture, invoice.Total, key, charge.Reference, now);

        await payments.AddAsync(payment, cancellationToken);
        await outbox.AddAsync("payment.captured",
            new PaymentEvent(payment.Id, payment.TenantId, invoice.Id, "capture", invoice.Total.Amount, invoice.Total.Currency, now), cancellationToken);

        return PaymentReceipt.From(payment, replayed: false);
    }
}

// ---- RefundPayment ----

public sealed record RefundPayment(Guid TenantId, Guid PaymentId, string? IdempotencyKey) : ICommand<PaymentReceipt>;

public sealed class RefundPaymentHandler(
    IInvoiceRepository invoices, IPaymentRepository payments, IPaymentGateway gateway, IEventOutbox outbox, TimeProvider clock)
    : IRequestHandler<RefundPayment, PaymentReceipt>
{
    public async Task<PaymentReceipt> Handle(RefundPayment request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = request.IdempotencyKey!;

        var capture = await payments.GetAsync(request.TenantId, request.PaymentId, cancellationToken);
        if (capture is null || capture.Kind != PaymentKind.Capture)
        {
            throw new NotFoundException($"Captured payment {request.PaymentId} not found.");
        }

        // Invoice first, key second: same reasoning as in CapturePaymentHandler.
        var invoice = await invoices.GetAsync(request.TenantId, capture.InvoiceId, cancellationToken)
            ?? throw new NotFoundException($"Invoice {capture.InvoiceId} not found.");

        var existing = await payments.FindByIdempotencyKeyAsync(request.TenantId, key, cancellationToken);
        if (existing is not null)
        {
            return existing.Kind == PaymentKind.Refund && existing.InvoiceId == invoice.Id
                ? PaymentReceipt.From(existing, replayed: true)
                : throw new ConflictException("This Idempotency-Key was already used for a different operation.");
        }

        invoice.MarkRefunded();
        await invoices.UpdateStatusAsync(invoice, cancellationToken);

        var refund = await gateway.RefundAsync(key, capture.GatewayReference, capture.Amount, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var payment = new Payment(Guid.NewGuid(), request.TenantId, invoice.Id, PaymentKind.Refund, capture.Amount, key, refund.Reference, now);

        await payments.AddAsync(payment, cancellationToken);
        await outbox.AddAsync("payment.refunded",
            new PaymentEvent(payment.Id, payment.TenantId, invoice.Id, "refund", capture.Amount.Amount, capture.Amount.Currency, now), cancellationToken);

        return PaymentReceipt.From(payment, replayed: false);
    }
}
