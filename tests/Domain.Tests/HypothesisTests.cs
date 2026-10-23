// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class HypothesisTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(0.5)]
    public void AHypothesisKeepsAFractionAndTrimsItsText(double confidence)
    {
        var cited = new List<EvidenceId> { EvidenceId.New(), EvidenceId.New() };
        var against = new List<string> { "  An ECO landed the same week.  " };
        var actions = new List<string> { "  Quarantine the lot.  " };
        var ruledOut = new RuledOutEntry(EvidenceId.New(), "The notice is older.", RuledOutSource.Record, RootCauseCategory.SupplyChainMaterial);
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "  The chamber has drifted.  ",
            confidence,
            cited,
            against,
            actions,
            [ruledOut]);

        hypothesis.Category.Should().Be(RootCauseCategory.ProcessDrift);
        hypothesis.Cause.Should().Be("The chamber has drifted.");
        hypothesis.Confidence.Should().Be(confidence);
        hypothesis.EvidenceIds.Should().Equal(cited);
        hypothesis.CounterEvidence.Should().Equal("An ECO landed the same week.");
        hypothesis.Actions.Should().Equal("Quarantine the lot.");
        hypothesis.RuledOut.Should().ContainSingle().Which.Should().BeSameAs(ruledOut);

        cited.Clear();
        against.Clear();
        actions.Clear();
        hypothesis.EvidenceIds.Should().HaveCount(2);
        hypothesis.CounterEvidence.Should().ContainSingle();
        hypothesis.Actions.Should().ContainSingle();
    }

    [Fact]
    public void EmptyCounterEvidenceAndActionsAreAllowedAndAMissingRulingListIsEmpty()
    {
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EngineeringChange,
            "Edge ring revision.",
            0,
            [EvidenceId.New()],
            [],
            []);

        hypothesis.CounterEvidence.Should().BeEmpty();
        hypothesis.Actions.Should().BeEmpty();
        hypothesis.RuledOut.Should().BeEmpty();
        hypothesis.Category.Should().Be(RootCauseCategory.EngineeringChange);
    }

    [Fact]
    public void ConfidenceOutsideZeroToOneIsRejectedIncludingANonNumber()
    {
        Rejects(1.0001, [EvidenceId.New()], "Confidence must be between 0 and 1.");
        Rejects(-0.0001, [EvidenceId.New()], "Confidence must be between 0 and 1.");
        Rejects(double.NaN, [EvidenceId.New()], "Confidence must be between 0 and 1.");
        Rejects(double.PositiveInfinity, [EvidenceId.New()], "Confidence must be between 0 and 1.");
        Rejects(double.NegativeInfinity, [EvidenceId.New()], "Confidence must be between 0 and 1.");
    }

    [Fact]
    public void AHypothesisRequiresAnIdACauseAndAtLeastOneRealEvidenceId()
    {
        var evidence = EvidenceId.New();
        Rejects(0.5, [evidence], "Hypothesis id is required.", id: default(HypothesisId));
        Rejects(0.5, [evidence], "Hypothesis cause is required.", cause: " ");
        Rejects(0.5, [evidence], "Hypothesis cause is required.", cause: null!);
        Rejects(0.5, [], "A hypothesis must cite at least one evidence item.");
        Rejects(0.5, [default, evidence], "Evidence ids are required.");
        Rejects(0.5, [evidence, default], "Evidence ids are required.");

        var nullEvidence = () => new Hypothesis(HypothesisId.New(), RootCauseCategory.ProcessDrift, "cause", 0.5, null!, [], []);
        nullEvidence.Should().Throw<ArgumentNullException>().WithParameterName("evidenceIds");
        var nullCounter = () => new Hypothesis(HypothesisId.New(), RootCauseCategory.ProcessDrift, "cause", 0.5, [evidence], null!, []);
        nullCounter.Should().Throw<ArgumentNullException>().WithParameterName("counterEvidence");
        var nullActions = () => new Hypothesis(HypothesisId.New(), RootCauseCategory.ProcessDrift, "cause", 0.5, [evidence], [], null!);
        nullActions.Should().Throw<ArgumentNullException>().WithParameterName("actions");
    }

    [Fact]
    public void CounterEvidenceAndActionsMustEachHaveText()
    {
        var evidence = new[] { EvidenceId.New() };
        var blankCounter = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "cause",
            0.2,
            evidence,
            ["kept", "  "],
            ["act"]);
        blankCounter.Should().Throw<DomainRuleException>().WithMessage("Counter-evidence text is required.");

        var nullCounter = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "cause",
            0.2,
            evidence,
            [null!],
            ["act"]);
        nullCounter.Should().Throw<DomainRuleException>().WithMessage("Counter-evidence text is required.");

        var blankAction = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.SupplyChainMaterial,
            "cause",
            0.2,
            evidence,
            [],
            ["  "]);
        blankAction.Should().Throw<DomainRuleException>().WithMessage("Proposed action text is required.");

        var nullAction = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.SupplyChainMaterial,
            "cause",
            0.2,
            evidence,
            [],
            [null!]);
        nullAction.Should().Throw<DomainRuleException>().WithMessage("Proposed action text is required.");
    }

    private static void Rejects(double confidence, IReadOnlyList<EvidenceId> evidence, string message, HypothesisId? id = null, string cause = "cause")
    {
        var act = () => new Hypothesis(
            id ?? HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            cause,
            confidence,
            evidence,
            [],
            []);
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
