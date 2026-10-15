// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.ModelGateway.Replay;

namespace Mps.Api.Demo;

/// <summary>
/// Remembers the scenario id for model calls made while that scenario is running.
/// </summary>
public sealed class ScriptedScenarioContext : IScenarioHint
{
    private readonly AsyncLocal<string?> _current = new();

    public string? CurrentScenarioId => string.IsNullOrWhiteSpace(_current.Value) ? null : _current.Value;

    public void Set(string? scenarioId)
    {
        _current.Value = string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId.Trim();
    }
}
