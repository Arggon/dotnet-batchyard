using Payments.Application;
using Payments.Application.Domain;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Payments.Data.Gateway;

/// <summary>
/// Retries transient gateway failures and bounds each attempt with a timeout. Safe to retry because every call
/// carries the idempotency key.
/// </summary>
public sealed class ResilientPaymentGateway(IPaymentGateway inner, GatewayResilienceOptions options) : IPaymentGateway
{
    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetries,
            Delay = options.BaseDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TimeoutRejectedException>(),
        })
        .AddTimeout(options.AttemptTimeout)
        .Build();

    public Task<GatewayResult> ChargeAsync(string idempotencyKey, Money amount, CancellationToken ct) =>
        _pipeline.ExecuteAsync(async token => await inner.ChargeAsync(idempotencyKey, amount, token), ct).AsTask();

    public Task<GatewayResult> RefundAsync(string idempotencyKey, string chargeReference, Money amount, CancellationToken ct) =>
        _pipeline.ExecuteAsync(async token => await inner.RefundAsync(idempotencyKey, chargeReference, amount, token), ct).AsTask();
}

public sealed class GatewayResilienceOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
