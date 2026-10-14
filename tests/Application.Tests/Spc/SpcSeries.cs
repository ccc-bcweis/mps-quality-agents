// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;
using Mps.Domain.Identity;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tests.Spc;

internal static class SpcSeries
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static readonly ToolId Tool = new("E-02");

    public static ControlBaseline Unit { get; } = new(0d, 1d, null, null);

    public static TelemetryPoint[] Values(params double[] values) => Shifted(TimeSpan.Zero, values);

    public static TelemetryPoint[] Shifted(TimeSpan shift, params double[] values)
    {
        var points = new TelemetryPoint[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            points[index] = new TelemetryPoint(Start.Add(shift).AddMinutes(index), values[index]);
        }

        return points;
    }

    public static SpcSample Sample(params double[] values) =>
        new(Tool, "A", "escTemperature", Unit, Values(values));
}
