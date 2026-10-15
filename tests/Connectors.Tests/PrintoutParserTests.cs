// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Printouts;
using Mps.Connectors.Printouts;

namespace Mps.Connectors.Tests;

public sealed class PrintoutParserTests
{
    private static readonly PdfTextExtractor Extractor = new();

    private static readonly LabeledPrintoutParser Parser = new();

    public static TheoryData<string, string, string, string, double, bool> Samples { get; } = new()
    {
        { "oscilloscope-e01.pdf", PrintoutReading.Oscilloscope, "E-01", "rfPower", 2140d, true },
        { "rf-power-meter-e02.pdf", PrintoutReading.RfPowerMeter, "E-02", "rfBiasPower", 510d, true },
        { "particle-counter-d01.pdf", PrintoutReading.ParticleCounter, "D-01", "particlesAdders", 3d, false },
        { "thermocouple-logger-e01.pdf", PrintoutReading.ThermocoupleLogger, "E-01", "escTemperature", 68d, true },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void EachSamplePdfParsesThePrintedReading(string fileName, string instrument, string toolId, string parameter, double value, bool outOfSpec)
    {
        var reading = Read(fileName);

        reading.Instrument.Should().Be(instrument);
        reading.ToolId.Should().Be(toolId);
        reading.Parameter.Should().Be(parameter);
        reading.Value.Should().Be(value);
        reading.ObservedAt.Should().NotBe(default);
        reading.Unit.Should().NotBeNullOrWhiteSpace();
        reading.SpecMinimum.Should().BeLessThanOrEqualTo(reading.SpecMaximum);
        reading.OperatorInitials.Should().NotBeNullOrWhiteSpace();
        reading.OutOfSpec.Should().Be(outOfSpec);
    }

    [Fact]
    public void OscilloscopePrintoutNamesTheRfOscillation()
    {
        var reading = Read("oscilloscope-e01.pdf");

        reading.ChamberId.Should().Be("E-01-A");
        reading.Shape.Should().Be("oscillation");
        reading.Unit.Should().Be("W");
        reading.SpecMinimum.Should().Be(1000d);
        reading.SpecMaximum.Should().Be(2000d);
        PrintoutReading.CatalogParameter(reading.ToolId, reading.Parameter).Should().Be("rfSourcePower");
    }

    [Fact]
    public void ThermocoupleScanHasNoTextLayer()
    {
        var bytes = File.ReadAllBytes(Path.Combine(SamplesDirectory(), "thermocouple-logger-e01.png"));

        Extractor.Extract(bytes, "image/png", "thermocouple-logger-e01.png").Trim().Should().BeEmpty();
    }

    private static PrintoutReading Read(string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(SamplesDirectory(), fileName));
        var text = Extractor.Extract(bytes, "application/pdf", fileName);
        Parser.TryParse(text, out var reading).Should().BeTrue("the printout {0} should carry a text layer", fileName);
        return reading;
    }

    private static string SamplesDirectory()
    {
        return Path.Combine(RepoPaths.Root(), "data", "samples");
    }
}
