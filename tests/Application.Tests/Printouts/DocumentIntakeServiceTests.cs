// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Printouts;
using Mps.Application.Spc;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Tests.Printouts;

public sealed class DocumentIntakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 22, 0, TimeSpan.Zero);

    [Fact]
    public async Task OutOfSpecOscilloscopeStoresEvidenceAndInjectsTheEtchSourceChannel()
    {
        var fixture = new Fixture();
        var reading = new PrintoutReading(
            "E-01",
            "E-01-A",
            Now,
            PrintoutReading.Oscilloscope,
            "rfPower",
            2140d,
            "W",
            1000d,
            2000d,
            "oscillation",
            "KL");

        var confirmed = await fixture.Service.ConfirmAsync(reading, CancellationToken.None);

        confirmed.OutOfSpec.Should().BeTrue();
        confirmed.PrintedParameter.Should().Be("rfPower");
        confirmed.CatalogParameter.Should().Be("rfSourcePower");
        confirmed.FaultId.Should().NotBeNull();
        confirmed.AppliedToSimulation.Should().BeTrue();
        confirmed.AlertId.Should().NotBeNull();
        fixture.Measurements.ForTool("E-01").Should().ContainSingle();
        fixture.Measurements.ForTool("E-01")[0].Evidence.Source.Should().Be(EvidenceSource.ManualMeasurement);
        fixture.Measurements.ForTool("E-01")[0].IncidentId.Should().Be(confirmed.IncidentId);
        fixture.Incidents.Incidents.Should().ContainSingle();
        fixture.Incidents.Incidents[0].Incident.Channel.Should().Be(IncidentChannel.OperatorReport);
        fixture.Incidents.Incidents[0].Incident.Summary.Should().StartWith("Manual measurement");
        fixture.Incidents.Incidents[0].Incident.Parameter.Should().Be("rfSourcePower");
        fixture.Incidents.Incidents[0].Incident.Severity.Should().Be(Severity.High);
        fixture.Faults.Records.Should().ContainSingle();
        fixture.Faults.Records[0].Parameter.Should().Be("rfSourcePower");
        fixture.Faults.Records[0].Shape.Should().Be("oscillation");
        fixture.Alerts.Alerts.Should().ContainSingle();
        fixture.Alerts.Alerts[0].Rule.Should().Be(ManualMeasurement.AlertRule);
        fixture.Alerts.Alerts[0].CorrelationId.Should().Be(fixture.Faults.Records[0].CorrelationId);
        fixture.Simulation.Calls.Should().ContainSingle();
        fixture.Simulation.Calls[0].Parameter.Should().Be("rfSourcePower");
        fixture.Simulation.Calls[0].Shape.Should().Be("oscillation");
    }

    [Fact]
    public async Task InSpecParticleCountStoresEvidenceAndDoesNotInject()
    {
        var fixture = new Fixture();
        var reading = new PrintoutReading(
            "D-01",
            "D-01-A",
            Now,
            PrintoutReading.ParticleCounter,
            "particlesAdders",
            3d,
            "count",
            0d,
            10d,
            null,
            "BN");

        var confirmed = await fixture.Service.ConfirmAsync(reading, CancellationToken.None);

        confirmed.OutOfSpec.Should().BeFalse();
        confirmed.FaultId.Should().BeNull();
        confirmed.AlertId.Should().BeNull();
        confirmed.AppliedToSimulation.Should().BeFalse();
        confirmed.CatalogParameter.Should().Be("particlesAdders");
        fixture.Measurements.ForTool("D-01").Should().ContainSingle();
        fixture.Incidents.Incidents.Should().ContainSingle();
        fixture.Incidents.Incidents[0].Incident.Severity.Should().Be(Severity.Low);
        fixture.Incidents.Incidents[0].Incident.Summary.Should().StartWith("Manual measurement");
        fixture.Faults.Records.Should().BeEmpty();
        fixture.Alerts.Alerts.Should().BeEmpty();
        fixture.Simulation.Calls.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Incidents = new InMemoryIncidentLog();
            Alerts = new InMemoryAlertLog();
            Faults = new InMemoryFaultLog();
            Measurements = new InMemoryManualMeasurementLog();
            Simulation = new RecordingPort();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(new ToolId("E-01"), "Etch 01", ToolFamily.Etch, "MPS-E7", "Bay-1", [new Chamber("A", "Chamber A")]));
            fleet.Add(new Tool(new ToolId("D-01"), "Dep 01", ToolFamily.Deposition, "MPS-D7", "Bay-3", [new Chamber("A", "Chamber A")]));
            var clock = new FixedClock(Now);
            var intake = new IntakeService(fleet, Incidents, new InMemoryTicketingPort(), NullQualityNotifier.Instance, clock);
            var faults = new FaultInjectionService(fleet, Faults, NullQualityNotifier.Instance, clock);
            Service = new DocumentIntakeService(
                new EmptyExtractor(),
                new EmptyParser(),
                new EmptyReader(),
                intake,
                faults,
                Alerts,
                NullQualityNotifier.Instance,
                Measurements,
                Simulation,
                clock);
        }

        public InMemoryIncidentLog Incidents { get; }

        public InMemoryAlertLog Alerts { get; }

        public InMemoryFaultLog Faults { get; }

        public InMemoryManualMeasurementLog Measurements { get; }

        public RecordingPort Simulation { get; }

        public DocumentIntakeService Service { get; }
    }

    private sealed class RecordingPort : ISimulationFaultPort
    {
        public List<(string ToolId, string? ChamberId, string Parameter, string Shape, double Magnitude)> Calls { get; } = [];

        public bool TryApply(string toolId, string? chamberId, string parameter, string shape, double magnitude)
        {
            Calls.Add((toolId, chamberId, parameter, shape, magnitude));
            return true;
        }
    }

    private sealed class InMemoryFaultLog : IFaultLog
    {
        public List<FaultRecord> Records { get; } = [];

        public Task AddAsync(FaultRecord fault, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(fault);
            _ = cancellationToken;
            Records.Add(fault);
            return Task.CompletedTask;
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
