using BuildingBlocks.Auth;

namespace Ingest.IntegrationTests;

/// <summary>Mints tokens exactly like the access service does, with a test-only key.</summary>
internal static class TestTokens
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef";

    public static string For(Guid tenantId, IEnumerable<string> permissions, int? maxBatch, IEnumerable<string>? roles = null) =>
        new JwtTokenIssuer(new JwtOptions { SigningKey = SigningKey }, TimeProvider.System)
            .Issue("test-client", tenantId, "Test", roles ?? [], permissions, maxBatch)
            .AccessToken;
}
