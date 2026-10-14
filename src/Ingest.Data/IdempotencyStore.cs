using Dapper;
using Microsoft.Data.SqlClient;

namespace Ingest.Data;

public sealed record StoredResponse(byte[] RequestHash, int StatusCode, string ResponseBody);

public interface IIdempotencyStore
{
    Task<StoredResponse?> FindAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);

    /// <summary>Saves the response. Returns false if another request stored the same key first.</summary>
    Task<bool> TrySaveAsync(Guid tenantId, string key, StoredResponse response, CancellationToken cancellationToken = default);
}

public sealed class SqlIdempotencyStore(string connectionString) : IIdempotencyStore
{
    private const int PrimaryKeyViolation = 2627;

    public async Task<StoredResponse?> FindAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<StoredResponse>(new CommandDefinition(
            """
            SELECT RequestHash, StatusCode, ResponseBody
            FROM dbo.IdempotencyKeys
            WHERE TenantId = @tenantId AND IdempotencyKey = @key
            """,
            new { tenantId, key },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> TrySaveAsync(
        Guid tenantId, string key, StoredResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await using var connection = new SqlConnection(connectionString);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.IdempotencyKeys (TenantId, IdempotencyKey, RequestHash, StatusCode, ResponseBody)
                VALUES (@tenantId, @key, @RequestHash, @StatusCode, @ResponseBody)
                """,
                new { tenantId, key, response.RequestHash, response.StatusCode, response.ResponseBody },
                cancellationToken: cancellationToken));
            return true;
        }
        catch (SqlException ex) when (ex.Number == PrimaryKeyViolation)
        {
            return false;
        }
    }
}
