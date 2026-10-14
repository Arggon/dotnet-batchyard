using Payments.Application.Mediator;

namespace Payments.Application.Invoices;

public sealed record PaymentHistoryItem(Guid PaymentId, Guid InvoiceId, string Kind, decimal Amount, string Currency, DateTime OccurredAt);

/// <summary>Reads the read model, not the write tables. Newest first, capped at <see cref="Take"/>.</summary>
public sealed record ListTenantPayments(Guid TenantId, int Take = 50) : IRequest<IReadOnlyList<PaymentHistoryItem>>;

public interface IPaymentsReadModel
{
    Task<IReadOnlyList<PaymentHistoryItem>> ListAsync(Guid tenantId, int take, CancellationToken ct);
}

public sealed class ListTenantPaymentsHandler(IPaymentsReadModel readModel) : IRequestHandler<ListTenantPayments, IReadOnlyList<PaymentHistoryItem>>
{
    public Task<IReadOnlyList<PaymentHistoryItem>> Handle(ListTenantPayments request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return readModel.ListAsync(request.TenantId, Math.Clamp(request.Take, 1, 200), cancellationToken);
    }
}
