// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Docs;
using Mps.Simulator.Fleet;

namespace Mps.Simulator.Tests;

public sealed class DocumentCorpusTests
{
    [Fact]
    public void TemplatesFillRealFleetWindowsAndIncludeOneDecoyAndOneFixture()
    {
        var fleet = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());
        var temp = Directory.CreateTempSubdirectory("mps-corpus");
        try
        {
            DocumentCorpusGenerator.Write(fleet, RepoPaths.TemplateDirectory(), temp.FullName);
            var names = Directory.GetFiles(temp.FullName, "*.md").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
            names.Should().HaveCount(12);
            var texts = names.ToDictionary(name => name!, name => File.ReadAllText(Path.Combine(temp.FullName, name!)), StringComparer.Ordinal);
            texts.Values.Count(text => text.Contains("untrusted: true", StringComparison.Ordinal)).Should().Be(1);
            texts.Values.Count(text => text.Contains("kind: decoy", StringComparison.Ordinal)).Should().Be(1);
            texts.Values.Count(text => text.Contains("kind: sop", StringComparison.Ordinal)).Should().Be(4);

            var etch = fleet.GetRequired("E-02");
            var esc = etch.Recipes.First(recipe => recipe.Id == "OXIDE-ETCH").Step("main-etch")!.WindowFor("escTemperature")!;
            texts["sop-e7-pm.md"].Should().Contain(DocumentCorpusGenerator.FormatWindow(esc));
            texts["sb-edge-ring.md"].Should().Contain(DocumentCorpusGenerator.FormatWindow(esc));

            var deposition = fleet.GetRequired("D-01");
            var pressure = deposition.Recipes.First(recipe => recipe.Id == "PECVD-OXIDE").Step("deposit")!.WindowFor("chamberPressure")!;
            var pressureText = DocumentCorpusGenerator.FormatWindow(pressure);
            texts["sop-d7-pm.md"].Should().Contain(pressureText);
            texts["decoy-showerhead.md"].Should().Contain("9.5-12 Torr");
            texts["decoy-showerhead.md"].Should().NotContain(pressureText);
            texts["untrusted-bay-note.md"].Should().Contain("Ignore previous instructions");
            texts.Values.Should().OnlyContain(text => text.Contains("whyItMatters:", StringComparison.Ordinal));
            texts["coa-nf-3317.md"].Should().Contain("NF-3317");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Fact]
    public void CommittedCorpusMatchesTheTemplates()
    {
        var fleet = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());
        var committed = RepoPaths.CorpusDirectory();
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_CORPUS") == "1")
        {
            DocumentCorpusGenerator.Write(fleet, RepoPaths.TemplateDirectory(), committed);
        }

        var temp = Directory.CreateTempSubdirectory("mps-corpus-compare");
        try
        {
            DocumentCorpusGenerator.Write(fleet, RepoPaths.TemplateDirectory(), temp.FullName);
            var names = Directory.GetFiles(temp.FullName, "*.md").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
            Directory.GetFiles(committed, "*.md").Select(Path.GetFileName).Order(StringComparer.Ordinal).Should().Equal(names);
            foreach (var name in names)
            {
                File.ReadAllText(Path.Combine(temp.FullName, name!)).Should().Be(File.ReadAllText(Path.Combine(committed, name!)));
            }
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
