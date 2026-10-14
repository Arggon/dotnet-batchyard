using BuildingBlocks.Auth;
using Payments.Application.Domain;
using Payments.Application.Invoices;
using Payments.Application.Mediator;

namespace Payments.Api;

public sealed record CreateInvoiceRequest(decimal Amount, string? Currency, string? Reference);

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var invoices = app.MapGroup("/v1/invoices").WithTags("Invoices");
        invoices.MapPost("", CreateInvoice).RequirePermission("payments:write");
        invoices.MapGet("{id:guid}", GetInvoice).RequirePermission("payments:read")
            .WithSummary("Current state of one invoice (write side, always up to date).");
        invoices.MapPost("{id:guid}/capture", Capture).RequirePermission("payments:write")
            .WithSummary("Capture the invoice amount. Idempotency-Key header required; the same key never charges twice.");

        app.MapPost("/v1/payments/{id:guid}/refund", Refund).RequirePermission("payments:write").WithTags("Payments")
            .WithSummary("Refund a captured payment in full. Idempotency-Key header required.");

        return app;
    }

    private static async Task<IResult> CreateInvoice(CreateInvoiceRequest body, HttpContext http, IMediator mediator)
    {
        var id = await mediator.Send(new CreateInvoice(http.User.TenantId(), body.Amount, body.Currency, body.Reference), http.RequestAborted);
        var view = await mediator.Send(new GetInvoice(http.User.TenantId(), id), http.RequestAborted);
        return Results.Created($"/v1/invoices/{id}", view);
    }

    private static async Task<IResult> GetInvoice(Guid id, HttpContext http, IMediator mediator)
    {
        var view = await mediator.Send(new GetInvoice(http.User.TenantId(), id), http.RequestAborted);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static Task<IResult> Capture(Guid id, HttpContext http, IMediator mediator) =>
        SendIdempotent(http, mediator, key => new CapturePayment(http.User.TenantId(), id, key));

    private static Task<IResult> Refund(Guid id, HttpContext http, IMediator mediator) =>
        SendIdempotent(http, mediator, key => new RefundPayment(http.User.TenantId(), id, key));

    /// <summary>
    /// A concurrent request with the same key (or on the same invoice) can make the first attempt fail on the
    /// unique index or the version check. One retry then sees the committed result: a replay for the same key,
    /// or a real conflict for a different one.
    /// </summary>
    private static async Task<IResult> SendIdempotent(HttpContext http, IMediator mediator, Func<string?, ICommand<PaymentReceipt>> command)
    {
        var key = http.Request.Headers["Idempotency-Key"].ToString();
        PaymentReceipt receipt;
        try
        {
            receipt = await mediator.Send(command(key), http.RequestAborted);
        }
        catch (Exception ex) when (ex is DuplicateIdempotencyKeyException or ConcurrencyConflictException)
        {
            receipt = await mediator.Send(command(key), http.RequestAborted);
        }

        if (receipt.Replayed)
        {
            http.Response.Headers["Idempotent-Replayed"] = "true";
            return Results.Ok(receipt);
        }

        return Results.Created($"/v1/payments/{receipt.PaymentId}", receipt);
    }
}
