// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class EvidenceItemTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 21, 42, 0, TimeSpan.Zero);

    private static readonly EvidenceId Id = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    [Fact]
    public void AnItemTrimsTextDropsBlanksAndCopiesSourceEvents()
    {
        var events = new List<string> { "  evt-1  ", "evt-1", " ", "evt-2", "evt-2" };
        var reading = new EvidenceReading(31.5, "  C  ", 20, 40, "  Etch tool E-02  ", "  Chamber B  ", 30, 1);
        var item = new EvidenceItem(
            Id,
            EvidenceSource.Telemetry,
            "  ESC temperature  ",
            "  Series  ",
            At,
            "  E-02  ",
            "  telemetry:E-02  ",
            reading,
            events,
            "  bryan  ");

        item.Id.Should().Be(Id);
        item.Source.Should().Be(EvidenceSource.Telemetry);
        item.Title.Should().Be("ESC temperature");
        item.Excerpt.Should().Be("Series");
        item.ObservedAt.Should().Be(At);
        item.ToolId.Should().Be("E-02");
        item.ExternalRef.Should().Be("telemetry:E-02");
        item.SuppliedBy.Should().Be("bryan");
        item.SourceEventIds.Should().Equal("evt-1", "evt-2");
        item.Reading.Should().NotBeNull();
        item.Reading!.Value.Should().Be(31.5);
        item.Reading.Unit.Should().Be("C");
        item.Reading.ToolName.Should().Be("Etch tool E-02");
        item.Reading.ChamberName.Should().Be("Chamber B");
        item.Reading.SpecLow.Should().Be(20);
        item.Reading.SpecHigh.Should().Be(40);
        item.Reading.Center.Should().Be(30);
        item.Reading.Sigma.Should().Be(1);

        events.Add("evt-3");
        item.SourceEventIds.Should().Equal("evt-1", "evt-2");

        var replaced = item.WithSourceEventIds(["  evt-9  ", "evt-9", "evt-8"]);
        replaced.SourceEventIds.Should().Equal("evt-9", "evt-8");
        replaced.Title.Should().Be(item.Title);
        replaced.SuppliedBy.Should().Be("bryan");
        item.SourceEventIds.Should().Equal("evt-1", "evt-2");
    }

    [Fact]
    public void BlankOptionalTextAndAnEmptyReadingAreDropped()
    {
        var item = new EvidenceItem(
            Id,
            EvidenceSource.Document,
            "Note",
            null!,
            At,
            "   ",
            null,
            new EvidenceReading(null, "   ", null, null, "  ", " ", null, null),
            null,
            "   ");

        item.Excerpt.Should().BeEmpty();
        item.ToolId.Should().BeNull();
        item.ExternalRef.Should().BeNull();
        item.Reading.Should().BeNull();
        item.SuppliedBy.Should().BeNull();
        item.SourceEventIds.Should().BeEmpty();

        new EvidenceItem(Id, EvidenceSource.Mes, "Note", "   ", At, null, "  ", sourceEventIds: [])
            .Excerpt.Should().BeEmpty();
    }

    [Fact]
    public void UserProvidedEvidenceNamesWhoSuppliedIt()
    {
        var item = new EvidenceItem(Id, EvidenceSource.UserProvided, "Note", "text", At, "E-02", "USER-R1", suppliedBy: "  bryan  ");

        item.SuppliedBy.Should().Be("bryan");
        item.Source.Should().Be(EvidenceSource.UserProvided);

        var missing = () => new EvidenceItem(Id, EvidenceSource.UserProvided, "Note", "text", At, null, null, suppliedBy: "  ");
        missing.Should().Throw<DomainRuleException>().WithMessage("User-provided evidence names who supplied it.");
        var absent = () => new EvidenceItem(Id, EvidenceSource.UserProvided, "Note", "text", At, null, null, suppliedBy: null);
        absent.Should().Throw<DomainRuleException>().WithMessage("User-provided evidence names who supplied it.");
    }

    [Fact]
    public void AnItemRequiresAnIdAndATitle()
    {
        var missingId = () => new EvidenceItem(default, EvidenceSource.Spc, "Chart", "Point", At, "E-02", "E-01");
        missingId.Should().Throw<DomainRuleException>().WithMessage("Evidence id is required.");

        var missingTitle = () => new EvidenceItem(Id, EvidenceSource.Spc, "  ", "Point", At, "E-02", "E-01");
        missingTitle.Should().Throw<DomainRuleException>().WithMessage("Evidence title is required.");
        var nullTitle = () => new EvidenceItem(Id, EvidenceSource.Spc, null!, "Point", At, "E-02", "E-01");
        nullTitle.Should().Throw<DomainRuleException>().WithMessage("Evidence title is required.");
    }

    [Fact]
    public void AReadingMustBeFiniteAndTheSpecMustRunLowToHigh()
    {
        Rejects(new EvidenceReading(double.NaN, "C", 0, 1, null, null), "Evidence value must be finite.");
        Rejects(new EvidenceReading(double.PositiveInfinity, "C", 0, 1, null, null), "Evidence value must be finite.");
        Rejects(new EvidenceReading(double.NegativeInfinity, "C", 0, 1, null, null), "Evidence value must be finite.");
        Rejects(new EvidenceReading(1, "C", double.NaN, 2, null, null), "Evidence spec must be finite.");
        Rejects(new EvidenceReading(1, "C", double.PositiveInfinity, 2, null, null), "Evidence spec must be finite.");
        Rejects(new EvidenceReading(1, "C", 0, double.NaN, null, null), "Evidence spec must be finite.");
        Rejects(new EvidenceReading(1, "C", 0, double.NegativeInfinity, null, null), "Evidence spec must be finite.");

        var inverted = () => new EvidenceItem(Id, EvidenceSource.Telemetry, "Note", "Note", At, "E-02", null, new EvidenceReading(1, "C", 5, 4, null, null));
        inverted.Should().Throw<DomainRuleException>().WithMessage("Evidence spec low cannot exceed spec high.");

        var equal = new EvidenceItem(Id, EvidenceSource.Telemetry, "Note", "Note", At, "E-02", null, new EvidenceReading(5, "C", 5, 5, null, null));
        equal.Reading!.SpecLow.Should().Be(5);
        equal.Reading.SpecHigh.Should().Be(5);

        var zero = new EvidenceItem(Id, EvidenceSource.ManualMeasurement, "Note", "Note", At, "E-02", null, EvidenceReading.Place("E-02", "A", 0));
        zero.Reading!.Value.Should().Be(0);
        zero.Source.Should().Be(EvidenceSource.ManualMeasurement);
    }

    [Fact]
    public void PlaceNamesTheToolAndChamberAndRejectsANonFiniteValue()
    {
        var reading = EvidenceReading.Place("  S-02  ", "  D-01-C  ", -1.5);
        reading.Value.Should().Be(-1.5);
        reading.ToolName.Should().Be("Strip tool S-02");
        reading.ChamberName.Should().Be("Chamber C");
        reading.Unit.Should().BeNull();

        EvidenceReading.Place(null, null).IsEmpty.Should().BeTrue();
        EvidenceReading.Place("ECO-4471", "not-a-chamber", 1).ToolName.Should().BeNull();
        EvidenceReading.Place("ECO-4471", "not-a-chamber", 1).ChamberName.Should().BeNull();

        var nan = () => EvidenceReading.Place("E-02", "B", double.PositiveInfinity);
        nan.Should().Throw<DomainRuleException>().WithMessage("Evidence value must be finite.");
    }

    [Fact]
    public void AReadingIsEmptyOnlyWhenEveryFieldIsBlank()
    {
        new EvidenceReading(null, " ", null, null, " ", " ", null, null).IsEmpty.Should().BeTrue();
        new EvidenceReading(0, null, null, null, null, null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, "C", null, null, null, null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, 0, null, null, null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, null, 1, null, null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, null, null, "Etch tool E-02", null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, null, null, null, "Chamber A").IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, null, null, null, null, 10, null).IsEmpty.Should().BeFalse();
        new EvidenceReading(null, null, null, null, null, null, null, 0).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ControlLimitsAreCentrePlusOrMinusSigma()
    {
        var reading = new EvidenceReading(10, null, null, null, null, null, 10, 2);

        var limits = reading.ControlLimits(3);
        limits.Low.Should().Be(4);
        limits.High.Should().Be(16);

        var flat = reading.ControlLimits(0);
        flat.Low.Should().Be(10);
        flat.High.Should().Be(10);

        var swapped = reading.ControlLimits(-3);
        swapped.Low.Should().Be(16);
        swapped.High.Should().Be(4);

        var noCenter = new EvidenceReading(10, null, null, null, null, null, null, 2).ControlLimits(3);
        noCenter.Low.Should().BeNull();
        noCenter.High.Should().BeNull();

        var noSigma = new EvidenceReading(10, null, null, null, null, null, 10, null).ControlLimits(3);
        noSigma.Low.Should().BeNull();
        noSigma.High.Should().BeNull();
    }

    [Fact]
    public void OutOfSpecUsesTheRecipeBoundsAndTreatsTheEdgesAsInside()
    {
        OutOfSpec(5, null, null).Should().BeNull();
        OutOfSpec(5, 0, 10).Should().BeFalse();
        OutOfSpec(0, 0, 10).Should().BeFalse();
        OutOfSpec(10, 0, 10).Should().BeFalse();
        OutOfSpec(-0.1, 0, 10).Should().BeTrue();
        OutOfSpec(10.1, 0, 10).Should().BeTrue();
        OutOfSpec(5, 0, null).Should().BeFalse();
        OutOfSpec(-0.1, 0, null).Should().BeTrue();
        OutOfSpec(5, null, 10).Should().BeFalse();
        OutOfSpec(10.1, null, 10).Should().BeTrue();
    }

    private static bool? OutOfSpec(double? value, double? low, double? high)
    {
        return new EvidenceReading(value, "C", low, high, null, null).IsOutOfSpec;
    }

    [Fact]
    public void OutOfSpecIsUnknownWithoutAValueOrABound()
    {
        new EvidenceReading(null, "C", 0, 10, null, null).IsOutOfSpec.Should().BeNull();
        new EvidenceReading(5, "C", null, null, null, null).IsOutOfSpec.Should().BeNull();
    }

    private static void Rejects(EvidenceReading reading, string message = "Evidence value must be finite.")
    {
        var act = () => new EvidenceItem(Id, EvidenceSource.Telemetry, "Note", "Note", At, "E-02", null, reading);
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
