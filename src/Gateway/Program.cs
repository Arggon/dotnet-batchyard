using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Routes and clusters come from configuration (appsettings.json, overridable by environment variables).
// Moving a route between the monolith and a service = changing its ClusterId (ADR 0014).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        // Tell the caller which backend answered, so a switch is visible in tests, k6 and curl.
        var cluster = context.Cluster?.ClusterId ?? "unknown";
        context.AddResponseHeader("X-Backend", cluster, append: false);
    });

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapReverseProxy();

app.Run();

/// <summary>Entry point, public so tests can host the gateway.</summary>
public partial class Program;
