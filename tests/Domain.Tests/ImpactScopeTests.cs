// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Impact;

namespace Mps.Domain.Tests;

public sealed class ImpactScopeTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ImpactScopeKind.Chamber)]
    [InlineData(ImpactScopeKind.Tool)]
    [InlineData(ImpactScopeKind.Recipe)]
    [InlineData(ImpactScopeKind.MaterialLot)]
    public void AScopeKeepsItsKindAndTrimsTheKeyAndNote(ImpactScopeKind kind)
    {
        var scope = new ImpactScope(kind, "  E-02  ", At, "  aligned to the ECO  ");

        scope.Kind.Should().Be(kind);
        scope.Key.Should().Be("E-02");
        scope.AlignedEventAt.Should().Be(At);
        scope.AlignmentNote.Should().Be("aligned to the ECO");
    }

    [Fact]
    public void ABlankNoteIsDroppedAndABlankKeyIsRejected()
    {
        var scope = new ImpactScope(ImpactScopeKind.Tool, "E-02", null, "   ");

        scope.AlignedEventAt.Should().BeNull();
        scope.AlignmentNote.Should().BeNull();
        new ImpactScope(ImpactScopeKind.Recipe, "RCP", null, null).AlignmentNote.Should().BeNull();

        var whitespace = () => new ImpactScope(ImpactScopeKind.MaterialLot, "  ", At, "note");
        whitespace.Should().Throw<DomainRuleException>().WithMessage("Impact scope key is required.");
        var missing = () => new ImpactScope(ImpactScopeKind.Chamber, null!, At, "note");
        missing.Should().Throw<DomainRuleException>().WithMessage("Impact scope key is required.");
    }
}
