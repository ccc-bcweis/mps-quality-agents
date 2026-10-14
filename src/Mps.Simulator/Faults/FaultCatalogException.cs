// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Faults;

public sealed class FaultCatalogException : Exception
{
    public FaultCatalogException(IReadOnlyList<string> errors)
        : base(Join(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    private static string Join(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return string.Join(Environment.NewLine, errors);
    }
}
