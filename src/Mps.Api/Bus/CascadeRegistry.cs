// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;

namespace Mps.Api.Bus;

/// <summary>In-memory links from a tool parameter and a correlation to the latest event ids.</summary>
public sealed class CascadeRegistry : ICascadeDirectory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _correlation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fault = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _step = new(StringComparer.Ordinal);

    public string? CorrelationFor(string toolId, string parameter) => Read(_correlation, Key(toolId, parameter));

    public string? ReadingEventId(string toolId, string parameter) => Read(_reading, Key(toolId, parameter));

    public string? FaultEventId(string toolId, string parameter) => Read(_fault, Key(toolId, parameter));

    public string? FindStep(string correlationId, string stepName) => Read(_step, StepKey(correlationId, stepName));

    public void RememberFault(string correlationId, string toolId, string parameter, string eventId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        var key = Key(toolId, parameter);
        if (key is null)
        {
            return;
        }

        lock (_gate)
        {
            _correlation[key] = correlationId.Trim();
            _fault[key] = eventId.Trim();
        }
    }

    public void RememberReading(string toolId, string parameter, string eventId, string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        var key = Key(toolId, parameter);
        if (key is null)
        {
            return;
        }

        lock (_gate)
        {
            _reading[key] = eventId.Trim();
            if (!string.IsNullOrWhiteSpace(correlationId) && !_correlation.ContainsKey(key))
            {
                _correlation[key] = correlationId.Trim();
            }
        }
    }

    public void RememberStep(string correlationId, string stepName, string eventId)
    {
        var key = StepKey(correlationId, stepName);
        if (key is null || string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        lock (_gate)
        {
            _step[key] = eventId.Trim();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _correlation.Clear();
            _reading.Clear();
            _fault.Clear();
            _step.Clear();
        }
    }

    private string? Read(Dictionary<string, string> map, string? key)
    {
        if (key is null)
        {
            return null;
        }

        lock (_gate)
        {
            return map.TryGetValue(key, out var value) ? value : null;
        }
    }

    private static string? Key(string? toolId, string? parameter)
    {
        if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(parameter))
        {
            return null;
        }

        return toolId.Trim() + "|" + parameter.Trim();
    }

    private static string? StepKey(string? correlationId, string? step)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(step))
        {
            return null;
        }

        return correlationId.Trim() + "|" + step.Trim();
    }
}
