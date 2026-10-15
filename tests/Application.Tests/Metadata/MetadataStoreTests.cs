// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Json.Schema;

using Mps.Application.Metadata;
using Mps.Domain.Incidents;

using YamlDotNet.RepresentationModel;

namespace Mps.Application.Tests.Metadata;

public sealed class MetadataStoreTests
{
    private static readonly YamlMetadataStore Store = DemoRules.Metadata;

    [Fact]
    public void LoadIsCachedAndMeasured()
    {
        Store.LoadCount.Should().Be(1);
        Store.LoadDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        Store.Company.Name.Should().Be("Meridian Precision Systems");
        Store.Company.Industry.Should().Be("Semiconductor manufacturing");
        Store.Sites.Select(site => site.Name).Should().Equal("Northwater Fab", "Ironwood Fab", "Lumen Pilot Works");
        Store.Capabilities.Should().HaveCount(18);
        Store.Capabilities.Count(capability => capability.Simulated).Should().Be(3);
        Store.Lines.Should().HaveCount(15);
        Store.Tools.Should().HaveCount(33);
        Store.ProcessMaps.Select(map => map.State).Should().Equal("current", "future");
        _ = Store.Company;
        Store.LoadCount.Should().Be(1);
    }

    [Fact]
    public void SimulatedToolIdsMatchTheFleetRoster()
    {
        var fleet = File.ReadAllText(Path.Combine(Directory.GetParent(DemoRules.MetadataDirectory())!.FullName, "fleet", "sites.yaml"));
        var ids = Regex.Matches(fleet, @"\{\s*id:\s*([A-Za-z0-9-]+)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        ids.Should().HaveCount(32);
        Store.Tools.Where(tool => tool.Simulated).Select(tool => tool.Id).Should().BeEquivalentTo(ids);
        var bench = Store.FindTool("nw-film-check");
        bench.Should().NotBeNull();
        bench!.Simulated.Should().BeFalse();
        bench.LineId.Should().BeNull();
        bench.CapabilityId.Should().Be("nw-manufacturing");
    }

    [Fact]
    public void EtchLimitsAndLineOrderMatchTheCatalog()
    {
        var etch = Store.FindTool("E-01");
        etch.Should().NotBeNull();
        var temperature = etch!.Parameters.Single(parameter => parameter.Id == "escTemperature");
        temperature.Lcl.Should().Be(40);
        temperature.Ucl.Should().Be(60);
        temperature.AlertLow.Should().Be(42);
        temperature.AlertHigh.Should().Be(58);
        temperature.Unit.Should().Be("C");
        Store.ParameterClass("E-02", "escTemperature").Should().Be("temperature");
        Store.SiteIdForTool("I-D-01").Should().Be("ironwood");

        var flow = Store.Lines.Single(line => line.Id == "nw-end-to-end");
        flow.IncludesLineIds.Should().Equal("nw-strip", "nw-clean", "nw-deposition", "nw-etch");
        Store.Lines.Single(line => line.Id == "nw-deposition").ToolIds.Should().Equal("D-01", "D-02", "D-03", "D-04");
    }

    [Fact]
    public void AToolOnAMissingLineFailsTheCheck()
    {
        var catalog = new MetadataCatalog(
            new CompanyProfile("meridian", "Meridian Precision Systems", "Make wafers.", "Three plants.", "Semiconductor manufacturing"),
            [new SiteProfile("northwater", "Northwater Fab", "Run wafers.", "A plant.", "America/Denver")],
            [new CapabilityProfile("nw-manufacturing", "northwater", "Manufacturing", CapabilityType.Manufacturing, "Wafers.", true)],
            [new ProductionLine("nw-etch", "nw-manufacturing", "Northwater wafer etch", "wafer-etch", 4, "Plasma opens the film.", [], ["E-01"])],
            [
                new ToolProfile(
                    "E-01",
                    "Etch tool",
                    "northwater",
                    "missing-line",
                    null,
                    "Conductor etch",
                    true,
                    [new ToolPort("Patterned wafer", "A wafer.")],
                    [new ToolPort("Etched wafer", "An opened film.")],
                    [new ToolParameter("escTemperature", "Chuck temperature", "temperature", "C", true, 40, 60, 42, 58, 50)]),
            ],
            [
                new BusinessRule(
                    "rule-intake-default",
                    "Anything else",
                    900,
                    "When nothing earlier matches.",
                    "Open a ticket.",
                    "Every case still gets a ticket.",
                    new RuleWhen(),
                    [new RuleAction("open-ticket", null, null)]),
            ],
            [
                new ProcessMap("current", "Today", "current", [new ProcessStep("notice", "Someone notices the excursion", 1, null, [])], [new ProcessTransition("notice", "notice", "Then")]),
                new ProcessMap("future", "Next", "future", [new ProcessStep("rule", "A rule opens the ticket", 1, null, ["TicketOpened"])], [new ProcessTransition("rule", "rule", "Then")]),
            ]);

        var errors = MetadataIntegrity.Check(catalog);
        errors.Should().Contain(error => error.Contains("missing-line", StringComparison.Ordinal));
    }

    [Fact]
    public void AToolCannotHaveTwoParents()
    {
        var tool = new ToolProfile(
            "E-01",
            "Etch tool",
            "northwater",
            "nw-etch",
            "nw-manufacturing",
            "Conductor etch",
            true,
            [new ToolPort("Patterned wafer", "A wafer.")],
            [new ToolPort("Etched wafer", "An opened film.")],
            [new ToolParameter("escTemperature", "Chuck temperature", "temperature", "C", true, 40, 60, 42, 58, 50)]);
        var catalog = CatalogWith(tool, ["E-01"]);
        MetadataIntegrity.Check(catalog).Should().Contain(error => error.Contains("not both", StringComparison.Ordinal));
    }

    [Fact]
    public void MetadataFilesMatchTheirSchemas()
    {
        var root = Directory.GetParent(DemoRules.MetadataDirectory())!.Parent!.FullName;
        var contracts = Path.Combine(root, "contracts", "metadata");
        var data = DemoRules.MetadataDirectory();
        ExpectValid(contracts, "company.schema.json", Path.Combine(data, "company.yaml"));
        ExpectValid(contracts, "sites.schema.json", Path.Combine(data, "sites.yaml"));
        ExpectValid(contracts, "capabilities.schema.json", Path.Combine(data, "capabilities.yaml"));
        ExpectValid(contracts, "lines.schema.json", Path.Combine(data, "lines.yaml"));
        ExpectValid(contracts, "business-rules.schema.json", Path.Combine(data, "business-rules.yaml"));
        ExpectValid(contracts, "process-maps.schema.json", Path.Combine(data, "process-maps.yaml"));
        foreach (var file in Directory.GetFiles(Path.Combine(data, "tools"), "*.yaml"))
        {
            ExpectValid(contracts, "tools.schema.json", file);
        }
    }

    private static MetadataCatalog CatalogWith(ToolProfile tool, IReadOnlyList<string> lineTools)
    {
        return new MetadataCatalog(
            new CompanyProfile("meridian", "Meridian Precision Systems", "Make wafers.", "Three plants.", "Semiconductor manufacturing"),
            [new SiteProfile("northwater", "Northwater Fab", "Run wafers.", "A plant.", "America/Denver")],
            [new CapabilityProfile("nw-manufacturing", "northwater", "Manufacturing", CapabilityType.Manufacturing, "Wafers.", true)],
            [new ProductionLine("nw-etch", "nw-manufacturing", "Northwater wafer etch", "wafer-etch", 4, "Plasma opens the film.", [], lineTools)],
            [tool],
            [
                new BusinessRule(
                    "rule-intake-default",
                    "Anything else",
                    900,
                    "When nothing earlier matches.",
                    "Open a ticket.",
                    "Every case still gets a ticket.",
                    new RuleWhen(),
                    [new RuleAction("open-ticket", null, null)]),
            ],
            [
                new ProcessMap("current", "Today", "current", [new ProcessStep("notice", "Someone notices the excursion", 1, null, [])], [new ProcessTransition("notice", "notice", "Then")]),
                new ProcessMap("future", "Next", "future", [new ProcessStep("rule", "A rule opens the ticket", 1, null, ["TicketOpened"])], [new ProcessTransition("rule", "rule", "Then")]),
            ]);
    }

    private static void ExpectValid(string contracts, string schemaName, string yamlPath)
    {
        var schema = JsonSchema.FromFile(Path.Combine(contracts, schemaName));
        var node = YamlJson.Parse(File.ReadAllText(yamlPath));
        var result = schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        result.IsValid.Should().BeTrue(schemaName + " " + Path.GetFileName(yamlPath) + Environment.NewLine + Dump(result));
    }

    private static string Dump(EvaluationResults result)
    {
        var lines = new List<string>();
        Walk(result, lines);
        return string.Join(Environment.NewLine, lines);

        static void Walk(EvaluationResults node, List<string> lines)
        {
            if (node.Errors is not null)
            {
                foreach (var error in node.Errors)
                {
                    lines.Add(node.InstanceLocation + " " + error.Value);
                }
            }

            if (node.Details is null)
            {
                return;
            }

            foreach (var child in node.Details)
            {
                Walk(child, lines);
            }
        }
    }

    private static class YamlJson
    {
        public static JsonNode Parse(string yaml)
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            return Convert(stream.Documents[0].RootNode) ?? new JsonObject();
        }

        private static JsonNode? Convert(YamlNode node)
        {
            switch (node)
            {
                case YamlScalarNode scalar:
                    return Scalar(scalar.Value ?? string.Empty);
                case YamlSequenceNode sequence:
                    var array = new JsonArray();
                    foreach (var child in sequence.Children)
                    {
                        array.Add(Convert(child));
                    }

                    return array;
                case YamlMappingNode map:
                    var obj = new JsonObject();
                    foreach (var entry in map.Children)
                    {
                        var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                        obj[key] = Convert(entry.Value);
                    }

                    return obj;
                default:
                    return null;
            }
        }

        private static JsonValue? Scalar(string value)
        {
            if (string.Equals(value, "true", StringComparison.Ordinal))
            {
                return JsonValue.Create(true);
            }

            if (string.Equals(value, "false", StringComparison.Ordinal))
            {
                return JsonValue.Create(false);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                && !value.Contains('-', StringComparison.Ordinal))
            {
                return JsonValue.Create(integer);
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && value.Contains('.', StringComparison.Ordinal))
            {
                return JsonValue.Create(number);
            }

            return JsonValue.Create(value);
        }
    }
}

