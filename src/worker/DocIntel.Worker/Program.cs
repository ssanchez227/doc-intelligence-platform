using Azure.Monitor.OpenTelemetry.Exporter;
using DocIntel.Worker;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Traces (including the Service Bus SDK's own) and logs go to Application Insights
// whenever the connection string is present.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource("Azure.*")
            .AddAzureMonitorTraceExporter());
    builder.Logging.AddOpenTelemetry(logging => logging.AddAzureMonitorLogExporter());
}

builder.Services.AddSingleton<IngestionMessageHandler>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
