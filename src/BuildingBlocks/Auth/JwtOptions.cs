namespace BuildingBlocks.Auth;

/// <summary>Shared token settings. Every service validates with the same issuer, audience and key (ADR 0006).</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "batchyard-access";
    public string Audience { get; set; } = "batchyard";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Comes from configuration; there is no default on purpose.</summary>
    public string SigningKey { get; set; } = "";

    public int LifetimeMinutes { get; set; } = 15;
}

/// <summary>Claim names in batchyard tokens.</summary>
public static class BatchyardClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Plan = "plan";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string MaxBatch = "max_batch";
}
