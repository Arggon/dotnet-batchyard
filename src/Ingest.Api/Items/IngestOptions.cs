namespace Ingest.Api.Items;

public sealed class IngestOptions
{
    public const string Section = "Ingest";

    /// <summary>Largest batch accepted in one request (spec default: 5,000).</summary>
    public int MaxBatchSize { get; set; } = 5000;

    /// <summary>Apply the SQL migrations on startup. On for local runs and tests.</summary>
    public bool RunMigrations { get; set; } = true;
}
