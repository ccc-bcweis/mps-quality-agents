// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Printouts;
using Mps.Domain.Evidence;

namespace Mps.Application.Tests.Printouts;

public sealed class DocumentIntakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 22, 0, TimeSpan.Zero);

    [Fact]
    public async Task OutOfSpecPrintoutIsIngestedAsOneManualSampleWithNoTicketFaultOrAlertOfItsOwn()
    {
        var fixture = new Fixture();
        var reading = new PrintoutReading("E-01", "E-01-A", Now, PrintoutReading.Oscilloscope, "rfPower", 2140d, "W", 1000d, 2000d, "oscillation", "KL");

        var confirmed = await fixture.Service.ConfirmAsync(reading, CancellationToken.None);

        confirmed.OutOfSpec.Should().BeTrue();
        confirmed.CatalogParameter.Should().Be("rfSourcePower");
        confirmed.FaultId.Should().BeNull("a printout no longer injects a fault");
        confirmed.AlertId.Should().BeNull("the generic rules raise any alert from the sample");
        confirmed.IncidentId.Should().BeNull("the generic pipeline opens any ticket");
        confirmed.AppliedToSimulation.Should().BeTrue();
        var sample = fixture.Samples.Samples.Should().ContainSingle().Subject;
        sample.Parameter.Should().Be("rfSourcePower");
        sample.Value.Should().Be(2140d);
        sample.ObservedAt.Should().Be(Now);
        var measurement = fixture.Measurements.ForTool("E-01").Should().ContainSingle().Subject;
        measurement.Evidence.Source.Should().Be(EvidenceSource.ManualMeasurement);
        sample.EvidenceId.Should().Be(measurement.Evidence.Id.Value);
    }

    [Fact]
    public async Task InSpecPrintoutTakesTheSamePath()
    {
        var fixture = new Fixture();
        var reading = new PrintoutReading("D-01", "D-01-A", Now, PrintoutReading.ParticleCounter, "particlesAdders", 3d, "count", 0d, 10d, null, "BN");

        var confirmed = await fixture.Service.ConfirmAsync(reading, CancellationToken.None);

        confirmed.OutOfSpec.Should().BeFalse();
        confirmed.IncidentId.Should().BeNull();
        fixture.Samples.Samples.Should().ContainSingle().Which.Value.Should().Be(3d);
        fixture.Measurements.ForTool("D-01").Should().ContainSingle();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Measurements = new InMemoryManualMeasurementLog();
            Samples = new RecordingSink();
            Service = new DocumentIntakeService(
                new EmptyExtractor(),
                new EmptyParser(),
                new EmptyReader(),
                Measurements,
                Samples,
                new FixedClock(Now));
        }

        public InMemoryManualMeasurementLog Measurements { get; }

        public RecordingSink Samples { get; }

        public DocumentIntakeService Service { get; }
    }

    private sealed class RecordingSink : IManualSampleSink
    {
        public List<ManualSample> Samples { get; } = [];

        public bool Ingest(ManualSample sample)
        {
            Samples.Add(sample);
            return true;
        }
    }

    private sealed class EmptyExtractor : IPrintoutTextExtractor
    {
        public string Extract(ReadOnlyMemory<byte> bytes, string? mediaType, string? fileName) => string.Empty;
    }

    private sealed class EmptyParser : IPrintoutFieldParser
    {
        public bool TryParse(string text, out PrintoutReading reading)
        {
            reading = null!;
            return false;
        }
    }

    private sealed class EmptyReader : IPrintoutModelReader
    {
        public Task<PrintoutReading?> ReadAsync(ReadOnlyMemory<byte> bytes, string? mediaType, string? fileName, CancellationToken cancellationToken)
        {
            _ = bytes;
            _ = mediaType;
            _ = fileName;
            _ = cancellationToken;
            return Task.FromResult<PrintoutReading?>(null);
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
