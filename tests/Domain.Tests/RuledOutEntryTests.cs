// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class RuledOutEntryTests
{
    [Fact]
    public void RuledOutDefaultsToEmpty()
    {
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "The bearing is worn.",
            0.8,
            [EvidenceId.New()],
            [],
            ["Replace the bearing."]);

        hypothesis.RuledOut.Should().BeEmpty();
    }

    [Fact]
    public void ARuledOutRecordIsKeptWhenItIsNotCited()
    {
        var cited = EvidenceId.New();
        var ruledOut = EvidenceId.New();
        var entry = new RuledOutEntry(
            ruledOut,
            "  Lot matches its specification.  ",
            RuledOutSource.Record,
            RootCauseCategory.SupplyChainMaterial);

        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "The bearing is worn.",
            0.8,
            [cited],
            [],
            ["Replace the bearing."],
            [entry]);

        hypothesis.EvidenceIds.Should().Equal(cited);
        hypothesis.RuledOut.Should().ContainSingle();
        hypothesis.RuledOut[0].Id.Should().Be(ruledOut);
        hypothesis.RuledOut[0].Reason.Should().Be("Lot matches its specification.");
        hypothesis.RuledOut[0].Source.Should().Be(RuledOutSource.Record);
        hypothesis.RuledOut[0].Class.Should().Be(RootCauseCategory.SupplyChainMaterial);
    }

    [Fact]
    public void AnAgentRulingMayOmitTheClass()
    {
        var entry = new RuledOutEntry(
            EvidenceId.New(),
            "The notice does not explain the drift.",
            RuledOutSource.Agent,
            null);

        entry.Source.Should().Be(RuledOutSource.Agent);
        entry.Class.Should().BeNull();
    }

    [Fact]
    public void AnIdCannotBeBothCitedAndRuledOut()
    {
        var id = EvidenceId.New();
        var entry = new RuledOutEntry(id, "It does not support the cause.", RuledOutSource.Agent, null);

        var act = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "The chamber has drifted.",
            0.5,
            [id],
            [],
            ["Clean the chamber."],
            [entry]);

        act.Should().Throw<DomainRuleException>().WithMessage("*both cited and ruled out*");
    }

    [Fact]
    public void ARuledOutEntryRequiresAnIdAndAReason()
    {
        var missingId = () => new RuledOutEntry(default, "A reason.", RuledOutSource.Record, null);
        missingId.Should().Throw<DomainRuleException>().WithMessage("*evidence id*");

        var missingReason = () => new RuledOutEntry(EvidenceId.New(), "   ", RuledOutSource.Record, null);
        missingReason.Should().Throw<DomainRuleException>().WithMessage("*reason*");
    }
}
