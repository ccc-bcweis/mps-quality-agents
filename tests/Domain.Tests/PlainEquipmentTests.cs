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
        act.Should().Throw<DomainRuleException>().WithMessage("Evidence value must be finite.");

        var infinite = () => EvidenceReading.Place("E-02", "B", double.NegativeInfinity);
        infinite.Should().Throw<DomainRuleException>().WithMessage("Evidence value must be finite.");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("E-2", "Etch tool E-2")]
    [InlineData(" E-02 ", "Etch tool E-02")]
    [InlineData("E-001", "Etch tool E-001")]
    [InlineData("C-12", "Clean tool C-12")]
    [InlineData("E-0001", null)]
    [InlineData("E1", null)]
    [InlineData("E-", null)]
    [InlineData("EE-01", null)]
    [InlineData("X-01", null)]
    [InlineData("e-02", null)]
    [InlineData("E-0A", null)]
    public void ToolNamesAcceptAFamilyLetterAndOneToThreeDigits(string? toolId, string? name)
    {
        PlainEquipment.ToolName(toolId).Should().Be(name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("A", "Chamber A")]
    [InlineData("D", "Chamber D")]
    [InlineData("E", null)]
    [InlineData("a", null)]
    [InlineData("chamber a", null)]
    [InlineData("chamber A", "Chamber A")]
    [InlineData("CHAMBER D", "Chamber D")]
    [InlineData("Chamber E", null)]
    [InlineData("Chamber ", null)]
    [InlineData("1-A", "Chamber A")]
    [InlineData("12-D", "Chamber D")]
    [InlineData("1-E", null)]
    [InlineData("XAB", null)]
    [InlineData("-A", null)]
    [InlineData("AB", null)]
    [InlineData("C1", "Chamber A")]
    [InlineData("C2", "Chamber B")]
    [InlineData("C3", "Chamber C")]
    [InlineData("C4", "Chamber D")]
    [InlineData("C5", null)]
    [InlineData("C0", null)]
    [InlineData("c1", null)]
    [InlineData("D-01-A", "Chamber A")]
    [InlineData("D-01-E", null)]
    public void ChamberNamesUseTheLetterOrTheCleanToolIndex(string? chamberId, string? name)
    {
        PlainEquipment.ChamberName(chamberId).Should().Be(name);
    }
}
