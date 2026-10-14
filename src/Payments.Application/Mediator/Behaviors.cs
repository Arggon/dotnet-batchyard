using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Payments.Application.Mediator;

/// <summary>Validates a request before its handler runs. Returns the list of problems; empty means valid.</summary>
public interface IValidator<in TRequest>
{
    IEnumerable<string> Validate(TRequest request);
}

public sealed class RequestValidationException(IReadOnlyList<string> errors)
    : Exception("The request is invalid: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>One database transaction around a command (implemented by the data layer).</summary>
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, HandlerStep<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await nextStep();
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogHandled(logger, RequestName, elapsedMs);
            }
        }
    }

    private static readonly string RequestName = typeof(TRequest).Name;

    [LoggerMessage(Level = LogLevel.Information, Message = "{Request} handled in {ElapsedMs:F1} ms")]
    private static partial void LogHandled(ILogger logger, string request, double elapsedMs);
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, HandlerStep<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);
        var errors = validators.SelectMany(v => v.Validate(request)).ToList();
        return errors.Count > 0 ? throw new RequestValidationException(errors) : nextStep();
    }
}

/// <summary>Commands run in one transaction: committed on success, rolled back on any exception. Queries pass through.</summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, HandlerStep<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);
        if (request is not ICommand<TResponse>)
        {
            return await nextStep();
        }

        await unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var response = await nextStep();
            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
