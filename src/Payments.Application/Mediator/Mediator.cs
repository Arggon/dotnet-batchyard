using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Payments.Application.Mediator;

// A minimal mediator (ADR 0010): requests, handlers and pipeline behaviors resolved from DI.

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040", Justification = "Marker interface carrying the response type.")]
public interface IRequest<TResponse>;

/// <summary>A request that changes state. Only commands get the transaction behavior.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040", Justification = "Marker interface.")]
public interface ICommand<TResponse> : IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> HandlerStep<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, HandlerStep<TResponse> nextStep, CancellationToken cancellationToken);
}

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public sealed class Mediator(IServiceProvider services) : IMediator
{
    // One wrapper per request type, so the reflection happens once.
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wrapper = (RequestWrapperBase<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            type => Activator.CreateInstance(typeof(RequestWrapper<,>).MakeGenericType(type, typeof(TResponse)))!);
        return wrapper.Handle(request, services, cancellationToken);
    }

    private abstract class RequestWrapperBase<TResponse>
    {
        public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class RequestWrapper<TRequest, TResponse> : RequestWrapperBase<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            HandlerStep<TResponse> next = () => handler.Handle(typed, ct);

            // Behaviors run in registration order: the first registered is the outermost.
            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var inner = next;
                next = () => behavior.Handle(typed, inner, ct);
            }

            return next();
        }
    }
}
