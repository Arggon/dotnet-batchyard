using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Ingest.Api.Items;
using Ingest.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddBatchyardTelemetry("ingest-api");

var connectionString = builder.Configuration.GetConnectionString("Ingest")
    ?? throw new InvalidOperationException("Connection string 'Ingest' is not configured.");

builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.Section));
builder.Services.AddSingleton<IItemRepository>(_ => new SqlItemRepository(connectionString));
builder.Services.AddSingleton<IIdempotencyStore>(_ => new SqlIdempotencyStore(connectionString));
// ingest.completed: the batch transaction writes it to dbo.Outbox; this publishes it (ADR 0017).
if (builder.Configuration.GetValue("Messaging:Enabled", true))
{
    builder.Services.AddBatchyardMessaging(builder.Configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new MessagingOptions());
    builder.Services.AddOutboxDispatcher(connectionString);
}

builder.Services.AddBatchyardJwt(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue(IngestOptions.Section + ":RunMigrations", true))
{
    DatabaseMigrator.Migrate(connectionString);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapItemEndpoints();

app.Run();

/// <summary>Entry point, public so integration tests can host the app.</summary>
public partial class Program;
