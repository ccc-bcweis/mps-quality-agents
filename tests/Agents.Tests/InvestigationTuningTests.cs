// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Mps.Agents.Investigation;
using Mps.Agents.Resolution;

namespace Mps.Agents.Tests;

/// <summary>Shape-classifier thresholds and the model call budget come from data/metadata/investigation.yaml.</summary>
public sealed class InvestigationTuningTests
{
    [Fact]
    public void ShippedValuesMatchTheFormerConstants()
    {
        var shape = InvestigationTuning.Shared.ShapeClassifier;
        (shape.StepShare, shape.FlatShare, shape.RampShare, shape.SameDirectionShare).Should().Be((0.7d, 0.2d, 0.5d, 0.75d));
        (shape.NoisyStepSigmas, shape.NoisyStepFitShare, shape.NoisyStepSideDrift).Should().Be((3d, 0.6d, 0.25d));
        (shape.NoisyStepMinSide, shape.NoisyStepSmoothing, shape.NoisyStepEdgeReadings).Should().Be((6, 5, 10));
        ResolutionAgent.MaxModelCalls.Should().Be(2);
        InvestigationTuning.Shared.Resolution.ShapeRetries.Should().Be(1);
    }

    [Fact]
    public void MissingSectionFailsAtLoad()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "resolution: { maxModelCalls: 2, shapeRetries: 1 }\n");
            var act = () => InvestigationTuning.Load(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*shapeClassifier*");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
