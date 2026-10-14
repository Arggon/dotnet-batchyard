using System.Threading.RateLimiting;
using Access.Api;
using Access.Data;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Access")
    ?? throw new InvalidOperationException("Connection string 'Access' is not configured.");
var accessOptions = builder.Configuration.GetSection(AccessOptions.Section).Get<AccessOptions>() ?? new AccessOptions();

builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection(AccessOptions.Section));
builder.Services.AddDbContext<AccessDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<CachedAccess>();
if (builder.Configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
{
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redis);
}
else
{
    // Without Redis (e.g. a quick `dotnet run`), fall back to a per-process cache.
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddBatchyardJwt(builder.Configuration);
builder.Services.AddScoped<IPermissionSource, LivePermissionSource>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(AccessEndpoints.TokenRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = accessOptions.TokenRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (accessOptions.RunMigrations)
{
    AccessDatabaseMigrator.Migrate(connectionString);
}

if (accessOptions.Seed is { AdminClientId.Length: > 0 } seed)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DemoSeeder.SeedAsync(scope.ServiceProvider, seed);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccessEndpoints();

app.Run();

/// <summary>Entry point, public so integration tests can host the app.</summary>
public partial class Program;
