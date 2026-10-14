using BuildingBlocks.Auth;
using Ingest.Api.Items;
using Ingest.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Ingest")
    ?? throw new InvalidOperationException("Connection string 'Ingest' is not configured.");

builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.Section));
builder.Services.AddSingleton<IItemRepository>(_ => new SqlItemRepository(connectionString));
builder.Services.AddSingleton<IIdempotencyStore>(_ => new SqlIdempotencyStore(connectionString));
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
