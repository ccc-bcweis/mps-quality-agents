// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Telemetry;

/// <summary>
/// One simulated sample. The frame has no root-cause field. Agents see the signal, not the fault label.
/// </summary>
public sealed record TelemetryFrame
{
    public TelemetryFrame(
        string toolId,
        string chamberId,
        string parameter,
        string unit,
        string sensorId,
        DateTimeOffset simulatedAt,
        double value,
        string recipeId,
        string stepId,
        string runId,
        string lotId)
    {
        ToolId = Require(toolId, nameof(toolId));
        ChamberId = Require(chamberId, nameof(chamberId));
        Parameter = Require(parameter, nameof(parameter));
        Unit = Require(unit, nameof(unit));
        SensorId = Require(sensorId, nameof(sensorId));
        RecipeId = Require(recipeId, nameof(recipeId));
        StepId = Require(stepId, nameof(stepId));
        RunId = Require(runId, nameof(runId));
        LotId = Require(lotId, nameof(lotId));
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Telemetry value must be finite.");
        }

        SimulatedAt = simulatedAt;
        Value = value;
    }

    public string ToolId { get; }

    public string ChamberId { get; }

    public string Parameter { get; }

    public string Unit { get; }

    /// <summary>Catalog id from <c>data/fleet</c>. Tool-scoped points use the tool id as <see cref="ChamberId"/>.</summary>
    public string SensorId { get; }

    public DateTimeOffset SimulatedAt { get; }

    public double Value { get; }

    public string RecipeId { get; }

    public string StepId { get; }

    public string RunId { get; }

    public string LotId { get; }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}
