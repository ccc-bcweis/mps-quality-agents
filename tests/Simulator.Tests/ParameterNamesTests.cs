// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Mps.Domain.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class ParameterNamesTests
{
    [Fact]
    public void TheServerListMatchesTheConsoleList()
    {
        var path = Path.Combine(RepoPaths.FleetDirectory(), "..", "..", "web", "src", "sites", "parameters.ts");
        var text = File.ReadAllText(path);
        var block = text[text.IndexOf("PARAMETER_LABEL", StringComparison.Ordinal)..text.IndexOf("};", StringComparison.Ordinal)];
        var web = Regex.Matches(block, "^\\s+(\\w+): \"([^\"]+)\"", RegexOptions.Multiline)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);
        web.Should().NotBeEmpty();
        ParameterNames.All.Should().BeEquivalentTo(web);
    }

    [Fact]
    public void EveryFleetParameterHasADisplayName()
    {
        var catalog = Mps.Simulator.Fleet.FleetCatalogLoader.Load(RepoPaths.FleetDirectory());
        var parameters = catalog.Tools.SelectMany(tool => tool.Sensors).Select(sensor => sensor.Parameter).OfType<string>().Distinct();
        parameters.Should().OnlyContain(parameter => ParameterNames.All.ContainsKey(parameter));
        ParameterNames.Display("cf4Flow").Should().Be("CF4 flow");
    }
}
