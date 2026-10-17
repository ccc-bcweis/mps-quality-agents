// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>
/// The ticket (or alert) event an investigation hangs off, captured when the investigation is kicked off.
/// A background investigation reads it instead of the shared step pointer, which the next tool may already have moved.
/// </summary>
public static class InvestigationParent
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? EventId => Current.Value;

    /// <summary>Run <paramref name="work"/> off the caller's flow with the parent captured now.</summary>
    public static Task Run(string? parentEventId, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var previous = Current.Value;
        Current.Value = parentEventId;
        try
        {
            return Task.Run(work, CancellationToken.None);
        }
        finally
        {
            Current.Value = previous;
        }
    }
}
