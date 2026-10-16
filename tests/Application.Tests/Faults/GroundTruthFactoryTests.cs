// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Tests.Faults;

public sealed class GroundTruthFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 22, 0, 0, TimeSpan.Zero);
    private static readonly Guid Fault = Guid.Parse("6f1c3c1e-3b0a-4c55-9b0e-0f6a3e1d2c11");

    [Fact]
    public void EquipmentWritesAnOverdueCalibrationOnTheAffectedInstrument()
    {
        var records = GroundTruthFactory.For(RootCauseCategory.EquipmentCalibrationOrAlignment, new ToolId("D-01"), "D-01-A", "chamberPressure", Fault, Now);

        var calibration = records.Calibrations.Should().ContainSingle().Subject;
        calibration.IsOverdue.Should().BeTrue();
        calibration.DueAt.Should().BeBefore(Now);
        calibration.Instrument.Should().Contain("D-01-A");
        records.RecordIds.Should().ContainSingle();
    }

    [Fact]
    public void EngineeringChangeWritesAnEcoBeforeOnsetOnTheTool()
    {
        var eco = GroundTruthFactory.For(RootCauseCategory.EngineeringChange, new ToolId("E-02"), "E-02-A", "escTemperature", Fault, Now).Ecos.Should().ContainSingle().Subject;

        eco.ToolId.Should().Be("E-02");
        eco.EffectiveAt.Should().BeBefore(Now);
    }

    [Fact]
    public void SupplyChainWritesAnOutOfSpecLotWithItsNotice()
    {
        var records = GroundTruthFactory.For(RootCauseCategory.SupplyChainMaterial, new ToolId("E-01"), null, "cf4Flow", Fault, Now);

        records.MaterialLots.Should().ContainSingle();
        records.Notices.Should().ContainSingle().Which.Summary.Should().Contain("outside the purchase specification");
    }

    [Fact]
    public void ProcessDriftWritesACleanRecordWithNoChange()
    {
        GroundTruthFactory.For(RootCauseCategory.ProcessDrift, new ToolId("S-01"), "S-01-A", "temperature", Fault, Now)
            .WorkOrders.Should().ContainSingle().Which.Summary.Should().Contain("No parts or settings changed");
    }

    [Fact]
    public void TheBoardServesCauseRecordsAndResetClearsThem()
    {
        var board = new ConfounderBoard();
        board.AddCause(GroundTruthFactory.For(RootCauseCategory.EquipmentCalibrationOrAlignment, new ToolId("D-01"), "D-01-A", "chamberPressure", Fault, Now));

        board.Calibrations("D-01").Should().ContainSingle();
        board.CauseIds(Fault).Should().ContainSingle();
        board.ConfounderIds(Fault).Should().BeEmpty();
        board.Clear();
        board.Calibrations("D-01").Should().BeEmpty();
    }
}
