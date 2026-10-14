using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Auth;

public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);

public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(
        string subject, Guid tenantId, string plan, IEnumerable<string> roles, IEnumerable<string> permissions, int? maxBatch)
    {
        ArgumentNullException.ThrowIfNull(options);
        var claims = new List<Claim>
        {
            new(BatchyardClaims.Subject, subject),
            new(BatchyardClaims.TenantId, tenantId.ToString()),
            new(BatchyardClaims.Plan, plan),
        };
        claims.AddRange(roles.Select(r => new Claim(BatchyardClaims.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(BatchyardClaims.Permission, p)));
        if (maxBatch is { } max)
        {
            claims.Add(new Claim(BatchyardClaims.MaxBatch, max.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(options.LifetimeMinutes),
            SigningCredentials = new SigningCredentials(SigningKey(options), SecurityAlgorithms.HmacSha256),
        });
        return new IssuedToken(token, options.LifetimeMinutes * 60);
    }

    internal static SymmetricSecurityKey SigningKey(JwtOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 bytes long.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
