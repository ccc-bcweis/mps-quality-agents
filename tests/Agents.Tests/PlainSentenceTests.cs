// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;

namespace Mps.Agents.Tests;

public sealed class PlainSentenceTests
{
    [Fact]
    public void AcceptsOnePlainSentence()
    {
        const string sentence = "Chuck temperature moved outside the recipe window, which can change etch depth.";

        PlainSentence.IsValid(sentence).Should().BeTrue();
        PlainSentence.TryProblem(sentence, out var problem).Should().BeTrue();
        problem.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "blank")]
    [InlineData("   ", "blank")]
    [InlineData("the ring moved.", "sentence-case")]
    [InlineData("The ring moved", "not-a-sentence")]
    [InlineData("The ring moved. The etch changed.", "multiple-sentences")]
    [InlineData("The XYZQ chart moved outside the window.", "acronym")]
    [InlineData("The QQ9 flow stepped down.", "acronym")]
    [InlineData("The change was XYZ-backed.", "acronym")]
    public void RejectsASentenceThatASupervisorCannotRead(string? text, string problem)
    {
        PlainSentence.IsValid(text).Should().BeFalse();
        PlainSentence.TryProblem(text, out var found).Should().BeFalse();
        found.Should().Be(problem);
    }

    [Fact]
    public void RejectsASentenceLongerThan240Characters()
    {
        var exact = "A" + new string('a', 238) + ".";
        var longer = "A" + new string('a', 239) + ".";

        exact.Length.Should().Be(PlainSentence.MaxLength);
        PlainSentence.IsValid(exact).Should().BeTrue();
        PlainSentence.TryProblem(longer, out var problem).Should().BeFalse();
        problem.Should().Be("too-long");
    }

    [Theory]
    [InlineData("The ECO landed before the signal moved.")]
    [InlineData("The spc chart moved outside the window.")]
    [InlineData("The change was ECO-backed.")]
    [InlineData("The SiH4 flow stayed inside its window.")]
    [InlineData("All checks passed and ESC loop calibration was in date.")]
    [InlineData("Recipe revision A is only a label and no ECO is shown.")]
    [InlineData("Liner revision label only; no ECO-backed change record.")]
    [InlineData("Wrong material family for the CF4 flow signal.")]
    public void AcceptsAGlossaryTerm(string sentence)
    {
        PlainSentence.IsValid(sentence).Should().BeTrue();
        PlainSentence.TryProblem(sentence, out var problem).Should().BeTrue();
        problem.Should().BeEmpty();
    }
}
