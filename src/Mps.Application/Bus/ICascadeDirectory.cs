// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>
/// Remembers which event opened a fault, which reading is current, and which step last ran for a correlation.
/// Publishers use it to set causation. It is not the system of record.
/// </summary>
public interface ICascadeDirectory
{
    string? CorrelationFor(string toolId, string parameter);

    string? ReadingEventId(string toolId, string parameter);

    string? FaultEventId(string toolId, string parameter);

    string? FindStep(string correlationId, string stepName);

    void RememberFault(string correlationId, string toolId, string parameter, string eventId);

    void RememberReading(string toolId, string parameter, string eventId, string? correlationId);

    void RememberStep(string correlationId, string stepName, string eventId);

    void Clear();
}

/// <summary>Unit-test stand-in. Every lookup is empty.</summary>
public sealed class EmptyCascadeDirectory : ICascadeDirectory
{
    public static EmptyCascadeDirectory Instance { get; } = new();

    public string? CorrelationFor(string toolId, string parameter) => null;

    public string? ReadingEventId(string toolId, string parameter) => null;

    public string? FaultEventId(string toolId, string parameter) => null;

    public string? FindStep(string correlationId, string stepName) => null;

    public void RememberFault(string correlationId, string toolId, string parameter, string eventId)
    {
    }

    public void RememberReading(string toolId, string parameter, string eventId, string? correlationId)
    {
    }

    public void RememberStep(string correlationId, string stepName, string eventId)
    {
    }

    public void Clear()
    {
    }
}
