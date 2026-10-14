// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Domain.Hypotheses;

namespace Mps.Application.Evidence;

public enum EvidenceStance
{
    Supports,
    RulesOut,
    Neutral,
}

public sealed record EvidenceNote(Guid EvidenceId, string? WhyItMatters, string? SignalId);

public sealed record EvidenceMatrixEntry(
    RootCauseCategory Category,
    Guid EvidenceId,
    EvidenceStance Stance,
    string? WhyItMatters);

/// <summary>
/// Plain-language notes and the root-cause matrix carried beside an investigation.
/// Domain evidence does not store these fields yet, so the API persists them here.
/// </summary>
public sealed class InvestigationNarrative
{
    public const int MaxWhyItMattersLength = 240;

    public const int MaxSignalIdLength = 200;

    public static InvestigationNarrative Empty { get; } = new([], []);

    public InvestigationNarrative(IReadOnlyList<EvidenceNote> notes, IReadOnlyList<EvidenceMatrixEntry> matrix)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(matrix);
        Notes = notes.ToArray();
        Matrix = matrix.ToArray();
    }

    public IReadOnlyList<EvidenceNote> Notes { get; }

    public IReadOnlyList<EvidenceMatrixEntry> Matrix { get; }

    public static InvestigationNarrative Create(
        IReadOnlyList<EvidenceNote> notes,
        IReadOnlyList<EvidenceMatrixEntry> matrix,
        IReadOnlySet<Guid> evidenceIds)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(evidenceIds);

        var normalizedNotes = new List<EvidenceNote>(notes.Count);
        var seenNotes = new HashSet<Guid>();
        foreach (var note in notes)
        {
            RequireKnown(note.EvidenceId, evidenceIds, "whyItMatters must cite an evidence item on the investigation.");
            if (!seenNotes.Add(note.EvidenceId))
            {
                throw new IntakeValidationException("Each evidence item has one whyItMatters.");
            }

            normalizedNotes.Add(new EvidenceNote(
                note.EvidenceId,
                NormalizeWhy(note.WhyItMatters),
                NormalizeSignal(note.SignalId)));
        }

        var normalizedMatrix = new List<EvidenceMatrixEntry>(matrix.Count);
        var seenMatrix = new HashSet<(RootCauseCategory Category, Guid EvidenceId)>();
        foreach (var entry in matrix)
        {
            RequireKnown(entry.EvidenceId, evidenceIds, "The evidence matrix must cite an evidence item on the investigation.");
            if (!seenMatrix.Add((entry.Category, entry.EvidenceId)))
            {
                throw new IntakeValidationException("The evidence matrix already cites that category for this evidence item.");
            }

            normalizedMatrix.Add(new EvidenceMatrixEntry(
                entry.Category,
                entry.EvidenceId,
                entry.Stance,
                NormalizeWhy(entry.WhyItMatters)));
        }

        return new InvestigationNarrative(normalizedNotes, normalizedMatrix);
    }

    private static void RequireKnown(Guid evidenceId, IReadOnlySet<Guid> evidenceIds, string message)
    {
        if (evidenceId == Guid.Empty || !evidenceIds.Contains(evidenceId))
        {
            throw new IntakeValidationException(message);
        }
    }

    private static string? NormalizeWhy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxWhyItMattersLength)
        {
            throw new IntakeValidationException("whyItMatters must be 240 characters or fewer.");
        }

        if (HasControl(trimmed))
        {
            throw new IntakeValidationException("whyItMatters contains disallowed control characters.");
        }

        return trimmed;
    }

    private static string? NormalizeSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxSignalIdLength)
        {
            throw new IntakeValidationException("signalId must be 200 characters or fewer.");
        }

        if (HasControl(trimmed))
        {
            throw new IntakeValidationException("signalId contains disallowed control characters.");
        }

        return trimmed;
    }

    private static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
