// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class GroundingValidatorTests
{
    [Fact]
    public void AcceptsACitedCategoryAndTextCounterEvidence()
    {
        var pack = Pack();
        var claim = Claim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C changed by ECO-4471.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["PM WO-19 was performed the same week."],
            ["Review ECO-4471 before opening a corrective action."]);

        var result = GroundingValidator.Validate(claim, pack);

        result.Accepted.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public void RejectsAnUnknownEvidenceId()
    {
        var claim = Claim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(99)]);

        GroundingValidator.Validate(claim, Pack()).Failures.Should().Contain("unknown-evidence-id:" + EvidenceSamples.Id(99));
    }

    [Fact]
    public void RejectsACategoryWithoutACause()
    {
        var claim = Claim(RootCauseCategory.ProcessDrift, "  ", 0.4d, [EvidenceSamples.Id(1)]);

        GroundingValidator.Validate(claim, Pack()).Failures.Should().Contain("category-without-cause");
    }

    [Fact]
    public void RejectsACategoryWithoutCitations()
    {
        var claim = Claim(RootCauseCategory.SupplyChainMaterial, "Lot LOT-NF3.", 0.7d, []);

        GroundingValidator.Validate(claim, Pack()).Failures.Should().Contain("category-without-evidence");
    }

    [Fact]
    public void RejectsACauseWithoutACategory()
    {
        var claim = Claim(null, "Edge ring revision C.", null, []);

        GroundingValidator.Validate(claim, Pack()).Failures.Should().Contain("cause-without-category");
    }

    [Fact]
    public void RejectsConfidenceOutsideTheUnitInterval()
    {
        var high = Claim(RootCauseCategory.EquipmentCalibrationOrAlignment, "Manometer overdue.", 1.1d, [EvidenceSamples.Id(1)]);
        var low = Claim(RootCauseCategory.EquipmentCalibrationOrAlignment, "Manometer overdue.", -0.1d, [EvidenceSamples.Id(1)]);
        var missing = Claim(RootCauseCategory.EquipmentCalibrationOrAlignment, "Manometer overdue.", null, [EvidenceSamples.Id(1)]);

        GroundingValidator.Validate(high, Pack()).Failures.Should().Contain("confidence-out-of-range");
        GroundingValidator.Validate(low, Pack()).Failures.Should().Contain("confidence-out-of-range");
        GroundingValidator.Validate(missing, Pack()).Failures.Should().Contain("confidence-required");
    }

    [Fact]
    public void RejectsAnUncitedClaimAndAClaimThatCitesAnUnknownId()
    {
        var uncited = new GroundedStatement("The edge ring moved.", []);
        var unknown = new GroundedStatement("A second sentence.", [EvidenceSamples.Id(99)]);
        var claim = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            [],
            [],
            [uncited, unknown]);

        var failures = GroundingValidator.Validate(claim, Pack()).Failures;

        failures.Should().Contain("uncited-claim");
        failures.Should().Contain("unknown-claim-evidence-id:" + EvidenceSamples.Id(99));
    }

    [Fact]
    public void RepairsAMissingExplanationAndMatrixFromTheCatalog()
    {
        var claim = Claim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."]);

        var result = GroundingValidator.Validate(claim, SensorPack(), WhyItMattersCatalog.Empty);

        result.Accepted.Should().BeTrue();
        result.Repaired.Should().BeTrue();
        result.Failures.Should().BeEmpty();
        result.Claim!.Explanations.Should().ContainSingle();
        result.Claim.Explanations[0].EvidenceId.Should().Be(EvidenceSamples.Id(1));
        result.Claim.Explanations[0].SignalId.Should().BeNull();
        PlainSentence.IsValid(result.Claim.Explanations[0].WhyItMatters).Should().BeTrue();
        result.Claim.EvidenceMatrix.Select(entry => entry.Category).Distinct().Should().HaveCount(4);
        result.Claim.EvidenceMatrix.Should().OnlyContain(entry =>
            entry.EvidenceId == EvidenceSamples.Id(1)
            || entry.EvidenceId == EvidenceSamples.Id(2)
            || entry.EvidenceId == EvidenceSamples.Id(3));
    }

    [Fact]
    public void RejectsAnExplanationThatNamesAnUnknownOrUncitedId()
    {
        var unknown = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."],
            ["Review the change."],
            [],
            [new CitedEvidence(EvidenceSamples.Id(99), "This sentence is not attached to a real record.", null)]);
        var uncited = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."],
            ["Review the change."],
            [],
            [new CitedEvidence(EvidenceSamples.Id(2), "Maintenance in this window can move a sensor or change an alignment.", null)]);

        var unknownResult = GroundingValidator.Validate(unknown, SensorPack(), WhyItMattersCatalog.Empty);
        var uncitedResult = GroundingValidator.Validate(uncited, SensorPack(), WhyItMattersCatalog.Empty);

        unknownResult.Accepted.Should().BeFalse();
        unknownResult.Claim.Should().BeNull();
        unknownResult.Failures.Should().Contain("why-it-matters-unknown:" + EvidenceSamples.Id(99));
        uncitedResult.Accepted.Should().BeFalse();
        uncitedResult.Failures.Should().Contain("why-it-matters-not-cited:" + EvidenceSamples.Id(2));
    }

    [Fact]
    public void RejectsAMatrixRowThatCitesAnIdOutsideThePack()
    {
        var claim = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."],
            ["Review the change."],
            [],
            [],
            [new EvidenceMatrixEntry(RootCauseCategory.EngineeringChange, EvidenceSamples.Id(99), EvidenceStance.Supports, "An engineering change landed just before this signal moved, which can change the process result.")]);

        var result = GroundingValidator.Validate(claim, SensorPack(), WhyItMattersCatalog.Empty);

        result.Accepted.Should().BeFalse();
        result.Claim.Should().BeNull();
        result.Failures.Should().Contain("matrix-unknown-evidence-id:" + EvidenceSamples.Id(99));
    }

    [Fact]
    public void RepairsAnInvalidSentenceAndASignalIdOnTheWrongKindOfEvidence()
    {
        var badSentence = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."],
            ["Review the change."],
            [],
            [new CitedEvidence(EvidenceSamples.Id(1), "eco broke the ring.", null)]);
        var sensor = new ResolutionClaim(
            RootCauseCategory.ProcessDrift,
            "The signal left its window.",
            0.7d,
            [EvidenceSamples.Id(3)],
            ["No engineering change is the selected cause."],
            ["Recommend a chamber clean."],
            [],
            [new CitedEvidence(EvidenceSamples.Id(3), "Chuck temperature moved outside the recipe window, which can change etch depth.", null)]);
        var labeled = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C.",
            0.8d,
            [EvidenceSamples.Id(1)],
            ["Preventive maintenance the same week."],
            ["Review the change."],
            [],
            [new CitedEvidence(EvidenceSamples.Id(1), "An engineering change landed just before this signal moved, which can change the process result.", "esc-temp")]);

        var sentence = GroundingValidator.Validate(badSentence, SensorPack(), WhyItMattersCatalog.Empty);
        var missingSignal = GroundingValidator.Validate(sensor, SensorPack(), WhyItMattersCatalog.Empty);
        var extraSignal = GroundingValidator.Validate(labeled, SensorPack(), WhyItMattersCatalog.Empty);

        sentence.Accepted.Should().BeTrue();
        sentence.Repaired.Should().BeTrue();
        sentence.Claim!.Explanations[0].WhyItMatters.Should().NotContain("eco");
        PlainSentence.IsValid(sentence.Claim.Explanations[0].WhyItMatters).Should().BeTrue();
        missingSignal.Repaired.Should().BeTrue();
        missingSignal.Claim!.Explanations.Should().Contain(item => item.EvidenceId == EvidenceSamples.Id(3) && item.SignalId == "esc-temp");
        extraSignal.Repaired.Should().BeTrue();
        extraSignal.Claim!.Explanations[0].SignalId.Should().BeNull();
    }

    private static IReadOnlyList<EvidenceItem> Pack()
    {
        return [EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471")];
    }

    private static IReadOnlyList<EvidenceItem> SensorPack()
    {
        return
        [
            EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471"),
            EvidenceSamples.Item(2, EvidenceSource.Cmms, "Preventive maintenance the same week", "WO-19"),
            EvidenceSamples.Item(
                3,
                EvidenceSource.Telemetry,
                "esc_temp window",
                "telemetry:MPS-E7:esc_temp:2026-10-14T15:00:00.0000000+00:00"),
        ];
    }

    private static ResolutionClaim Claim(
        RootCauseCategory? category,
        string? cause,
        double? confidence,
        IReadOnlyList<EvidenceId> evidenceIds,
        IReadOnlyList<string>? counterEvidence = null,
        IReadOnlyList<string>? actions = null)
    {
        return new ResolutionClaim(category, cause, confidence, evidenceIds, counterEvidence ?? [], actions ?? [], []);
    }
}
