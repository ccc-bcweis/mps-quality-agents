// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Domain.Evidence;

namespace Mps.Agents.Tests;

public sealed class WhyItMattersCatalogTests
{
    [Fact]
    public void AMissingDirectoryFallsBackToBuiltInSentences()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mps-why-missing-" + Guid.NewGuid().ToString("N"));
        var catalog = WhyItMattersCatalog.Load(missing);
        var record = EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471");
        var sensor = Sensor("esc_temp");

        catalog.ExplainItem(record).WhyItMatters.Should().Be(
            "An engineering change landed just before this signal moved, which can change the process result.");
        catalog.ExplainItem(record).SignalId.Should().BeNull();
        var explained = catalog.ExplainItem(sensor);
        PlainSentence.IsValid(explained.WhyItMatters).Should().BeTrue();
        explained.SignalId.Should().Be("esc-temp");
    }

    [Fact]
    public void AMatchingTemplateOverridesTheFallbackAndAnInvalidSentenceIsSkipped()
    {
        using var dir = TempData();
        File.WriteAllText(
            Path.Combine(dir.Path, "why-it-matters.yaml"),
            """
            templates:
              - whyItMatters: A source level sentence covers every telemetry point.
                source: telemetry
              - whyItMatters: A parameter level sentence covers this temperature point.
                source: telemetry
                parameter: esc_temp
                signalId: specific-esc
              - whyItMatters: not a sentence
                source: spc
                parameter: skipped_temp
              - whyItMatters: This sentence has no matching rule at all.
            """);

        var catalog = WhyItMattersCatalog.Load(dir.Path);
        var specific = catalog.ExplainItem(Sensor("esc_temp"));
        var broad = catalog.ExplainItem(Sensor("flow_rate"));
        var skipped = catalog.ExplainItem(EvidenceSamples.Item(
            4,
            EvidenceSource.Spc,
            "skipped_temp window",
            "spc:MPS-E7:skipped_temp:2026-10-14T15:00:00.0000000+00:00:rule"));
        var record = catalog.ExplainItem(EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471"));

        specific.WhyItMatters.Should().Be("A parameter level sentence covers this temperature point.");
        specific.SignalId.Should().Be("specific-esc");
        broad.WhyItMatters.Should().Be("A source level sentence covers every telemetry point.");
        skipped.WhyItMatters.Should().Be("This sensor's readings are part of the signal under investigation; open them to compare against the recipe spec.");
        skipped.SignalId.Should().Be("skipped-temp");
        record.WhyItMatters.Should().Be(
            "An engineering change landed just before this signal moved, which can change the process result.");
    }

    [Fact]
    public void AFleetSensorIdIsUsedWhenTheParameterMatches()
    {
        using var dir = TempData();
        var fleet = Path.Combine(dir.Path, "fleet");
        Directory.CreateDirectory(fleet);
        File.WriteAllText(
            Path.Combine(fleet, "mps-e7.yaml"),
            """
            tool:
              sensors:
                - id: fleet-esc
                  parameter: esc_temp
            """);

        var catalog = WhyItMattersCatalog.Load(dir.Path);

        catalog.SignalIdFor(Sensor("esc_temp")).Should().Be("fleet-esc");
        catalog.SignalIdFor(EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471")).Should().BeNull();
    }

    [Fact]
    public void UnreadableTemplateYamlDoesNotThrow()
    {
        using var dir = TempData();
        File.WriteAllText(Path.Combine(dir.Path, "why-it-matters.yaml"), "templates: [");

        var catalog = WhyItMattersCatalog.Load(dir.Path);
        var explained = catalog.ExplainItem(Sensor("esc_temp"));

        PlainSentence.IsValid(explained.WhyItMatters).Should().BeTrue();
        explained.SignalId.Should().Be("esc-temp");
    }

    [Fact]
    public void TheSharedCatalogExplainsASensorWithoutThrowing()
    {
        var explained = WhyItMattersCatalog.Shared.ExplainItem(Sensor("esc_temp"));

        PlainSentence.IsValid(explained.WhyItMatters).Should().BeTrue();
        explained.SignalId.Should().NotBeNullOrWhiteSpace();
    }

    private static EvidenceItem Sensor(string parameter)
    {
        return EvidenceSamples.Item(
            3,
            EvidenceSource.Telemetry,
            parameter + " window",
            "telemetry:MPS-E7:" + parameter + ":2026-10-14T15:00:00.0000000+00:00");
    }

    private static TempDirectory TempData()
    {
        return new TempDirectory(Directory.CreateTempSubdirectory("mps-why-"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory(DirectoryInfo directory)
        {
            Directory = directory;
        }

        public DirectoryInfo Directory { get; }

        public string Path => Directory.FullName;

        public void Dispose()
        {
            Directory.Delete(recursive: true);
        }
    }
}
