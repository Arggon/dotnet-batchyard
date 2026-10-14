using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Auth;

public static class AuthExtensions
{
    /// <summary>JWT bearer validation shared by every service: issuer, audience, lifetime and signature.</summary>
    public static IServiceCollection AddBatchyardJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        var key = JwtTokenIssuer.SigningKey(options);
        services.AddSingleton(options);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(bearer =>
        {
            bearer.MapInboundClaims = false;
            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = options.Issuer,
                ValidAudience = options.Audience,
                IssuerSigningKey = key,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = BatchyardClaims.Subject,
                RoleClaimType = BatchyardClaims.Role,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
        services.AddAuthorization();
        services.AddSingleton<IPermissionSource, TokenPermissionSource>();
        return services;
    }

    /// <summary>401 without a valid token, 403 when the caller lacks <paramref name="permission"/>.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.User.Identity?.IsAuthenticated != true)
            {
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                    detail: "Send a bearer token from POST /v1/auth/token.");
            }

            var source = http.RequestServices.GetRequiredService<IPermissionSource>();
            if (!await source.HasPermissionAsync(http.User, permission, http.RequestAborted))
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden",
                    detail: $"This operation needs the '{permission}' permission.");
            }

            return await next(context);
        });
        return builder;
    }

    public static Guid TenantId(this ClaimsPrincipal user) =>
        Guid.TryParse(user?.FindFirst(BatchyardClaims.TenantId)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("The token has no valid tenant_id claim.");

    public static string ClientId(this ClaimsPrincipal user) =>
        user?.FindFirst(BatchyardClaims.Subject)?.Value ?? throw new InvalidOperationException("The token has no sub claim.");

    public static int? MaxBatch(this ClaimsPrincipal user) =>
        int.TryParse(user?.FindFirst(BatchyardClaims.MaxBatch)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var max)
            ? max
            : null;
}

/// <summary>Where permission checks get their answer. Default: the token's perm claims.</summary>
public interface IPermissionSource
{
    ValueTask<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken);
}

public sealed class TokenPermissionSource : IPermissionSource
{
    public ValueTask<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken) =>
        ValueTask.FromResult(user?.HasClaim(BatchyardClaims.Permission, permission) == true);
}
