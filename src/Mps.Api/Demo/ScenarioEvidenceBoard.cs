// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Demo;

/// <summary>
/// Remembers which scripted scenario is staged, so enterprise tools can serve that scenario's own records.
/// The injector sets it; demo reset clears it. It outlives the trigger request, unlike the scenario hint.
/// </summary>
public sealed class ScenarioEvidenceBoard
{
    private volatile string? _active;

    public string? ActiveScenarioId => _active;

    public void Activate(string scenarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        _active = scenarioId.Trim();
    }

    public void Clear() => _active = null;
}
