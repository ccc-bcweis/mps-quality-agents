// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Telemetry;

namespace Mps.Simulator.GroundTruth;

/// <summary>
/// Hidden answer for eval. Serialize this only under <c>data/groundtruth</c>. Do not return it from an agent tool.
/// </summary>
public sealed class GroundTruthDocument
{
    public GroundTruthDocument(
        string faultId,
        string scenario,
        string category,
        string expectedBehavior,
        bool assertCategory,
        string cause,
        IReadOnlyList<string> tools,
        string parameter,
        string? chamberId,
        IReadOnlyList<string> evidenceRecordIds,
        IReadOnlyList<string> confounderRecordIds,
        IReadOnlyList<string> missingRecordIds,
        string? contradiction,
        IReadOnlyList<string> counterEvidence,
        IReadOnlyList<FaultSignal> signals)
    {
        FaultId = Require(faultId, nameof(faultId));
        Scenario = Require(scenario, nameof(scenario));
        Category = Require(category, nameof(category));
        ExpectedBehavior = Require(expectedBehavior, nameof(expectedBehavior));
        Cause = Require(cause, nameof(cause));
        Parameter = Require(parameter, nameof(parameter));
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(evidenceRecordIds);
        ArgumentNullException.ThrowIfNull(confounderRecordIds);
        ArgumentNullException.ThrowIfNull(missingRecordIds);
        ArgumentNullException.ThrowIfNull(counterEvidence);
        ArgumentNullException.ThrowIfNull(signals);
        if (tools.Count == 0 || signals.Count == 0)
        {
            throw new ArgumentException("A ground-truth document needs a tool and a signal.");
        }

        AssertCategory = assertCategory;
        Tools = tools.ToArray();
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        EvidenceRecordIds = evidenceRecordIds.ToArray();
        ConfounderRecordIds = confounderRecordIds.ToArray();
        MissingRecordIds = missingRecordIds.ToArray();
        Contradiction = string.IsNullOrWhiteSpace(contradiction) ? null : contradiction.Trim();
        CounterEvidence = counterEvidence.ToArray();
        Signals = signals.ToArray();
    }

    public string FaultId { get; }

    public string Scenario { get; }

    public string Category { get; }

    public string ExpectedBehavior { get; }

    public bool AssertCategory { get; }

    public string Cause { get; }

    public IReadOnlyList<string> Tools { get; }

    public string Parameter { get; }

    public string? ChamberId { get; }

    public IReadOnlyList<string> EvidenceRecordIds { get; }

    public IReadOnlyList<string> ConfounderRecordIds { get; }

    public IReadOnlyList<string> MissingRecordIds { get; }

    public string? Contradiction { get; }

    public IReadOnlyList<string> CounterEvidence { get; }

    public IReadOnlyList<FaultSignal> Signals { get; }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}
