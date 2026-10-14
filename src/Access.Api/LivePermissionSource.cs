using System.Security.Claims;
using Access.Data;
using BuildingBlocks.Auth;

namespace Access.Api;

/// <summary>Inside Access, permission checks use the current assignments, so a plan change applies at once.</summary>
internal sealed class LivePermissionSource(AccessService access) : IPermissionSource
{
    public async ValueTask<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken)
    {
        var current = await access.ResolveAsync(user.ClientId(), cancellationToken);
        return current is not null && current.TenantId == user.TenantId() && current.Has(permission);
    }
}
