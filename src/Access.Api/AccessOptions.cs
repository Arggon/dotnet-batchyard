namespace Access.Api;

public sealed class AccessOptions
{
    public const string Section = "Access";

    public bool RunMigrations { get; set; } = true;

    /// <summary>Token requests allowed per client IP per minute.</summary>
    public int TokenRequestsPerMinute { get; set; } = 10;

    /// <summary>Shared secret for the marketplace webhook stub. Empty = webhook disabled.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Optional demo tenant created on startup (local runs only).</summary>
    public SeedOptions? Seed { get; set; }
}

public sealed class SeedOptions
{
    public string TenantName { get; set; } = "Demo Parts Co";
    public string Plan { get; set; } = "Pro";
    public string AdminClientId { get; set; } = "";
    public string AdminClientSecret { get; set; } = "";
}
