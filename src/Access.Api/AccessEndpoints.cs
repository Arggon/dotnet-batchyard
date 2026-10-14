using System.Security.Cryptography;
using System.Text;
using Access.Data;
using BuildingBlocks.Auth;
using Microsoft.Extensions.Options;

namespace Access.Api;

public sealed record TokenRequest(string? ClientId, string? ClientSecret);
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
public sealed record PlanChangeRequest(string? Plan);
public sealed record MarketplaceEvent(Guid TenantId, string? Event);
public sealed record TenantView(Guid TenantId, string Plan, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, int? MaxBatchSize);

public static class AccessEndpoints
{
    public const string TokenRateLimitPolicy = "token";

    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/token", IssueToken)
            .RequireRateLimiting(TokenRateLimitPolicy)
            .WithTags("Auth")
            .WithSummary("Exchange API client credentials for a 15-minute bearer token (OAuth2 client-credentials style).");

        var tenant = app.MapGroup("/v1/tenants/me").WithTags("Tenant");
        tenant.MapGet("", GetMe).RequirePermission("items:read").WithSummary("The caller's tenant, plan and effective permissions.");
        tenant.MapPut("plan", ChangePlan).RequirePermission("plan:manage").WithSummary("Move the tenant to another plan.");

        app.MapGet("/v1/exports/items", () => Results.Ok(new { message = "Exports are a placeholder in v1; this endpoint only proves the exports:read permission." }))
            .RequirePermission("exports:read")
            .WithTags("Exports");

        app.MapPost("/v1/webhooks/marketplace", MarketplaceWebhook)
            .WithTags("Webhooks")
            .WithSummary("Stub of a marketplace subscription webhook: subscribe -> Marketplace plan, unsubscribe -> Free.");

        return app;
    }

    private static async Task<IResult> IssueToken(TokenRequest request, AccessService access, JwtTokenIssuer issuer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "clientId and clientSecret are required");
        }

        var who = await access.AuthenticateAsync(request.ClientId, request.ClientSecret, ct);
        if (who is null)
        {
            // Same answer for unknown client and wrong secret, so the endpoint can't be used to find client ids.
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid client credentials");
        }

        var token = issuer.Issue(who.ClientId, who.TenantId, who.Plan, who.Roles, who.Permissions, who.MaxBatchSize);
        return Results.Ok(new TokenResponse(token.AccessToken, "Bearer", token.ExpiresInSeconds));
    }

    private static async Task<IResult> GetMe(HttpContext http, CachedAccess access)
    {
        var me = await access.ResolveAsync(http.User.TenantId(), http.User.ClientId(), http.RequestAborted);
        return me is null
            ? Results.NotFound()
            : Results.Ok(new TenantView(me.TenantId, me.Plan, me.Roles, me.Permissions, me.MaxBatchSize));
    }

    private static async Task<IResult> ChangePlan(PlanChangeRequest request, HttpContext http, AccessService access)
    {
        if (string.IsNullOrWhiteSpace(request.Plan) || !await access.ChangePlanAsync(http.User.TenantId(), request.Plan, http.RequestAborted))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown plan",
                detail: "Use one of: Free, Pro, Marketplace.");
        }

        return Results.Ok(new { plan = request.Plan, note = "Tokens issued from now on carry the new plan; cached permissions refresh when plan.changed is consumed." });
    }

    private static async Task<IResult> MarketplaceWebhook(
        MarketplaceEvent body, HttpContext http, AccessService access, IOptions<AccessOptions> options)
    {
        var expected = options.Value.WebhookSecret;
        var given = http.Request.Headers["X-Webhook-Secret"].ToString();
        if (string.IsNullOrEmpty(expected)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected)))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid webhook secret");
        }

        var plan = body.Event switch
        {
            "subscribe" => "Marketplace",
            "unsubscribe" => "Free",
            _ => null,
        };
        if (plan is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown event", detail: "Use subscribe or unsubscribe.");
        }

        return await access.ChangePlanAsync(body.TenantId, plan, http.RequestAborted)
            ? Results.Ok(new { tenantId = body.TenantId, plan })
            : Results.NotFound();
    }
}
