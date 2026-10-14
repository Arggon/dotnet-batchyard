using Microsoft.Extensions.DependencyInjection;
using Payments.Application;
using Payments.Application.Mediator;
using Payments.Data.Gateway;

namespace Payments.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsData(this IServiceCollection services, string connectionString, GatewayResilienceOptions? gateway = null)
    {
        services.AddScoped(_ => new PaymentsDb(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentsDb>());
        services.AddScoped<IInvoiceRepository, SqlInvoiceRepository>();
        services.AddScoped<IPaymentRepository, SqlPaymentRepository>();
        services.AddScoped<IEventOutbox, SqlEventOutbox>();
        services.AddSingleton<FakePaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp =>
            new ResilientPaymentGateway(sp.GetRequiredService<FakePaymentGateway>(), gateway ?? new GatewayResilienceOptions()));
        return services;
    }
}
