using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Invoices;
using Payments.Application.Mediator;

namespace Payments.Application;

public static class DependencyInjection
{
    /// <summary>Mediator, behaviors (outermost first: logging, validation, transaction), handlers and validators.</summary>
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        services.AddScoped<IMediator, Mediator.Mediator>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddScoped<IRequestHandler<CreateInvoice, Guid>, CreateInvoiceHandler>();
        services.AddScoped<IRequestHandler<CapturePayment, PaymentReceipt>, CapturePaymentHandler>();
        services.AddScoped<IRequestHandler<RefundPayment, PaymentReceipt>, RefundPaymentHandler>();
        services.AddScoped<IRequestHandler<GetInvoice, InvoiceView?>, GetInvoiceHandler>();

        services.AddSingleton<IValidator<CreateInvoice>, CreateInvoiceValidator>();
        services.AddSingleton<IValidator<CapturePayment>, IdempotencyKeyValidator>();
        services.AddSingleton<IValidator<RefundPayment>, IdempotencyKeyValidator>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
