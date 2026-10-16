// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;
using Mps.Simulator.Sites;

namespace Mps.Simulator.Tests;

public sealed class SitePlantTests
{
    private static readonly Mps.Application.Spc.SpcRuleSettings Rules = Mps.Application.Spc.SpcRuleSettings.Load(Path.Combine(RepoPaths.Root(), "data", "metadata", "spc-rules.yaml"));

    private static readonly FleetCatalog Fleet = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void ThreePlantsKeepTheOriginalEightToolsOnNorthwater()
    {
        var world = Load(SiteIds.DefaultSeed);
        world.Sites.Select(site => site.Id).Should().Equal("northwater", "ironwood", "lumen");
        world.Sites.Should().OnlyContain(site => site.Bays.Count >= 3 && site.Bays.Count <= 5);
        world.Sites.Should().OnlyContain(site => site.Tools.Count() >= 8 && site.Tools.Count() <= 16);
        world.Find("northwater")!.Tools.Should().HaveCount(12);
        world.Find("ironwood")!.Tools.Should().HaveCount(10);
        world.Find("lumen")!.Tools.Should().HaveCount(10);
        Fleet.Tools.Should().HaveCount(8);

        var original = world.FindTool("D-01");
        original!.SiteId.Should().Be(SiteIds.Northwater);
        original.BayId.Should().Be("Bay-1");
        original.Status.Should().Be("nominal");
        world.FindTool("D-03")!.Status.Should().Be("nominal");
        world.FindTool("I-D-01")!.Status.Should().Be("nominal");
        world.FindTool("I-E-01")!.Status.Should().Be("warning");
        world.FindTool("E-01")!.SiteId.Should().Be(SiteIds.Northwater);
        world.FindTool("P-S-01")!.SiteId.Should().Be(SiteIds.Lumen);
        world.FindTool("I-D-01")!.Spec.Sensors.Should().OnlyContain(sensor => sensor.Id.StartsWith("I-D-01", StringComparison.Ordinal));
        world.Sites.Should().OnlyContain(site => site.Name == SiteIds.NameFor(site.Id));
        world.Find("northwater")!.Flows.Should().NotBeEmpty();
        world.Find("lumen")!.Role.Should().Be(SiteIds.PilotRole);
    }

    [Fact]
    public void TheSameSeedRebuildsPositionsTelemetryAndThroughput()
    {
        var first = Load(SiteIds.DefaultSeed);
        var second = Load(SiteIds.DefaultSeed);
        var other = Load(SiteIds.DefaultSeed + 1);
        Positions(first).Should().Equal(Positions(second));
        Positions(first).Should().NotEqual(Positions(other));
        first.Sites.Select(site => site.Kpis.WaferStartsPerHour).Should().Equal(second.Sites.Select(site => site.Kpis.WaferStartsPerHour));
        ToolIds(first).Should().Equal(ToolIds(other));

        var etch = SiteComparison.Compare(first, ToolFamily.Etch, Rules.ControlLimitSigma, Rules.WatchSigma);
        var again = SiteComparison.Compare(second, ToolFamily.Etch, Rules.ControlLimitSigma, Rules.WatchSigma);
        etch.ReferenceSiteId.Should().Be(SiteIds.Northwater);
        Flagged(etch).Should().Equal(Flagged(again));
        Flagged(etch).Should().BeEquivalentTo("escTemperature@ironwood", "wallTemperature@ironwood");
        Flagged(SiteComparison.Compare(first, ToolFamily.Deposition, Rules.ControlLimitSigma, Rules.WatchSigma)).Should().BeEmpty();
        Flagged(SiteComparison.Compare(first, ToolFamily.Strip, Rules.ControlLimitSigma, Rules.WatchSigma)).Should().BeEmpty();
        Flagged(SiteComparison.Compare(first, ToolFamily.Clean, Rules.ControlLimitSigma, Rules.WatchSigma)).Should().BeEmpty();

        var esc = etch.Parameters.Should().ContainSingle(row => row.Parameter == "escTemperature").Subject;
        esc.Sites.Should().ContainSingle(row => row.SiteId == SiteIds.Northwater && !row.Discrepancy && row.Reason == null);
        var iron = esc.Sites.Should().ContainSingle(row => row.SiteId == SiteIds.Ironwood && row.Discrepancy).Subject;
        iron.Reason.Should().Contain("electrostatic chuck temperature");
        iron.Reason.Should().Contain("hotter");
        iron.Reason.Should().Contain("Ironwood Fab");
        iron.Reason.Should().Contain("Northwater Fab");
        iron.Reason.Should().Contain("spread is wider");
        iron.Reason.Should().Contain("alarm rate");
        iron.Reason.Should().Contain("Uptime and yield");
        iron.Mean.Should().BeGreaterThan(esc.Sites.Single(row => row.SiteId == SiteIds.Northwater).Mean);
        esc.Sites.Should().Contain(row => row.SiteId == SiteIds.Lumen && !row.Discrepancy);

        foreach (var site in first.Sites)
        {
            site.Tools.Should().OnlyContain(tool => tool.X >= 4 && tool.X <= 96 && tool.Y >= 4 && tool.Y <= 96);
            site.Kpis.WaferOutsPerHour.Should().BeLessThan(site.Kpis.WaferStartsPerHour);
            site.Kpis.Wip.Should().BeGreaterThan(0);
        }
    }

    private static SiteWorld Load(int seed) => SitePlantGenerator.Load(Path.Combine(RepoPaths.Root(), "data"), seed);

    private static IEnumerable<string> Positions(SiteWorld world)
    {
        return world.Sites.SelectMany(site => site.Tools).Select(tool => tool.Id + ":" + tool.X + "," + tool.Y);
    }

    private static IEnumerable<string> ToolIds(SiteWorld world)
    {
        return world.Sites.SelectMany(site => site.Tools).Select(tool => tool.Id);
    }

    private static IEnumerable<string> Flagged(FamilyComparison comparison)
    {
        return comparison.Parameters.SelectMany(parameter => parameter.Sites.Where(row => row.Discrepancy).Select(row => parameter.Parameter + "@" + row.SiteId));
    }
}
