using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Reads and settles rows of a service's dbo.Outbox table. Every service uses the same table shape:
/// Id, Type, Payload, OccurredAt, PublishedAt, Attempts.
/// </summary>
public sealed class SqlOutboxStore(string connectionString)
{
    /// <summary>
    /// Claims a batch with UPDLOCK + READPAST so two dispatcher instances never pick the same row,
    /// publishes each one through <paramref name="publish"/>, and marks it published in the same transaction.
    /// </summary>
    public async Task<int> DispatchBatchAsync(int batchSize, Func<IntegrationMessage, Task> publish, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publish);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        var rows = (await connection.QueryAsync<IntegrationMessage>(new CommandDefinition(
            """
            SELECT TOP (@batchSize) Id, Type, Payload
            FROM dbo.Outbox WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE PublishedAt IS NULL
            ORDER BY OccurredAt
            """,
            new { batchSize }, tx, cancellationToken: ct))).ToList();

        var sent = 0;
        foreach (var row in rows)
        {
            try
            {
                await publish(row);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Keep order: stop at the first failure and retry it on the next poll.
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Outbox SET Attempts = Attempts + 1 WHERE Id = @Id", new { row.Id }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                throw;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.Outbox SET PublishedAt = SYSUTCDATETIME(), Attempts = Attempts + 1 WHERE Id = @Id",
                new { row.Id }, tx, cancellationToken: ct));
            sent++;
        }

        await tx.CommitAsync(ct);
        return sent;
    }
}
