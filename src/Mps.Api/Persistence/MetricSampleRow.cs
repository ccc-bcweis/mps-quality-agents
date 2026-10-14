// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Persistence;

public sealed class MetricSampleRow
{
    public Guid Id { get; set; }

    public Guid InvestigationId { get; set; }

    public DateTime RecordedAt { get; set; }

    public bool Completed { get; set; }

    public bool Succeeded { get; set; }

    public double? GroundingScore { get; set; }

    public int ToolSuccesses { get; set; }

    public int ToolFailures { get; set; }

    public double? LatencyMs { get; set; }

    public double CostUsd { get; set; }
}
