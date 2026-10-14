using DbUp;

namespace Ingest.Data;

/// <summary>Creates the database if needed and applies the embedded SQL scripts from /db/ingest in order.</summary>
public static class DatabaseMigrator
{
    public static void Migrate(string connectionString, int attempts = 10)
    {
        // SQL Server can report healthy a moment before it accepts logins for new databases; retry briefly.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                EnsureDatabase.For.SqlDatabase(connectionString);
                break;
            }
            catch (Microsoft.Data.SqlClient.SqlException) when (attempt < attempts)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        var result = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }
    }
}
