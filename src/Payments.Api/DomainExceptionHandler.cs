using Microsoft.AspNetCore.Diagnostics;
using Payments.Application.Domain;
using Payments.Application.Mediator;
using Polly.Timeout;

namespace Payments.Api;

/// <summary>Maps application exceptions to Problem Details (RFC 9457).</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException or DuplicateIdempotencyKeyException => (StatusCodes.Status409Conflict, "Conflict"),
            HttpRequestException or TimeoutRejectedException => (StatusCodes.Status502BadGateway, "Payment gateway unavailable"),
            _ => (0, ""),
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = exception.Message },
        };
        if (exception is RequestValidationException validation)
        {
            context.ProblemDetails.Extensions["errors"] = validation.Errors;
        }

        return await problemDetails.TryWriteAsync(context);
    }
}
