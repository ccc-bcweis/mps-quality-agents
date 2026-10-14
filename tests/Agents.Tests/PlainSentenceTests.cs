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
    [InlineData("The spc chart moved outside the window.", "acronym")]
    [InlineData("The ECO landed before the signal moved.", "acronym")]
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
}
