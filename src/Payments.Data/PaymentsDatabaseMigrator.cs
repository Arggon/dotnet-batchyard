using DbUp;

namespace Payments.Data;

/// <summary>Creates the payments database if needed and applies the embedded scripts from /db/payments in order.</summary>
public static class PaymentsDatabaseMigrator
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
            .WithScriptsEmbeddedInAssembly(typeof(PaymentsDatabaseMigrator).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("Payments database migration failed.", result.Error);
        }
    }
}
