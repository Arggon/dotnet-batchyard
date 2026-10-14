using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application;
using Payments.Application.Invoices;
using Payments.Application.Mediator;
using Payments.Data.Gateway;

namespace Payments.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsData(this IServiceCollection services, string connectionString, GatewayResilienceOptions? gateway = null)
    {
        services.AddSingleton(new PaymentsConnectionString(connectionString));
        services.AddScoped<IPaymentsReadModel, SqlPaymentsReadModel>();
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

    /// <summary>Outbox dispatcher plus the read-model consumer. Off in tests that do not run a broker.</summary>
    public static IServiceCollection AddPaymentsMessaging(this IServiceCollection services, string connectionString, MessagingOptions options)
    {
        services.AddBatchyardMessaging(options);
        services.AddOutboxDispatcher(connectionString);
        services.AddMessageConsumer<PaymentsProjection>(PaymentsProjection.Queue);
        return services;
    }
}
