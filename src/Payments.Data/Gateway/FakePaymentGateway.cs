using System.Collections.Concurrent;
using Payments.Application;
using Payments.Application.Domain;

namespace Payments.Data.Gateway;

/// <summary>
/// Stands in for a real payment provider. Like real providers, it is idempotent by key: the same key
/// returns the same charge. Tests can make it fail or slow down to exercise retries and timeouts.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, string> _charges = new();
    private int _failuresLeft;

    /// <summary>Number of distinct charges made (what a real card would see).</summary>
    public int ChargeCount => _charges.Count(c => c.Value.StartsWith("ch_", StringComparison.Ordinal));

    public int Calls { get; private set; }

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Make the next <paramref name="count"/> calls fail with a transient error.</summary>
    public void FailNext(int count) => Interlocked.Exchange(ref _failuresLeft, count);

    public Task<GatewayResult> ChargeAsync(string idempotencyKey, Money amount, CancellationToken ct) =>
        Call(idempotencyKey, "ch_", ct);

    public Task<GatewayResult> RefundAsync(string idempotencyKey, string chargeReference, Money amount, CancellationToken ct) =>
        Call(idempotencyKey, "re_", ct);

    private async Task<GatewayResult> Call(string key, string prefix, CancellationToken ct)
    {
        Calls++;
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, ct);
        }

        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
        {
            throw new HttpRequestException("The payment gateway is temporarily unavailable.");
        }

        return new GatewayResult(_charges.GetOrAdd(key, _ => prefix + Guid.NewGuid().ToString("N")[..16]));
    }
}
