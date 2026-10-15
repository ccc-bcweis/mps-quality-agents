// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;
using Mps.Domain.Identity;

using DomainRuleException = Mps.Domain.DomainRuleException;

namespace Mps.Domain.Tests;

public sealed class PlainEquipmentTests
{
    [Fact]
    public void NamesToolsAndChambersInPlainLanguage()
    {
        PlainEquipment.ToolName("E-02").Should().Be("Etch tool E-02");
        PlainEquipment.ToolName("D-01").Should().Be("Deposition tool D-01");
        PlainEquipment.ToolName("S-02").Should().Be("Strip tool S-02");
        PlainEquipment.ToolName("C-01").Should().Be("Clean tool C-01");
        PlainEquipment.ToolName("ECO-4471").Should().BeNull();
        PlainEquipment.ToolName("MPS-E7").Should().BeNull();

        PlainEquipment.ChamberName("B").Should().Be("Chamber B");
        PlainEquipment.ChamberName("D-01-A").Should().Be("Chamber A");
        PlainEquipment.ChamberName("C2").Should().Be("Chamber B");
        PlainEquipment.ChamberName("Chamber A").Should().Be("Chamber A");
        PlainEquipment.ChamberName("not-a-chamber").Should().BeNull();
    }

    [Fact]
    public void ReadingKeepsAFiniteValueAndDropsAnEmptyPlace()
    {
        var reading = EvidenceReading.Place("E-02", "B", 31.4);
        var item = new EvidenceItem(
            new EvidenceId(Guid.Parse("11111111-1111-4111-8111-111111111111")),
            EvidenceSource.Telemetry,
            "ESC temperature window",
            "Series",
            new DateTimeOffset(2026, 10, 14, 21, 42, 0, TimeSpan.Zero),
            "E-02",
            "telemetry:E-02:escTemperature:2026-10-14T21:42:00.0000000+00:00",
            reading);

        item.Reading.Should().NotBeNull();
        item.Reading!.Value.Should().Be(31.4);
        item.Reading.ToolName.Should().Be("Etch tool E-02");
        item.Reading.ChamberName.Should().Be("Chamber B");

        var blank = new EvidenceItem(
            EvidenceId.New(),
            EvidenceSource.Document,
            "Note",
            "Note",
            DateTimeOffset.UnixEpoch,
            null,
            null,
            EvidenceReading.Place(null, null));
        blank.Reading.Should().BeNull();
    }

    [Fact]
    public void ANonFiniteReadingIsRejected()
    {
        var act = () => EvidenceReading.Place("E-02", "B", double.NaN);
        act.Should().Throw<DomainRuleException>();
    }
}
