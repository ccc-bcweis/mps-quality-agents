// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Demo;

/// <summary>
/// Remembers which scripted scenario is staged, so enterprise tools can serve that scenario's own records.
/// The injector sets it; demo reset clears it. It outlives the trigger request, unlike the scenario hint.
/// </summary>
public sealed class ScenarioEvidenceBoard
{
    private volatile string? _active;

    private DateTimeOffset? _activatedAt;

    public string? ActiveScenarioId => _active;

    /// <summary>Plant time the scenario was staged; scenario records keep their spacing from its anchor, shifted to here.</summary>
    public DateTimeOffset? ActivatedAt => _activatedAt;

    public void Activate(string scenarioId, DateTimeOffset? activatedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        _activatedAt = activatedAt;
        _active = scenarioId.Trim();
    }

    public void Clear()
    {
        _active = null;
        _activatedAt = null;
    }
}
