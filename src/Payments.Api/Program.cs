using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Payments.Api;
using Payments.Application;
using Payments.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddBatchyardTelemetry("payments-api");

var connectionString = builder.Configuration.GetConnectionString("Payments")
    ?? throw new InvalidOperationException("Connection string 'Payments' is not configured.");

builder.Services.AddPaymentsApplication();
builder.Services.AddPaymentsData(connectionString,
    builder.Configuration.GetSection("Gateway").Get<Payments.Data.Gateway.GatewayResilienceOptions>());
if (builder.Configuration.GetValue("Messaging:Enabled", true))
{
    builder.Services.AddPaymentsMessaging(connectionString,
        builder.Configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new MessagingOptions());
}

builder.Services.AddBatchyardJwt(builder.Configuration);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("Payments:RunMigrations", true))
{
    PaymentsDatabaseMigrator.Migrate(connectionString);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPaymentEndpoints();

app.Run();

/// <summary>Entry point, public so integration tests can host the app.</summary>
public partial class Program;
