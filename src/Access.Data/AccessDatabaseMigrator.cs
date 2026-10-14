using DbUp;

namespace Access.Data;

/// <summary>Creates the access database if needed and applies the embedded scripts from /db/access in order.</summary>
public static class AccessDatabaseMigrator
{
    public static void Migrate(string connectionString, int attempts = 10)
    {
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
            .WithScriptsEmbeddedInAssembly(typeof(AccessDatabaseMigrator).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("Access database migration failed.", result.Error);
        }
    }
}
