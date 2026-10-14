// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Fleet;

/// <summary>
/// A fleet YAML file broke the schema. The message lists every violation found in that load.
/// </summary>
public sealed class FleetSpecException : Exception
{
    public FleetSpecException(IReadOnlyList<string> errors)
        : base(Join(errors))
    {
        Errors = errors;
    }

    private static string Join(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return string.Join(Environment.NewLine, errors);
    }

    public IReadOnlyList<string> Errors { get; }
}
