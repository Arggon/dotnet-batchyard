using Payments.Application.Mediator;

namespace Payments.Application.Invoices;

public sealed record InvoiceView(Guid Id, string Reference, decimal Amount, string Currency, string Status, DateTime CreatedAt);

/// <summary>Reads the write side, so the state of one invoice is always current.</summary>
public sealed record GetInvoice(Guid TenantId, Guid InvoiceId) : IRequest<InvoiceView?>;

public sealed class GetInvoiceHandler(IInvoiceRepository invoices) : IRequestHandler<GetInvoice, InvoiceView?>
{
    public async Task<InvoiceView?> Handle(GetInvoice request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await invoices.GetAsync(request.TenantId, request.InvoiceId, cancellationToken);
        return invoice is null
            ? null
            : new InvoiceView(invoice.Id, invoice.Reference, invoice.Total.Amount, invoice.Total.Currency,
                invoice.Status.ToString().ToLowerInvariant(), invoice.CreatedAt);
    }
}
