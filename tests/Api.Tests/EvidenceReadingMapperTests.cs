// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Http;
using Mps.Application.Evidence;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Api.Tests;

public sealed class EvidenceReadingMapperTests
{
    [Fact]
    public void MapperFillsUnitAndSpecFromTheFamilyCatalog()
    {
        var observed = new DateTimeOffset(2026, 10, 14, 21, 42, 0, TimeSpan.Zero);
        var evidenceId = new EvidenceId(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var item = new EvidenceItem(
            evidenceId,
            EvidenceSource.Spc,
            "westernElectric1 chamberPressure",
            "SPC rule westernElectric1 broke on D-01.",
            observed,
            "D-01",
            "spc:D-01:chamberPressure:2026-10-14T21:42:00.0000000+00:00:westernElectric1",
            EvidenceReading.Place("D-01", "A", 6.52));
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.RecommendationReady,
            [item],
            [],
            null,
            "corr-reading");

        var response = ContractMapper.ToResponse(snapshot, InvestigationNarrative.Empty);
        var reading = response.Evidence.Should().ContainSingle().Subject;

        reading.Value.Should().Be(6.52);
        reading.Unit.Should().Be("Torr");
        reading.SpecLow.Should().Be(2);
        reading.SpecHigh.Should().Be(6);
        reading.ToolName.Should().Be("Deposition tool D-01");
        reading.ChamberName.Should().Be("Chamber A");
        reading.Excerpt.Should().Contain("SPC rule");
    }

    [Fact]
    public void EtchTemperatureUsesTheEtchWindow()
    {
        var item = new EvidenceItem(
            EvidenceId.New(),
            EvidenceSource.Telemetry,
            "escTemperature window",
            "Series",
            DateTimeOffset.UnixEpoch,
            "E-02",
            "telemetry:E-02:escTemperature:2026-10-14T21:42:00.0000000+00:00",
            EvidenceReading.Place("E-02", "C2", 53.1));
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.EvidenceGathered,
            [item],
            [],
            null,
            "corr-etch");

        var reading = ContractMapper.ToResponse(snapshot).Evidence.Should().ContainSingle().Subject;

        reading.Unit.Should().Be("C");
        reading.SpecLow.Should().Be(40);
        reading.SpecHigh.Should().Be(60);
        reading.ToolName.Should().Be("Etch tool E-02");
        reading.ChamberName.Should().Be("Chamber B");
    }
}
