// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Live;

/// <summary>Live writes alerts. Shadow evaluates and counts what it would write.</summary>
public sealed class SpcOptions
{
    public const string Live = "Live";

    public const string Shadow = "Shadow";

    public SpcOptions(string mode)
    {
        Mode = mode;
    }

    public string Mode { get; }

    public bool IsShadow => string.Equals(Mode, Shadow, StringComparison.OrdinalIgnoreCase);

    public static SpcOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var mode = configuration["Spc:Mode"];
        if (string.IsNullOrWhiteSpace(mode))
        {
            return new SpcOptions(Live);
        }

        if (!string.Equals(mode, Live, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, Shadow, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Spc:Mode '" + mode + "' is not Live or Shadow.");
        }

        return new SpcOptions(mode);
    }
}
