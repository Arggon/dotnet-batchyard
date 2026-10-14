using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Access.Data;

/// <summary>
/// Cache-aside over <see cref="AccessService"/> permission lookups (ADR 0008).
/// Keys include a per-tenant generation; the plan.changed event bumps the generation, so every cached entry
/// for that tenant is skipped at once and expires on its own TTL.
/// If the cache is unavailable, lookups go straight to SQL.
/// </summary>
public sealed partial class CachedAccess(AccessService access, IDistributedCache cache, ILogger<CachedAccess> logger)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public async Task<EffectiveAccess?> ResolveAsync(Guid tenantId, string clientId, CancellationToken ct = default)
    {
        string? key = null;
        try
        {
            key = $"access:perm:{tenantId}:{await GenerationAsync(tenantId, ct)}:{clientId}";
            var cached = await cache.GetAsync(key, ct);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<EffectiveAccess>(cached);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheUnavailable(logger, ex);
            key = null;
        }

        var fresh = await access.ResolveAsync(clientId, ct);
        if (fresh is not null && key is not null)
        {
            await TryAsync(() => cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(fresh),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct));
        }

        return fresh;
    }

    /// <summary>
    /// Bumps the tenant's generation so all its cached entries are skipped. Called only by the
    /// plan.changed consumer (ADR 0013); if Redis is down the failure is logged and entries expire on their TTL.
    /// </summary>
    public Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        TryAsync(() => cache.SetStringAsync(GenerationKey(tenantId), Guid.NewGuid().ToString("N"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl * 2 }, ct));

    private async Task<string> GenerationAsync(Guid tenantId, CancellationToken ct) =>
        await cache.GetStringAsync(GenerationKey(tenantId), ct) ?? "0";

    private static string GenerationKey(Guid tenantId) => $"access:gen:{tenantId}";

    private async Task TryAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheUnavailable(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permission cache unavailable; using SQL directly")]
    private static partial void LogCacheUnavailable(ILogger logger, Exception exception);
}
