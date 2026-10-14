// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Faults;

public static class FaultCategories
{
    public const string EngineeringChange = "engineeringChange";

    public const string Calibration = "equipmentCalibrationOrAlignment";

    public const string Material = "supplyChainMaterial";

    public const string Drift = "processDrift";

    public const string Recommend = "recommend";

    public const string Escalate = "escalate";

    public const string RequestInfo = "requestInfo";

    public static readonly string[] All = [EngineeringChange, Calibration, Material, Drift];

    public static readonly string[] Behaviors = [Recommend, Escalate, RequestInfo];
}

public sealed record FaultRuns(string RecipeId, string RecipeRevision, string LotId, string MaterialCode, int Count);

public sealed record MissingRecord(string RecordId, string RecordType, string Reason);

public sealed record RevisionConflict(string PlmRevision, string MesRevision, string PartName, string Detail);

public abstract record Confounder
{
    public sealed record PreventiveMaintenance(WorkOrder Order) : Confounder;

    public sealed record SupplierLotChange(MaterialLot Lot, SupplierNotice Notice, IReadOnlyList<string> ToolIds) : Confounder;

    public sealed record ChangeOrder(EcoRecord Eco) : Confounder;
}

public sealed class FaultEvidence
{
    public FaultEvidence(
        EcoRecord? eco,
        ConfigRevision? config,
        CalibrationRecord? calibration,
        MaterialLot? materialLot,
        IReadOnlyList<string> materialToolIds,
        SupplierNotice? notice,
        WorkOrder? workOrder,
        FaultRuns? runs,
        IReadOnlyList<Confounder> confounders,
        IReadOnlyList<MissingRecord> missing,
        RevisionConflict? contradiction,
        bool contradictory,
        bool missingEvidence)
    {
        ArgumentNullException.ThrowIfNull(materialToolIds);
        ArgumentNullException.ThrowIfNull(confounders);
        ArgumentNullException.ThrowIfNull(missing);
        Eco = eco;
        Config = config;
        Calibration = calibration;
        MaterialLot = materialLot;
        MaterialToolIds = materialToolIds.ToArray();
        Notice = notice;
        WorkOrder = workOrder;
        Runs = runs;
        Confounders = confounders.ToArray();
        Missing = missing.ToArray();
        Contradiction = contradiction;
        Contradictory = contradictory;
        MissingEvidence = missingEvidence;
    }

    public EcoRecord? Eco { get; }

    public ConfigRevision? Config { get; }

    public CalibrationRecord? Calibration { get; }

    public MaterialLot? MaterialLot { get; }

    public IReadOnlyList<string> MaterialToolIds { get; }

    public SupplierNotice? Notice { get; }

    public WorkOrder? WorkOrder { get; }

    public FaultRuns? Runs { get; }

    public IReadOnlyList<Confounder> Confounders { get; }

    public IReadOnlyList<MissingRecord> Missing { get; }

    public RevisionConflict? Contradiction { get; }

    public bool Contradictory { get; }

    public bool MissingEvidence { get; }
}

public sealed class FaultDefinition
{
    public FaultDefinition(
        string id,
        string title,
        string category,
        string scenario,
        string expectedBehavior,
        bool assertCategory,
        IReadOnlyList<string> toolIds,
        string? chamberId,
        string parameter,
        FaultShape shape,
        MagnitudeKind magnitudeKind,
        double magnitude,
        DateTimeOffset onset,
        double periodSeconds,
        double rampSeconds,
        double dutyCycle,
        string cause,
        IReadOnlyList<string> counterEvidence,
        IReadOnlyList<string> actions,
        FaultEvidence evidence)
    {
        Id = Require(id);
        Title = Require(title);
        Category = Require(category);
        Scenario = Require(scenario);
        ExpectedBehavior = Require(expectedBehavior);
        Parameter = Require(parameter);
        Cause = Require(cause);
        ArgumentNullException.ThrowIfNull(toolIds);
        ArgumentNullException.ThrowIfNull(counterEvidence);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(evidence);
        AssertCategory = assertCategory;
        ToolIds = toolIds.ToArray();
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Shape = shape;
        MagnitudeKind = magnitudeKind;
        Magnitude = magnitude;
        Onset = onset;
        PeriodSeconds = periodSeconds;
        RampSeconds = rampSeconds;
        DutyCycle = dutyCycle;
        CounterEvidence = counterEvidence.ToArray();
        Actions = actions.ToArray();
        Evidence = evidence;
    }

    public string Id { get; }

    public string Title { get; }

    public string Category { get; }

    public string Scenario { get; }

    public string ExpectedBehavior { get; }

    public bool AssertCategory { get; }

    public IReadOnlyList<string> ToolIds { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public FaultShape Shape { get; }

    public MagnitudeKind MagnitudeKind { get; }

    public double Magnitude { get; }

    public DateTimeOffset Onset { get; }

    public double PeriodSeconds { get; }

    public double RampSeconds { get; }

    public double DutyCycle { get; }

    public string Cause { get; }

    public IReadOnlyList<string> CounterEvidence { get; }

    public IReadOnlyList<string> Actions { get; }

    public FaultEvidence Evidence { get; }

    public IReadOnlyList<FaultSignal> ToSignals()
    {
        return ToolIds.Select(toolId => new FaultSignal(
            toolId,
            ChamberId,
            Parameter,
            Shape,
            MagnitudeKind,
            Magnitude,
            Onset,
            PeriodSeconds,
            RampSeconds,
            DutyCycle)).ToArray();
    }

    private static string Require(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.");
        }

        return value.Trim();
    }
}

public sealed class FaultCatalog
{
    public FaultCatalog(IReadOnlyList<FaultDefinition> faults)
    {
        ArgumentNullException.ThrowIfNull(faults);
        Faults = faults.ToArray();
    }

    public IReadOnlyList<FaultDefinition> Faults { get; }

    public FaultDefinition GetRequired(string id)
    {
        var fault = Faults.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (fault is null)
        {
            throw new FaultCatalogException([$"Fault {id} is not in the catalog."]);
        }

        return fault;
    }
}
