// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Hypotheses;

namespace Mps.Agents.Tests;

public sealed class CauseGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 21, 0, 0, TimeSpan.Zero);

    private static EvidencePack Pack(SignalShape shape, params SupplierNotice[] notices) =>
        new([], [], [], [], [], [], notices, true, shape);

    private static ResolutionClaim Claim(string cause, string? shape) =>
        new(RootCauseCategory.EngineeringChange, cause, 0.9, [], [], [], [], null, null, shape);

    [Fact]
    public void ContrastingProseWithTheRightShapeFieldPasses()
    {
        CauseGuard.Check(Claim("CF4 flow on E-02-A shows a step rather than a gradual drift, after lot CF4-221 arrived.", "step"), Pack(SignalShape.Step))
            .Should().BeNull("the check compares the structured shape field, not words in the prose");
    }

    [Fact]
    public void AReportedShapeThatIsNotTheMeasuredShapeEscalates()
    {
        CauseGuard.Check(Claim("CF4 flow on E-02-A changed after lot CF4-221 arrived.", "ramp"), Pack(SignalShape.Step))
            .Should().Contain("gradual drift").And.Contain("a step");
    }

    [Fact]
    public void AMissingOrUnknownShapeIdEscalates()
    {
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", null), Pack(SignalShape.Oscillation))
            .Should().Contain("did not report the signal shape");
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", "wobble"), Pack(SignalShape.Oscillation))
            .Should().Contain("not a known shape");
    }

    [Fact]
    public void AShapeTheReadingsDoNotShowYetEscalatesWithAWayForward()
    {
        CauseGuard.Check(Claim("The chamber pressure moved after ECO-4471.", "step"), Pack(SignalShape.Unknown))
            .Should().Contain("Request more evidence");
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", "unknown"), Pack(SignalShape.Unknown)).Should().BeNull();
    }

    [Fact]
    public void ARuledOutRecordCannotBeTheCause()
    {
        var notice = new SupplierNotice("SN-SIH4-088", "Cinderwell", "GAS-SIH4", "SIH4-088", "Silane lot SIH4-088 matches the purchase specification.", Now);
        CauseGuard.Check(Claim("Silane lot SIH4-088 is the cause.", "oscillation"), Pack(SignalShape.Oscillation, notice)).Should().Contain("SIH4-088");
    }

    [Fact]
    public void AMatchingShapeAndARealRecordPass()
    {
        CauseGuard.Check(Claim("The oscillation started after ECO-4471 changed the edge ring.", "oscillation"), Pack(SignalShape.Oscillation)).Should().BeNull();
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring; process drift is ruled out.", "OSCILLATION"), Pack(SignalShape.Oscillation)).Should().BeNull();
    }

    [Fact]
    public void TheRetryNoteNamesTheMeasuredShapeIdAndTheWordsToAvoidFromTheCatalog()
    {
        var note = CauseGuard.ShapeFeedback("ramp", Pack(SignalShape.Step));
        note.Should().Contain("Set shape to \"step\"");
        note.Should().Contain("gradual").And.Contain("drift").And.Contain("not even to say the signal is not like that");
        note.Should().NotContain("stepped");
    }

    [Fact]
    public void TheShapeCatalogComesFromMetadataAndCoversEveryMeasuredShape()
    {
        var catalog = SignalShapeCatalog.Load();
        catalog.Ids.Should().BeEquivalentTo(["step", "ramp", "oscillation", "intermittent", "unknown"]);
        foreach (var shape in Enum.GetValues<SignalShape>())
        {
            catalog.Of(shape).Description.Should().NotBeNullOrWhiteSpace();
        }

        var bad = Path.Combine(Path.GetTempPath(), "shapes-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(bad, "shapes:\n  - id: wobble\n    description: a wobble\n");
        try
        {
            FluentActions.Invoking(() => SignalShapeCatalog.Load(bad)).Should().Throw<InvalidOperationException>().WithMessage("*wobble*");
        }
        finally
        {
            File.Delete(bad);
        }
    }
}
