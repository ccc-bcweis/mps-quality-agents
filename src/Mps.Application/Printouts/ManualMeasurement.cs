// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;

namespace Mps.Application.Printouts;

/// <summary>One confirmed bench reading, stored as evidence on the incident it opened or joined.</summary>
public sealed class ManualMeasurement
{
    public const string WhyItMatters = "A technician measured this on a bench instrument, so the investigation can cite the printed reading and not only the tool's own sensor.";

    public const string AlertRule = "manualMeasurement";

    public ManualMeasurement(EvidenceItem evidence, Guid incidentId)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        Evidence = evidence;
        IncidentId = incidentId;
    }

    public EvidenceItem Evidence { get; }

    public Guid IncidentId { get; }
}

public sealed class InMemoryManualMeasurementLog : IManualMeasurementLog
{
    private readonly List<ManualMeasurement> _items = [];
    private readonly object _gate = new();

    public void Add(ManualMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        lock (_gate)
        {
            _items.Add(measurement);
        }
    }

    public IReadOnlyList<ManualMeasurement> ForTool(string toolId)
    {
        lock (_gate)
        {
            return _items
                .Where(item => string.Equals(item.Evidence.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }
}
