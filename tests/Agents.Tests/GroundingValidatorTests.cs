// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

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

    private static IReadOnlyList<EvidenceItem> Pack()
    {
        return [EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471")];
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
