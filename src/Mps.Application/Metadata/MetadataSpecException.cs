// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Metadata;

/// <summary>
/// A metadata file or a referential check failed. The message lists every violation found in that load.
/// </summary>
public sealed class MetadataSpecException : Exception
{
    public MetadataSpecException(IReadOnlyList<string> errors)
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
