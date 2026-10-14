using Microsoft.AspNetCore.Http;

namespace BuildingBlocks;

/// <summary>
/// Reads the tenant from the <c>X-Tenant-Id</c> header. Temporary until JWT auth lands in M2 (ADR 0004).
/// </summary>
public static class TenantHeader
{
    public const string Name = "X-Tenant-Id";

    public static bool TryGet(HttpRequest request, out Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenantId = Guid.Empty;
        return request.Headers.TryGetValue(Name, out var values)
            && values.Count == 1
            && Guid.TryParse(values[0], out tenantId)
            && tenantId != Guid.Empty;
    }
}
