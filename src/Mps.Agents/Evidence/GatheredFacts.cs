// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Impact;

namespace Mps.Agents.Evidence;

/// <summary>
/// Evidence pack plus the observations used to compute impact features.
/// </summary>
public sealed class GatheredFacts
{
    public GatheredFacts(
        EvidencePack pack,
        IReadOnlyList<ImpactObservation> observations,
        IReadOnlyList<AlignedEvent> events,
        SignalShape shape)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(events);
        Pack = pack;
        Observations = observations.ToArray();
        Events = events.ToArray();
        Shape = shape;
    }

    public EvidencePack Pack { get; }

    public IReadOnlyList<ImpactObservation> Observations { get; }

    public IReadOnlyList<AlignedEvent> Events { get; }

    public SignalShape Shape { get; }
}
