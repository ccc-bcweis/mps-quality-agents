// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Api.Demo;

/// <summary>One bench printout a scenario confirms. The file is under data/samples. Timing <c>now</c> stamps the plant clock.</summary>
public sealed record ScenarioPrintout(string FileName, string Timing);

/// <summary>Printout steps from <c>data/scenarios/catalog.yaml</c>. A scenario with none confirms nothing.</summary>
public sealed class ScenarioManualSampleCatalog
{
    public const string TimingNow = "now";

    private readonly Dictionary<string, ScenarioPrintout[]> _byScenario;

    private ScenarioManualSampleCatalog(Dictionary<string, ScenarioPrintout[]> byScenario)
    {
        _byScenario = byScenario;
    }

    public IReadOnlyList<ScenarioPrintout> For(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return [];
        }

        return _byScenario.TryGetValue(scenarioId.Trim(), out var steps) ? steps : [];
    }

    public static ScenarioManualSampleCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Scenario catalog was not found.", path);
        }

        var dto = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<CatalogFile>(File.ReadAllText(path));
        var byScenario = new Dictionary<string, ScenarioPrintout[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in dto?.Scenarios ?? [])
        {
            if (string.IsNullOrWhiteSpace(row.Id) || row.Printouts is null || row.Printouts.Count == 0)
            {
                continue;
            }

            var steps = new List<ScenarioPrintout>(row.Printouts.Count);
            foreach (var printout in row.Printouts)
            {
                if (string.IsNullOrWhiteSpace(printout.File))
                {
                    throw new InvalidDataException("Scenario " + row.Id + " has a printout with no file.");
                }

                var timing = string.IsNullOrWhiteSpace(printout.Timing) ? TimingNow : printout.Timing.Trim();
                if (!string.Equals(timing, TimingNow, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Scenario " + row.Id + " printout timing must be now.");
                }

                steps.Add(new ScenarioPrintout(printout.File.Trim(), TimingNow));
            }

            if (!byScenario.TryAdd(row.Id.Trim(), [.. steps]))
            {
                throw new InvalidDataException("Duplicate printout steps for scenario " + row.Id + ".");
            }
        }

        return new ScenarioManualSampleCatalog(byScenario);
    }

    private sealed class CatalogFile
    {
        public List<ScenarioRow>? Scenarios { get; set; }
    }

    private sealed class ScenarioRow
    {
        public string? Id { get; set; }

        public List<PrintoutRow>? Printouts { get; set; }
    }

    private sealed class PrintoutRow
    {
        public string? File { get; set; }

        public string? Timing { get; set; }
    }
}
