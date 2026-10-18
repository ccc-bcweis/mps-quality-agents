// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Prompts;
using Mps.Application.Wording;
using Mps.Domain;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

/// <summary>The plain-sentence rule and the live ruled-out reasons from issue 389.</summary>
public sealed class PlainWordingTests
{
    [Fact]
    public void TheResolutionPromptStatesTheRuleFromTheDataFile()
    {
        var rule = PlainWording.Load(null);
        var yaml = File.ReadAllText(DataFiles.Find(PlainWording.RelativePath)!);
        var prompt = PromptCatalog.Load().Resolution;

        yaml.Should().Contain("maxLength: " + rule.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        yaml.Should().Contain("oneSentence: true");
        yaml.Should().Contain("sentenceCase: true");
        yaml.Should().Contain("allowedTerms: " + rule.AllowedTerms);
        rule.OneSentence.Should().BeTrue();
        rule.SentenceCase.Should().BeTrue();
        rule.PromptClause.Should().Contain(rule.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
        rule.PromptClause.Should().Contain("one sentence");
        rule.PromptClause.Should().Contain("sentence case");
        rule.PromptClause.Should().Contain(rule.AllowedTerms);
        prompt.Version.Should().Be("1.14.0");
        prompt.Body.Should().Contain(rule.PromptClause);
        prompt.Body.Should().NotContain(PlainWording.PromptMarker);
        PromptCatalog.Load().Investigation.Body.Should().Contain(rule.PromptClause);
    }

    [Theory]
    [InlineData("Routine preventive maintenance with no fault or deferred action.")]
    [InlineData("Supplier notice says the silane lot matches specification.")]
    [InlineData("Material lot has no defect notice tied to the affected chamber.")]
    [InlineData("No supplier issue or lot defect is tied to this chamber.")]
    [InlineData("Calibration was completed in date, so it is not overdue.")]
    [InlineData("Revision label alone is not a change record.")]
    [InlineData("Recipe revision A is only a label and no ECO is shown.")]
    [InlineData("Scheduled preventive maintenance only; no fault or overdue condition.")]
    [InlineData("All checks passed and ESC loop calibration was in date.")]
    [InlineData("Calibration was completed and in date, not overdue.")]
    [InlineData("Material lot has no supplier notice.")]
    [InlineData("Configuration label only; it is not the change record.")]
    [InlineData("Run record only; it does not explain the onset.")]
    [InlineData("Liner revision label only; no ECO-backed change record.")]
    [InlineData("Recipe revision label only; no engineering change order.")]
    [InlineData("No supplier notice and no affected-tool linkage.")]
    [InlineData("Wrong material family for the CF4 flow signal.")]
    [InlineData("Chamber clean is a confounder, not an overdue calibration or alignment issue.")]
    [InlineData("Routine preventive maintenance does not explain the signal.")]
    [InlineData("Calibration was completed in date, so calibration class does not apply.")]
    public void LiveRuledOutReasonsArePlainSentences(string reason)
    {
        PlainSentence.IsValid(reason).Should().BeTrue(PlainSentence.TryProblem(reason, out var problem) ? string.Empty : problem);
    }

    [Fact]
    public void TheSc1AnswerThatListsThePmRecordTwiceStillEscalatesAsRuledOutAndCited()
    {
        var pm = new EvidenceId(Guid.Parse("71454076-cbc5-592e-8f1a-8fed4a6d1bda"));
        var eco = new EvidenceId(Guid.Parse("87d7c0bc-2553-5eed-baea-b3458a4fc842"));
        var signal = new EvidenceId(Guid.Parse("f411787c-ab0d-5fe6-910b-7b2ad4f522d4"));
        const string reason = "All checks passed and ESC loop calibration was in date.";
        var at = new DateTimeOffset(2026, 10, 16, 18, 24, 0, TimeSpan.Zero);
        var pack = new[]
        {
            new EvidenceItem(pm, EvidenceSource.Cmms, "Scheduled chamber PM", "All PM checks passed.", at, "E-02", "WO-PM-8821"),
            new EvidenceItem(eco, EvidenceSource.Plm, "Edge ring revision C", "Replace the chamber A edge ring with revision C.", at, "E-02", "ECO-4471"),
            new EvidenceItem(signal, EvidenceSource.Telemetry, "ESC temperature readings", "The reading swings up and down.", at, "E-02", "telemetry:E-02:escTemperature"),
        };
        var claim = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C changed the chamber.",
            0.8d,
            [eco, signal, pm],
            ["Equipment is ruled out."],
            ["Review the change."],
            [],
            null,
            null,
            "oscillation",
            [new RuledOutClaim(pm, reason)]);

        var result = GroundingValidator.Validate(claim, pack);

        result.Accepted.Should().BeFalse();
        result.Failures.Should().Contain("ruled-out-and-cited:" + pm.Value.ToString("D"));
        result.Failures.Should().NotContain("ruled-out-reason:" + pm.Value.ToString("D"));
    }
}
