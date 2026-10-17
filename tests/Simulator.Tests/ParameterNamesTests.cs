// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;

namespace Mps.Simulator.Tests;

public sealed class ParameterNamesTests
{
    [Fact]
    public void EveryFleetParameterHasADisplayName()
    {
        var catalog = Mps.Simulator.Fleet.FleetCatalogLoader.Load(RepoPaths.FleetDirectory());
        var parameters = catalog.Tools.SelectMany(tool => tool.Sensors).Select(sensor => sensor.Parameter).OfType<string>().Distinct();
        parameters.Should().OnlyContain(parameter => ParameterNames.All.ContainsKey(parameter));
        ParameterNames.Display("cf4Flow").Should().Be("CF4 flow");
    }

    [Fact]
    public void NamesComeFromParametersYaml()
    {
        var path = Path.Combine(RepoPaths.FleetDirectory(), "..", "metadata", "parameters.yaml");
        var loaded = ParameterNames.Load(path);
        ParameterNames.All.Should().BeEquivalentTo(loaded);
        ParameterNames.Display("vibrationRms").Should().Be(loaded["vibrationRms"]);
    }

    [Fact]
    public void UnknownIdsReadAsWords() => ParameterNames.Display("someNewSignal").Should().Be("Some new signal");
}

public sealed class SiteDirectoryTests
{
    [Fact]
    public void ThePrefixRuleComesFromSitesYaml()
    {
        var layout = Mps.Simulator.Sites.SiteLayoutLoader.Load(Path.Combine(RepoPaths.FleetDirectory(), "sites.yaml"));
        var directory = Mps.Simulator.Sites.SiteIds.Directory;
        directory.Sites.Select(site => site.Id).Should().Equal(layout.Sites.Select(site => site.Id));
        foreach (var site in layout.Sites)
        {
            foreach (var tool in site.Bays.SelectMany(bay => bay.Tools))
            {
                directory.ForTool(tool.Id).Should().Be(site.Id, $"{tool.Id} sits in {site.Id}");
            }
        }

        directory.DefaultSiteId.Should().Be(layout.Sites.Single(site => site.ToolPrefix.Length == 0).Id);
    }

    [Fact]
    public void TheLongestPrefixWinsAndTheUnprefixedPlantTakesTheRest()
    {
        var directory = new Mps.Simulator.Sites.SiteDirectory([("home", "Home", ""), ("x", "X plant", "X-"), ("xy", "XY plant", "X-Y")]);
        directory.ForTool("E-01").Should().Be("home");
        directory.ForTool("X-E-01").Should().Be("x");
        directory.ForTool("X-YE-01").Should().Be("xy");
        directory.NameFor("x").Should().Be("X plant");
        directory.NameFor("nope").Should().Be("nope");
    }
}