public sealed class BusinessRuleEngineTests
{
    [Fact]
    public void ActionsCannotCreateACorrectiveAction()
    {
        Enum.GetNames<RuleEffect>().Should().BeEquivalentTo(
            "OpenTicket",
            "SetPriority",
            "RouteToQueue",
            "EscalateToHuman",
            "AutoInvestigate");
        DemoRules.Metadata.Rules.SelectMany(rule => rule.Actions)
            .Should().NotContain(action => action.Type.Contains("capa", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("E-02", "escTemperature", "rule-sc1-edge-ring", "Process engineering")]
    [InlineData("E-01", "escTemperature", "rule-sc2-calibration", "Maintenance")]
    [InlineData("E-01", "cf4Flow", "rule-sc3-gas-lot", "Supplier quality")]
    [InlineData("E-02", "cf4Flow", "rule-sc3-gas-lot", "Supplier quality")]
    [InlineData("E-01", "etchRate", "rule-sc4-drift", "Process engineering")]
    [InlineData("D-03", "filmThickness", "rule-sc5-precursor", "Supplier quality")]
    [InlineData("I-D-01", "filmThickness", "rule-sc5-precursor", "Supplier quality")]
    [InlineData("E-01", "rfSourcePower", "rule-sc6-source-power", "Maintenance")]
    [InlineData("D-02", "vibrationRms", "rule-sc7-bearing", "Maintenance")]
    [InlineData("E-03", "rfBiasPower", "rule-sc8-recipe-edit", "Process engineering")]
    public void ScenarioAlertsOpenATicketWithoutAskingAPerson(string tool, string parameter, string ruleId, string queue)
    {
        var decision = Alert(tool, parameter, Severity.High, "western-electric-1");
        decision.RuleId.Should().Be(ruleId);
        decision.RuleName.Should().NotBeNullOrWhiteSpace();
        decision.OpensTicket.Should().BeTrue();
        decision.EscalatesToHuman.Should().BeFalse();
        decision.AutoInvestigates.Should().BeTrue();
        decision.Queue.Should().Be(queue);
        decision.Priority.Should().Be("high");
    }

    [Fact]
    public void ACriticalSignalStillEscalates()
    {
        var decision = Alert("E-02", "escTemperature", Severity.Critical, "western-electric-1");
        decision.RuleId.Should().Be("rule-critical-human");
        decision.OpensTicket.Should().BeTrue();
        decision.EscalatesToHuman.Should().BeTrue();
        decision.Queue.Should().Be("Shift lead");
        decision.Priority.Should().Be("critical");
    }

    [Fact]
    public void AMediumOrLowAlertDoesNotOpenATicket()
    {
        var decision = Alert("E-02", "rfHours", Severity.Low, "nelson-6-rising");
        decision.RuleId.Should().Be("rule-watch-signal");
        decision.OpensTicket.Should().BeFalse();
        decision.EscalatesToHuman.Should().BeFalse();
    }

    [Fact]
    public void AWindowBreachOnPressureOpensTheGenericHighRule()
    {
        var decision = Alert("E-02", "chamberPressure", Severity.High, "window");
        decision.RuleId.Should().Be("rule-high-signal");
        decision.OpensTicket.Should().BeTrue();
        decision.EscalatesToHuman.Should().BeFalse();
    }

    [Fact]
    public void AJoinedWesternElectricTokenStillMatches()
    {
        var decision = Alert("E-02", "chamberPressure", Severity.High, "window+western-electric-1");
        decision.RuleId.Should().Be("rule-western-electric-one");
        decision.OpensTicket.Should().BeTrue();
    }

    [Fact]
    public void RepeatsLoseToAScenarioRuleAndBeatTheGenericRule()
    {
        var scenario = Alert("E-02", "escTemperature", Severity.High, "western-electric-1", repeat: 5);
        scenario.RuleId.Should().Be("rule-sc1-edge-ring");

        var generic = Alert("E-02", "chamberPressure", Severity.High, "western-electric-1", repeat: 3);
        generic.RuleId.Should().Be("rule-repeat-signal");
        generic.OpensTicket.Should().BeTrue();
    }

    [Fact]
    public void IronwoodChuckTemperatureDoesNotStealNorthwater()
    {
        Alert("E-02", "escTemperature", Severity.High, "western-electric-1").RuleId.Should().Be("rule-sc1-edge-ring");
        Alert("I-E-01", "escTemperature", Severity.High, "western-electric-1").RuleId.Should().Be("rule-ironwood-chuck");
    }

    [Fact]
    public void EvidenceAndConfidenceEscalateWithoutOpeningATicket()
    {
        var conflict = Case(evidence: "contradictory");
        conflict.RuleId.Should().Be("rule-evidence-conflict");
        conflict.EscalatesToHuman.Should().BeTrue();
        conflict.OpensTicket.Should().BeFalse();

        var gap = Case(evidence: "insufficient");
        gap.RuleId.Should().Be("rule-evidence-gap");
        gap.EscalatesToHuman.Should().BeTrue();

        var unsure = Case(evidence: "sufficient", confidence: 0.49);
        unsure.RuleId.Should().Be("rule-low-confidence");
        unsure.EscalatesToHuman.Should().BeTrue();
        unsure.OpensTicket.Should().BeFalse();

        var enough = Case(evidence: "sufficient", confidence: 0.8);
        enough.RuleId.Should().Be("rule-evidence-enough");
        enough.AutoInvestigates.Should().BeTrue();
        enough.OpensTicket.Should().BeFalse();
        enough.EscalatesToHuman.Should().BeFalse();
    }

    [Fact]
    public void ChannelsKeepTodaysTicketAndEscalationOutcomes()
    {
        var nonconformance = Case(channel: IncidentChannel.QmsNonconformance.ToString(), severity: Severity.High);
        nonconformance.RuleId.Should().Be("rule-quality-nonconformance");
        nonconformance.OpensTicket.Should().BeTrue();
        nonconformance.EscalatesToHuman.Should().BeFalse();
        nonconformance.Queue.Should().Be("Quality review");

        var supplier = Case(channel: IncidentChannel.SupplierQualityNotice.ToString(), severity: Severity.High);
        supplier.Queue.Should().Be("Supplier quality");

        var scripted = Case(channel: IncidentChannel.FaultInjector.ToString(), severity: Severity.High);
        scripted.RuleId.Should().Be("rule-fault-script");
        scripted.EscalatesToHuman.Should().BeFalse();

        var report = Case(channel: IncidentChannel.OperatorReport.ToString(), severity: Severity.Medium, tool: "E-02", parameter: "escTemperature");
        report.RuleId.Should().Be("rule-operator-report");
        report.OpensTicket.Should().BeTrue();
        report.EscalatesToHuman.Should().BeFalse();

        var critical = Case(channel: IncidentChannel.OperatorReport.ToString(), severity: Severity.Critical, tool: "E-02", parameter: "escTemperature");
        critical.RuleId.Should().Be("rule-critical-human");
        critical.EscalatesToHuman.Should().BeTrue();
    }

    [Fact]
    public void NothingMatchesWhenTheCatalogHasNoCatchAll()
    {
        var store = new SingleRuleStore(new BusinessRule(
            "rule-only",
            "Only one",
            1,
            "When the tool is missing.",
            "Open a ticket.",
            "This rule does not match.",
            new RuleWhen { ToolId = "Z-99" },
            [new RuleAction("open-ticket", null, null)]));
        var act = () => new BusinessRuleEngine(store).Evaluate(new RuleContext { ToolId = "E-02", Severity = Severity.High });
        act.Should().Throw<MetadataSpecException>();
    }

    private static RuleDecision Alert(string tool, string parameter, Severity severity, string alert, int repeat = 0)
    {
        return DemoRules.Engine.Evaluate(new RuleContext
        {
            ToolId = tool,
            ParameterId = parameter,
            Severity = severity,
            AlertRuleId = alert,
            RepeatCount = repeat,
            Channel = IncidentChannel.IotSpcAlert.ToString(),
        });
    }

    private static RuleDecision Case(
        string? channel = null,
        Severity? severity = null,
        string? evidence = null,
        double? confidence = null,
        string? tool = null,
        string? parameter = null)
    {
        return DemoRules.Engine.Evaluate(new RuleContext
        {
            Channel = channel,
            Severity = severity,
            EvidenceSufficiency = evidence,
            Confidence = confidence,
            ToolId = tool,
            ParameterId = parameter,
        });
    }

    private sealed class SingleRuleStore : IMetadataStore
    {
        public SingleRuleStore(BusinessRule rule)
        {
            Rules = [rule];
        }

        public CompanyProfile Company => new("meridian", "Meridian Precision Systems", "Make wafers.", "Three plants.", "Semiconductor manufacturing");

        public IReadOnlyList<SiteProfile> Sites => [];

        public IReadOnlyList<CapabilityProfile> Capabilities => [];

        public IReadOnlyList<ProductionLine> Lines => [];

        public IReadOnlyList<ToolProfile> Tools => [];

        public IReadOnlyList<BusinessRule> Rules { get; }

        public IReadOnlyList<ProcessMap> ProcessMaps => [];

        public TimeSpan LoadDuration => TimeSpan.Zero;

        public int LoadCount => 1;

        public ToolProfile? FindTool(string toolId) => null;

        public string? ParameterClass(string toolId, string parameterId) => null;

        public string? SiteIdForTool(string toolId) => null;
    }
}
