// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;

using Mps.Agents.Investigation;
using Mps.Application.Decisions;
using Mps.Application.Metadata;
using Mps.Application.Spc;
using Mps.Simulator;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

/// <summary>
/// One golden snapshot of every limit, threshold and label as each consumer reads it (not as the yaml spells it):
/// the rules engine and limit check, alert clearing, debounce and recovery watch, the simulator's schedule, noise,
/// fault pace and scripted faults, the site directory, the shape classifier, the model-call budget, shape wait,
/// evidence rounds, the override minimum, rule sentences, parameter names, Rules &amp; Process and the console catalog.
/// A move that silently changes any of them fails here. After a deliberate change, rerun with UPDATE_GOLDEN=1 and
/// review the diff of tests/Api.Tests/Golden/effective-limits.golden.json. Values were checked equal to the build
/// before #291 when this file was written (audit of Oct 17, 2026).
/// </summary>
public sealed class EffectiveLimitsGoldenTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Fact]
    public async Task EveryConsumerReadsTheSameValuesAsTheGoldenSnapshot()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var golden = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Put(string key, object? value) => golden[key] = Text(value);

        var spc = SpcRuleSettings.Shared;

        // Rules engine, limit check, clearing, debounce, recovery watch (as the code that runs reads them).
        Put("spc.controlLimitSigma", spc.ControlLimitSigma);
        Put("spc.watchSigma", spc.WatchSigma);
        Put("spc.criticalSigma", spc.CriticalSigma);
        Put("spc.we1", $"{spc.WesternElectric.Rule1.Count} of {spc.WesternElectric.Rule1.Window} beyond {Text(spc.WesternElectric.Rule1.Sigma)}");
        Put("spc.we2", $"{spc.WesternElectric.Rule2.Count} of {spc.WesternElectric.Rule2.Window} beyond {Text(spc.WesternElectric.Rule2.Sigma)}");
        Put("spc.we3", $"{spc.WesternElectric.Rule3.Count} of {spc.WesternElectric.Rule3.Window} beyond {Text(spc.WesternElectric.Rule3.Sigma)}");
        Put("spc.we4.count", spc.WesternElectric.Rule4.Count);
        Put("spc.nelsonRising.count", spc.NelsonRising.Count);
        Put("spc.limitCheck.sigma (SpcCoverage)", Mps.Api.Live.SpcCoverage.LimitSigma);
        Put("spc.limitCheck.readings (SpcCoverage)", Mps.Api.Live.SpcCoverage.RecentReadings);
        Put("spc.clearing", $"{spc.Clearing.Readings} readings within {Text(spc.Clearing.Sigma)} sigma");
        Put("spc.rule1.tier", spc.WesternElectric.Rule1.Tier);
        Put("spc.rule2.tier", spc.WesternElectric.Rule2.Tier);
        Put("spc.rule3.tier", spc.WesternElectric.Rule3.Tier);
        Put("spc.rule4.tier", spc.WesternElectric.Rule4.Tier);
        Put("spc.rule4.minMagnitudeSigma", spc.WesternElectric.Rule4.MinMagnitudeSigma);
        Put("spc.nelsonRising.tier", spc.NelsonRising.Tier);
        Put("spc.nelsonRising.minMagnitudeSigma", spc.NelsonRising.MinMagnitudeSigma);
        Put("spc.nelsonFalling", spc.NelsonFalling.Count + " " + spc.NelsonFalling.Tier + " min " + Text(spc.NelsonFalling.MinMagnitudeSigma));
        Put("spc.specWindow.tier", spc.SpecWindow.Tier);
        Put("spc.watchExpiry", spc.WatchExpiry.Readings + " readings or " + Text(spc.WatchExpiry.Minutes) + " minutes");
        Put("spc.ticketCap.perToolPerHour", spc.TicketCap.PerToolPerHour);
        Put("spc.sensors.enabledKinds", string.Join(',', spc.Sensors.EnabledKinds));
        using (var scope = factory.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
            Put("spc.debounce (SpcAlertService from DI)", typeof(SpcAlertService).GetField("_debounce", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(alerts));
        }

        // Simulator.
        Put("sim.recipe", $"step {RecipeSchedule.StepSeconds}s, wafer {RecipeSchedule.WaferSeconds}s, {RecipeSchedule.WafersPerLot} wafers per lot");
        Put("sim.noise.phi", Member(typeof(BaselineNoise).Assembly.GetType("Mps.Simulator.Telemetry.TelemetryMath")!, "Phi"));
        Put("sim.noise.scale", Member(typeof(BaselineNoise).Assembly.GetType("Mps.Simulator.Telemetry.TelemetryMath")!, "NoiseScale"));
        Put("sim.noise.excursion", $"chance {Text(BaselineNoise.ExcursionChance)}, {Text(BaselineNoise.ExcursionSigma)} sigma");
        Put("sim.realTimeFaultPace (PlantSimulation)", Mps.Api.Live.PlantSimulation.RealTimeFaultPace);
        Put("sim.siteComparison.spreadRatio", SiteComparison.SpreadRatio);
        var fleet = FleetCatalogLoader.Load(Path.Combine(Root(), "data", "fleet"));
        var origin = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var nominal = new StringBuilder();
        foreach (var tool in fleet.Tools)
        {
            foreach (var sensor in tool.Sensors.Where(sensor => sensor.Parameter is not null && sensor.ChamberId is not null))
            {
                nominal.Append(tool.Id).Append('/').Append(sensor.ChamberId).Append('/').Append(sensor.Parameter).Append(':')
                    .Append(Series(FleetTelemetry.Generate(tool, sensor.ChamberId!, sensor.Parameter!, Mps.Simulator.Demo.DemoDefaults.PlantSeed, origin, origin, origin.AddHours(2)))).Append('\n');
            }
        }

        Put("sim.nominalTelemetry.2h.sha256", Sha(nominal.ToString()));
        foreach (var scenario in ScriptedScenarioCatalog.All)
        {
            Put($"sim.scenario.{scenario.Id}.signals", JsonSerializer.Serialize(scenario.Signals(origin)));
            Put($"sim.scenario.{scenario.Id}.streamSignals", JsonSerializer.Serialize(scenario.StreamSignals(origin)));
        }

        // Sites.
        Put("sites", string.Join("; ", SiteIds.Directory.Sites.Select(site => $"{site.Id}={site.Name} prefix '{site.Prefix}'")));
        Put("sites.default", SiteIds.Default);

        // Investigation, guards and decisions.
        foreach (var name in new[] { "StepShare", "FlatShare", "RampShare", "SameDirectionShare", "NoisyStepSigmas", "NoisyStepFitShare", "NoisyStepSideDrift", "NoisyStepMinSide", "NoisyStepSmoothing", "NoisyStepEdgeReadings" })
        {
            Put("shape." + name + " (SignalShapeClassifier)", Member(typeof(Mps.Agents.Impact.SignalShapeClassifier), name));
        }

        Put("resolution.maxModelCalls (ResolutionAgent)", Mps.Agents.Resolution.ResolutionAgent.MaxModelCalls);
        Put("resolution.shapeRetries (MafInvestigationWorkflow)", Mps.Orchestration.MafInvestigationWorkflow.ShapeRetries);
        var wait = ShapeWait.Load();
        Put("shapeWait", $"{wait.Attempts} tries, {Text(wait.Delay.TotalSeconds)}s apart, from {wait.MinReadingsSinceOnset} to {wait.MaxReadingsSinceOnset} readings");
        Put("decision.overrideMinChars", DecisionSettings.Shared.OverrideMinChars);
        Put("decision.maxEvidenceRounds", EvidenceRoundSettings.Shared.MaxRounds);
        Put("decision.maxEvidenceChars", Mps.Domain.Investigations.InvestigationRounds.MaxEvidenceLength);

        // Server text: rule sentences and parameter names that land in evidence, prompts, tickets and alerts.
        foreach (var rule in SpcRuleText.RuleIds.Append("western-electric-1+western-electric-2+western-electric-3+western-electric-4+nelson-6-rising"))
        {
            Put("text.rule." + rule, SpcRuleNames.Plain(rule));
        }

        foreach (var (id, label) in ParameterNames.All.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Put("text.parameter." + id, label);
        }

        // What the console and Rules & Process get.
        Put("api.metadata.catalog", Canonical(await client.GetStringAsync("/api/metadata/catalog")));
        Put("api.metadata.settings", Canonical(await client.GetStringAsync("/api/metadata/settings")));

        var path = Path.Combine(Root(), "tests", "Api.Tests", "Golden", "effective-limits.golden.json");
        var json = JsonSerializer.Serialize(golden, Pretty) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            await File.WriteAllTextAsync(path, json);
        }

        File.Exists(path).Should().BeTrue("the golden snapshot is checked in");
        var expected = JsonSerializer.Deserialize<SortedDictionary<string, string>>(await File.ReadAllTextAsync(path))!;
        var changed = expected.Keys.Union(golden.Keys)
            .Where(key => !expected.TryGetValue(key, out var was) || !golden.TryGetValue(key, out var now) || was != now)
            .Select(key => $"{key}: golden '{Get(expected, key)}' now '{Get(golden, key)}'")
            .ToArray();
        changed.Should().BeEmpty("every effective limit and label must match the golden snapshot; after a deliberate change rerun with UPDATE_GOLDEN=1 and review the diff");
    }

    [Fact]
    public async Task EachConsumerEqualsTheCatalog()
    {
        await using var factory = new ApiFactory();
        var catalog = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/api/metadata/catalog")).RootElement;
        var limits = catalog.GetProperty("limits");
        var spc = SpcRuleSettings.Shared;

        Mps.Api.Live.SpcCoverage.LimitSigma.Should().Be(limits.GetProperty("controlLimitSigma").GetDouble(), "the limit check uses the catalog's control limit");
        Mps.Api.Live.SpcCoverage.RecentReadings.Should().Be(limits.GetProperty("limitCheckReadings").GetInt32());
        limits.GetProperty("zoneSigmas").EnumerateArray().Select(zone => zone.GetDouble()).Should()
            .Equal([spc.WesternElectric.Rule3.Sigma, spc.WesternElectric.Rule2.Sigma, spc.WesternElectric.Rule1.Sigma], "the chart zones are the rule sigmas");
        limits.GetProperty("zoneSigmas")[2].GetDouble().Should().Be(spc.ControlLimitSigma, "the chart UCL sits on the control limit the rules check");
        spc.WesternElectric.Rule1.Tier.Should().Be("alert");
        spc.WesternElectric.Rule4.Tier.Should().Be("watch");
        spc.WesternElectric.Rule4.MinMagnitudeSigma.Should().BeGreaterThan(0);
        spc.WatchExpiry.Readings.Should().BeGreaterThan(0);
        spc.TicketCap.PerToolPerHour.Should().BeGreaterThan(0);
        using (var scope = factory.Services.CreateScope())
        {
            var alerts = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
            typeof(SpcAlertService).GetField("_debounce", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(alerts).Should().Be(spc.AlertDebounce);
        }

        Mps.Api.Live.PlantSimulation.RealTimeFaultPace.Should().Be(SimulationSettings.Shared.RealTimeFaultPace);
        Mps.Agents.Resolution.ResolutionAgent.MaxModelCalls.Should().Be(InvestigationTuning.Shared.Resolution.MaxModelCalls);
        Mps.Orchestration.MafInvestigationWorkflow.ShapeRetries.Should().Be(InvestigationTuning.Shared.Resolution.ShapeRetries);
        limits.GetProperty("overrideMinChars").GetInt32().Should().Be(DecisionSettings.Shared.OverrideMinChars);
        limits.GetProperty("maxEvidenceRounds").GetInt32().Should().Be(EvidenceRoundSettings.Shared.MaxRounds);
        foreach (var rule in catalog.GetProperty("rules").EnumerateArray())
        {
            SpcRuleNames.Plain(rule.GetProperty("id").GetString()!).Should().Be(rule.GetProperty("plain").GetString(), "server text and the console use one sentence per rule");
        }

        foreach (var parameter in catalog.GetProperty("parameters").EnumerateArray())
        {
            ParameterNames.Display(parameter.GetProperty("id").GetString()!).Should().Be(parameter.GetProperty("label").GetString());
        }

        foreach (var site in catalog.GetProperty("sites").EnumerateArray())
        {
            SiteIds.NameFor(site.GetProperty("id").GetString()!).Should().Be(site.GetProperty("name").GetString());
        }
    }

    private static object? Member(Type type, string name)
    {
        var field = type.GetField(name, Static);
        if (field is not null)
        {
            return field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
        }

        var property = type.GetProperty(name, Static) ?? throw new InvalidOperationException($"{type.Name}.{name} is gone; update the golden test to read the new consumer.");
        return property.GetValue(null);
    }

    private static string Text(object? value) => value switch
    {
        null => "null",
        double number => number.ToString("R", Invariant),
        TimeSpan span => span.TotalSeconds.ToString("R", Invariant) + "s",
        IFormattable formattable => formattable.ToString(null, Invariant),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Series(IReadOnlyList<Mps.Domain.Telemetry.TelemetryPoint> points) =>
        Sha(string.Join(";", points.Select(point => point.At.ToUnixTimeMilliseconds().ToString(Invariant) + "=" + point.Value.ToString("R", Invariant))))[..16];

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Canonical(string json) => JsonNode.Parse(json)!.ToJsonString(Compact);

    private static string Get(SortedDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : "<missing>";

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MPS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
