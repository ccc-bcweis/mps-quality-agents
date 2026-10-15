// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Printouts;

namespace Mps.Api.Tests;

public sealed class PrintoutModelReaderPromptTests
{
    [Theory]
    [InlineData("toolId")]
    [InlineData("chamberId")]
    [InlineData("observedAt")]
    [InlineData("instrument")]
    [InlineData("parameter")]
    [InlineData("value")]
    [InlineData("unit")]
    [InlineData("specMinimum")]
    [InlineData("specMaximum")]
    [InlineData("thermocoupleLogger")]
    [InlineData("printout reader")]
    public void PromptNamesEveryFieldTheParserNeeds(string key)
    {
        Assert.Contains(key, PrintoutModelReader.SystemPrompt, StringComparison.Ordinal);
    }
}
