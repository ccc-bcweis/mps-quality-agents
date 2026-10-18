// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;

namespace Mps.Agents.Tests;

public sealed class EvidenceGateTests
{
    [Fact]
    public void EngineeringChangeIsSufficientWhenCalibrationIsEmpty()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "ESC temp oscillation", "SPC-1"),
                EvidenceSamples.Item(2, EvidenceSource.Plm, "ECO-4471 edge ring", "ECO-4471"),
                EvidenceSamples.Item(3, EvidenceSource.Cmms, "PM the same week", "WO-19"),
            ],
            ecos: [new EcoRecord("ECO-4471", "Edge ring rev C", "MPS-E7", "R1", EvidenceSamples.At.AddDays(-2), "Edge ring revision C")],
            workOrders: [new WorkOrder("WO-19", EvidenceSamples.Tool, "E-02", "PM", EvidenceSamples.At.AddDays(-1), "Preventive maintenance")],
            calibrationLookupCompleted: true,
            signalShape: SignalShape.Oscillation);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.Sufficient);
        result.Gaps.Should().BeEmpty();
        result.Conflicts.Should().BeEmpty();
        result.EscalationReason.Should().BeNull();
    }

    [Fact]
    public void OverdueCalibrationIsSufficient()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Pressure step", "SPC-2"),
                EvidenceSamples.Item(2, EvidenceSource.Cmms, "Manometer overdue", "CAL-9"),
            ],
            calibrations:
            [
                new CalibrationRecord("CAL-9", EvidenceSamples.Tool, "manometer", EvidenceSamples.At.AddDays(-3), null, true),
            ],
            calibrationLookupCompleted: true,
            signalShape: SignalShape.Step);

        EvidenceGate.Evaluate(pack).Outcome.Should().Be(GateOutcome.Sufficient);
    }

    [Fact]
    public void ARecipeSetpointEditWithNoEngineeringChangeIsSufficient()
    {
        // Live SC-8: the recipe edit was the only discriminator; the gate had passed on an unrelated supplier notice.
        var recipe = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Telemetry, "Bias RF power step", "TEL-1"),
                EvidenceSamples.Item(2, EvidenceSource.Cmms, "An operator edited the RF power recipe setpoint. There is no engineering change order for that edit.", "WO-RCP-1"),
            ],
            workOrders: [new WorkOrder("WO-RCP-1", EvidenceSamples.Tool, "E-03", "process", EvidenceSamples.At.AddDays(-2), "Recipe setpoint edit")],
            signalShape: SignalShape.Step);
        var plainPm = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Telemetry, "Bias RF power step", "TEL-1"),
                EvidenceSamples.Item(2, EvidenceSource.Cmms, "Preventive maintenance", "WO-PM-1"),
            ],
            workOrders: [new WorkOrder("WO-PM-1", EvidenceSamples.Tool, "E-03", "PM", EvidenceSamples.At.AddDays(-2), "Preventive maintenance")],
            signalShape: SignalShape.Step);

        EvidenceGate.Evaluate(recipe).Outcome.Should().Be(GateOutcome.Sufficient);
        EvidenceGate.Evaluate(plainPm).Gaps.Should().Contain("missing-discriminator");
    }

    [Fact]
    public void SupplyChainMaterialIsSufficient()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Telemetry, "Etch rate shift", "TEL-1"),
                EvidenceSamples.Item(2, EvidenceSource.Erp, "New gas lot", "LOT-NF3"),
            ],
            materialLots: [new MaterialLot("LOT-NF3", "NF3", "Northwind Gas", EvidenceSamples.At.AddDays(-1), "SN-4")],
            supplierNotices: [new SupplierNotice("SN-4", "Northwind Gas", "NF3", "LOT-NF3", "Purity excursion", EvidenceSamples.At.AddDays(-1))],
            signalShape: SignalShape.Step);

        EvidenceGate.Evaluate(pack).Outcome.Should().Be(GateOutcome.Sufficient);
    }

    [Fact]
    public void ProcessDriftIsSufficientWhenTheRampHasNoDiscreteEvent()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Slow ramp", "SPC-3"),
                EvidenceSamples.Item(2, EvidenceSource.Cmms, "RF hours since clean", "WO-RF"),
            ],
            workOrders: [new WorkOrder("WO-RF", EvidenceSamples.Tool, "E-02", "rf-hours", EvidenceSamples.At, "RF hours since the last clean")],
            calibrationLookupCompleted: true,
            signalShape: SignalShape.Ramp);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.Sufficient);
        result.EscalationReason.Should().BeNull();
    }

    [Fact]
    public void AnEmptyPackRequestsInformationWithNamedGaps()
    {
        var result = EvidenceGate.Evaluate(EvidenceSamples.Pack());

        result.Outcome.Should().Be(GateOutcome.RequestInfo);
        result.EscalationReason.Should().Be("request_info: missing-signal, missing-context, insufficient-count, missing-discriminator");
    }

    [Fact]
    public void MissingSignalRequestsInformation()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(2, EvidenceSource.Plm, "ECO-4471", "ECO-4471"),
                EvidenceSamples.Item(3, EvidenceSource.Mes, "Recipe header", "RUN-1"),
            ],
            ecos: [new EcoRecord("ECO-4471", "Edge ring", "MPS-E7", null, EvidenceSamples.At, "Edge ring")]);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.RequestInfo);
        result.Gaps.Should().Contain("missing-signal");
    }

    [Fact]
    public void MissingContextRequestsInformation()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Oscillation", "SPC-1"),
                EvidenceSamples.Item(4, EvidenceSource.Telemetry, "ESC temp", "TEL-1"),
            ],
            signalShape: SignalShape.Oscillation);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.RequestInfo);
        result.Gaps.Should().Contain("missing-context");
    }

    [Fact]
    public void FewerThanTwoItemsRequestsInformation()
    {
        var pack = EvidenceSamples.Pack(
            items: [EvidenceSamples.Item(1, EvidenceSource.Spc, "One point", "SPC-1")],
            ecos: [new EcoRecord("ECO-1", "Change", "MPS-E7", null, EvidenceSamples.At, "Change")]);

        EvidenceGate.Evaluate(pack).Gaps.Should().Contain("insufficient-count");
    }

    [Fact]
    public void SignalWithoutADiscriminatorRequestsInformation()
    {
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Oscillation", "SPC-1"),
                EvidenceSamples.Item(3, EvidenceSource.Cmms, "PM the same week", "WO-19"),
            ],
            workOrders: [new WorkOrder("WO-19", EvidenceSamples.Tool, "E-02", "PM", EvidenceSamples.At, "Preventive maintenance")],
            signalShape: SignalShape.Oscillation);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.RequestInfo);
        result.Gaps.Should().ContainSingle().Which.Should().Be("missing-discriminator");
    }

    [Fact]
    public void ConflictingRevisionsEscalateAndNameBoth()
    {
        var pack = SufficientEngineeringChange(
            configClaims:
            [
                new ConfigClaim(EvidenceSource.Plm, "showerhead", "C", "CFG-1"),
                new ConfigClaim(EvidenceSource.Mes, "showerhead", "B", "RUN-1"),
            ],
            calibrationLookupCompleted: true);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.Escalate);
        result.EscalationReason.Should().Contain("rev C");
        result.EscalationReason.Should().Contain("rev B");
        result.EscalationReason.Should().Contain("calibration records missing");
        result.EscalationReason.Should().StartWith("config-revision:");
    }

    [Fact]
    public void MatchingRevisionsAndDifferentPartsAreNotConflicts()
    {
        var pack = SufficientEngineeringChange(
            configClaims:
            [
                new ConfigClaim(EvidenceSource.Plm, "showerhead", "C", "CFG-1"),
                new ConfigClaim(EvidenceSource.Mes, "showerhead", " c ", "RUN-1"),
                new ConfigClaim(EvidenceSource.Mes, "edge ring", "B", "RUN-2"),
            ]);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.Sufficient);
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void SameSourceRevisionsAreNotAConflict()
    {
        var pack = SufficientEngineeringChange(
            configClaims:
            [
                new ConfigClaim(EvidenceSource.Plm, "showerhead", "B", "CFG-1"),
                new ConfigClaim(EvidenceSource.Plm, "showerhead", "C", "CFG-2"),
            ]);

        EvidenceGate.Evaluate(pack).Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void TheSameExternalIdWithDifferentTimestampsEscalates()
    {
        var pack = SufficientEngineeringChange(
            extraItems:
            [
                EvidenceSamples.Item(8, EvidenceSource.Plm, "ECO again", "ECO-4471", EvidenceSamples.At.AddDays(-1)),
            ]);

        var result = EvidenceGate.Evaluate(pack);

        result.Outcome.Should().Be(GateOutcome.Escalate);
        result.Conflicts.Should().Contain(conflict => conflict.Contains("timestamp-conflict", StringComparison.Ordinal) && conflict.Contains("ECO-4471", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameExternalIdAtTheSameInstantIsNotAConflict()
    {
        var instant = EvidenceSamples.At;
        var otherOffset = instant.ToOffset(TimeSpan.FromHours(2));
        var pack = EvidenceSamples.Pack(
            items:
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Signal", "SPC-1", instant),
                EvidenceSamples.Item(2, EvidenceSource.Plm, "ECO", "ECO-4471", instant),
                new EvidenceItem(EvidenceSamples.Id(9), EvidenceSource.Mes, "Header", "Header", otherOffset, "MPS-E7", "ECO-4471"),
            ],
            ecos: [new EcoRecord("ECO-4471", "Edge ring", "MPS-E7", null, instant, "Edge ring")]);

        EvidenceGate.Evaluate(pack).Outcome.Should().Be(GateOutcome.Sufficient);
    }

    private static EvidencePack SufficientEngineeringChange(
        IReadOnlyList<ConfigClaim>? configClaims = null,
        IReadOnlyList<EvidenceItem>? extraItems = null,
        bool calibrationLookupCompleted = false)
    {
        List<EvidenceItem> items =
        [
            EvidenceSamples.Item(1, EvidenceSource.Spc, "ESC temp oscillation", "SPC-1"),
            EvidenceSamples.Item(2, EvidenceSource.Plm, "ECO-4471 edge ring", "ECO-4471"),
        ];
        if (extraItems is not null)
        {
            items.AddRange(extraItems);
        }

        return EvidenceSamples.Pack(
            items,
            configClaims,
            ecos: [new EcoRecord("ECO-4471", "Edge ring rev C", "MPS-E7", "R1", EvidenceSamples.At.AddDays(-2), "Edge ring revision C")],
            calibrationLookupCompleted: calibrationLookupCompleted,
            signalShape: SignalShape.Oscillation);
    }
}
