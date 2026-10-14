using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

/// <summary>One call per service: traces, metrics and logs over OpenTelemetry (ADR 0016).</summary>
public static class TelemetryExtensions
{
    /// <summary>Same name as <c>BuildingBlocks.Messaging.MessagingTelemetry.SourceName</c>; repeated so this block does not depend on messaging.</summary>
    public const string MessagingSource = "Batchyard.Messaging";

    /// <summary>
    /// Registers tracing (ASP.NET Core, HttpClient, SqlClient, messaging spans) and metrics (ASP.NET Core, HttpClient, runtime).
    /// Export is OTLP and only switched on when OTEL_EXPORTER_OTLP_ENDPOINT is set, so tests and plain `dotnet run` export nothing.
    /// </summary>
    public static WebApplicationBuilder AddBatchyardTelemetry(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o => o.Filter = http => !IsHealthCheck(http))
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddSource(MessagingSource))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    private static bool IsHealthCheck(HttpContext http) => http.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
