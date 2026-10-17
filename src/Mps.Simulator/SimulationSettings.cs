// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator;

/// <summary>How the fictional plant generates readings, from data/metadata/simulation.yaml, loaded once at startup.</summary>
public sealed class SimulationSettings
{
    private static readonly Lazy<SimulationSettings> SharedSettings = new(() => Load(DataFiles.Find(Path.Combine("metadata", "simulation.yaml"))));

    public static SimulationSettings Shared => SharedSettings.Value;

    public RecipeScheduleSettings RecipeSchedule { get; set; } = new();

    public NoiseSettings Noise { get; set; } = new();

    public double RealTimeFaultPace { get; set; }

    public SiteComparisonSettings SiteComparison { get; set; } = new();

    public static SimulationSettings Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("data/metadata/simulation.yaml was not found.");
        }

        var settings = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<SimulationSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("simulation.yaml is empty.");
        if (settings.RecipeSchedule.StepSeconds <= 0 || settings.RecipeSchedule.WaferSeconds <= 0 || settings.RecipeSchedule.WafersPerLot <= 0
            || settings.Noise.Phi <= 0 || settings.Noise.Scale <= 0 || settings.Noise.ExcursionChance < 0 || settings.Noise.ExcursionSigma <= 0
            || settings.RealTimeFaultPace <= 0 || settings.SiteComparison.SpreadRatio <= 0)
        {
            throw new InvalidOperationException("simulation.yaml is missing a setting.");
        }

        return settings;
    }

    public sealed class RecipeScheduleSettings
    {
        public int StepSeconds { get; set; }

        public int WaferSeconds { get; set; }

        public int WafersPerLot { get; set; }
    }

    public sealed class NoiseSettings
    {
        public double Phi { get; set; }

        public double Scale { get; set; }

        public double ExcursionChance { get; set; }

        public double ExcursionSigma { get; set; }
    }

    public sealed class SiteComparisonSettings
    {
        public double SpreadRatio { get; set; }
    }
}
