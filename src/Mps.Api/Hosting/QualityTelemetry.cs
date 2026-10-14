// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Persistence;
using Mps.Application.Metrics;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mps.Api.Hosting;

internal static class QualityTelemetry
{
    public static void AddQualityTelemetry(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Configuration.GetValue("Otel:Enabled", true))
        {
            return;
        }

        var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var export = !string.IsNullOrWhiteSpace(endpoint);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Mps.Api"))
            .WithTracing(tracing =>
            {
                tracing.AddSource(QualityDiagnostics.ActivitySourceName);
                tracing.AddAspNetCoreInstrumentation();
                if (export)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(EfMetricsLog.MeterName);
                metrics.AddAspNetCoreInstrumentation();
                if (export)
                {
                    metrics.AddOtlpExporter();
                }
            });
    }
}
