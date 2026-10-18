// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Investigation;

/// <summary>Shape-classifier thresholds and the model call budget, from data/metadata/investigation.yaml, loaded once at startup.</summary>
public sealed class InvestigationTuning
{
    private static readonly Lazy<InvestigationTuning> SharedTuning = new(() => Load(DataFiles.Find(Path.Combine("metadata", "investigation.yaml"))));

    public static InvestigationTuning Shared => SharedTuning.Value;

    public ShapeClassifierSettings ShapeClassifier { get; set; } = new();

    public ResolutionSettings Resolution { get; set; } = new();

    public static InvestigationTuning Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("data/metadata/investigation.yaml was not found.");
        }

        var tuning = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<InvestigationTuning>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("investigation.yaml is empty.");
        var shape = tuning.ShapeClassifier;
        if (shape.StepShare <= 0 || shape.FlatShare <= 0 || shape.RampShare <= 0 || shape.SameDirectionShare <= 0 || shape.NoisyStepSigmas <= 0
            || shape.NoisyStepFitShare <= 0 || shape.NoisyStepSideDrift <= 0 || shape.NoisyStepMinSide <= 0 || shape.NoisyStepSmoothing <= 0
            || shape.NoisyStepEdgeReadings <= 0 || tuning.Resolution.MaxModelCalls <= 0 || tuning.Resolution.ShapeRetries < 0)
        {
            throw new InvalidOperationException("investigation.yaml is missing a shapeClassifier or resolution setting.");
        }

        return tuning;
    }

    public sealed class ShapeClassifierSettings
    {
        public double StepShare { get; set; }

        public double FlatShare { get; set; }

        public double RampShare { get; set; }

        public double SameDirectionShare { get; set; }

        public double NoisyStepSigmas { get; set; }

        public double NoisyStepFitShare { get; set; }

        public double NoisyStepSideDrift { get; set; }

        public int NoisyStepMinSide { get; set; }

        public int NoisyStepSmoothing { get; set; }

        public int NoisyStepEdgeReadings { get; set; }
    }

    public sealed class ResolutionSettings
    {
        public int MaxModelCalls { get; set; }

        public int ShapeRetries { get; set; }
    }
}
