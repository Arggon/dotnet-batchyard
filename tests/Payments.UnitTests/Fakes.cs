using Microsoft.Extensions.DependencyInjection;
using Payments.Application;
using Payments.Application.Domain;
using Payments.Application.Mediator;

namespace Payments.UnitTests;

/// <summary>In-memory stand-ins for the data layer. Data-path behavior is covered by integration tests against SQL Server.</summary>
internal sealed class Fakes : IInvoiceRepository, IPaymentRepository, IPaymentGateway, IEventOutbox, IUnitOfWork
{
    public Dictionary<Guid, Invoice> Invoices { get; } = [];
    public List<Payment> Payments { get; } = [];
    public List<(string Type, object Payload)> Events { get; } = [];
    public List<string> Charges { get; } = [];
    public List<string> Log { get; } = [];
    public bool FailNextGatewayCall { get; set; }

    public Task AddAsync(Invoice invoice, CancellationToken ct)
    {
        Invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetAsync(Guid tenantId, Guid invoiceId, CancellationToken ct) =>
        Task.FromResult(Invoices.TryGetValue(invoiceId, out var i) && i.TenantId == tenantId
            ? Invoice.Restore(i.Id, i.TenantId, i.Reference, i.Total, i.Status, i.CreatedAt, i.Version)
            : null);

    public Task UpdateStatusAsync(Invoice invoice, CancellationToken ct)
    {
        Invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task AddAsync(Payment payment, CancellationToken ct)
    {
        Payments.Add(payment);
        return Task.CompletedTask;
    }

    Task<Payment?> IPaymentRepository.GetAsync(Guid tenantId, Guid paymentId, CancellationToken ct) =>
        Task.FromResult(Payments.SingleOrDefault(p => p.Id == paymentId && p.TenantId == tenantId));

    public Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
        Task.FromResult(Payments.SingleOrDefault(p => p.TenantId == tenantId && p.IdempotencyKey == key));

    public Task<GatewayResult> ChargeAsync(string idempotencyKey, Money amount, CancellationToken ct)
    {
        if (FailNextGatewayCall)
        {
            FailNextGatewayCall = false;
            throw new HttpRequestException("gateway down");
        }

        Charges.Add(idempotencyKey);
        return Task.FromResult(new GatewayResult($"ch_{Charges.Count}"));
    }

    public Task<GatewayResult> RefundAsync(string idempotencyKey, string chargeReference, Money amount, CancellationToken ct) =>
        Task.FromResult(new GatewayResult($"re_{chargeReference}"));

    public Task AddAsync(string type, object payload, CancellationToken ct)
    {
        Events.Add((type, payload));
        return Task.CompletedTask;
    }

    public Task BeginAsync(CancellationToken cancellationToken) => Record("begin");
    public Task CommitAsync(CancellationToken cancellationToken) => Record("commit");
    public Task RollbackAsync(CancellationToken cancellationToken) => Record("rollback");

    private Task Record(string entry)
    {
        Log.Add(entry);
        return Task.CompletedTask;
    }

    public ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection().AddLogging().AddPaymentsApplication();
        services.AddSingleton<IInvoiceRepository>(this);
        services.AddSingleton<IPaymentRepository>(this);
        services.AddSingleton<IPaymentGateway>(this);
        services.AddSingleton<IEventOutbox>(this);
        services.AddSingleton<IUnitOfWork>(this);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
