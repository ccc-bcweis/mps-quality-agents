// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Resolution;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class RecommendedActionsTests
{
    private static readonly EvidenceItem Eco = new(EvidenceId.New(), EvidenceSource.Plm, "Edge ring rev C", "x", DateTimeOffset.UnixEpoch, "E-02", "ECO-4471");

    [Theory]
    [InlineData(RootCauseCategory.EngineeringChange)]
    [InlineData(RootCauseCategory.EquipmentCalibrationOrAlignment)]
    [InlineData(RootCauseCategory.SupplyChainMaterial)]
    [InlineData(RootCauseCategory.ProcessDrift)]
    public void ASufficientRecommendationAlwaysHasContainmentAndPreventive(RootCauseCategory category)
    {
        var actions = RecommendedActions.Complete(category, [], [Eco], "E-02");

        actions.Should().Contain(action => action.StartsWith(RecommendedActions.ContainmentPrefix, StringComparison.Ordinal));
        actions.Should().Contain(action => action.StartsWith(RecommendedActions.PreventivePrefix, StringComparison.Ordinal));
        actions.Should().OnlyContain(action => !action.Contains('{'));
    }

    [Fact]
    public void ModelActionsAreKeptAndLabelled()
    {
        var actions = RecommendedActions.Complete(RootCauseCategory.EngineeringChange, ["Roll back to edge ring rev B.", "Engineering review of ECO-4471."], [Eco], "E-02");

        actions.Should().Equal("Containment: Roll back to edge ring rev B.", "Preventive: Engineering review of ECO-4471.");
    }

    [Fact]
    public void TheEngineeringChangeFallbackNamesTheRecord()
    {
        RecommendedActions.Complete(RootCauseCategory.EngineeringChange, [], [Eco], "E-02").Should().OnlyContain(action => action.Contains("ECO-4471"));
    }
}
