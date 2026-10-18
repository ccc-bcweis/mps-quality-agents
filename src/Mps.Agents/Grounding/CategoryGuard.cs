// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Explain;
using Mps.Agents.Ranking;
using Mps.Application.Wording;
using Mps.Domain.Hypotheses;

namespace Mps.Agents.Grounding;

/// <summary>A cited record and the class a category evidence rule says it implies.</summary>
public sealed record CategoryImplication(EvidenceNote Note, CategoryEvidenceRule Rule);

/// <summary>
/// Checks the structured category against the records the reply cites, using the metadata map in
/// data/metadata/category-evidence.yaml. When cited records imply classes, the category must be one of them.
/// Records the reply itself rules out (ruledOutIds, counterEvidence, or rulesOut for that class) do not count.
/// </summary>
public static class CategoryGuard
{
    /// <summary>The cited records that imply a class, after the reply's own ruled-out records are set aside.</summary>
    public static IReadOnlyList<CategoryImplication> Implications(ResolutionClaim claim, IReadOnlyList<EvidenceNote> notes, CategoryEvidenceRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(notes);
        rules ??= CategoryEvidenceRules.Shared;
        var cited = claim.EvidenceIds.Select(id => id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<CategoryImplication>();
        foreach (var note in notes.Where(note => cited.Contains(note.Id)))
        {
            foreach (var rule in rules.Rules.Where(rule => rule.Matches(note)))
            {
                if (!SetAside(claim, note, rule.Implies))
                {
                    found.Add(new CategoryImplication(note, rule));
                }
            }
        }

        return found;
    }

    /// <summary>Null when the category agrees with the cited records, otherwise a plain escalation reason.</summary>
    public static string? Check(ResolutionClaim claim, IReadOnlyList<EvidenceNote> notes, CategoryEvidenceRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var implications = Implications(claim, notes, rules);
        if (implications.Count == 0 || (claim.Category is { } category && implications.Any(item => item.Rule.Implies == category)))
        {
            return null;
        }

        var first = implications[0];
        return "The AI ranked the cause as " + Name(claim.Category) + ", but it cites " + Label(first.Note) + ", where " + first.Rule.Description
            + ", which points to " + Name(first.Rule.Implies) + ".";
    }

    /// <summary>The note sent back to the model once when its category disagrees with the records it cites.</summary>
    public static string Feedback(ResolutionClaim claim, IReadOnlyList<EvidenceNote> notes, CategoryEvidenceRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var implications = Implications(claim, notes, rules);
        var implied = implications.Select(item => Name(item.Rule.Implies)).Distinct().ToArray();
        var records = string.Join("; ", implications.Select(item => Label(item.Note) + " (" + item.Rule.Description + ")").Distinct());
        return "Your previous answer set category to \"" + Name(claim.Category) + "\", but it cites " + records + ", which points to "
            + string.Join(" or ", implied.Select(name => "\"" + name + "\"")) + ". Set category to the class the cited records support. "
            + "If such a record is not the cause, put its id in ruledOutIds with " + PlainWording.Shared.PromptClause
            + " Or list it in counterEvidence and mark it rulesOut for that class in evidenceMatrix, or escalate.";
    }

    private static bool SetAside(ResolutionClaim claim, EvidenceNote note, RootCauseCategory implied)
    {
        if (claim.RuledOutIds.Any(ruled => string.Equals(ruled.Id.ToString(), note.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var reference = note.ExternalRef;
        if (!string.IsNullOrWhiteSpace(reference) && claim.CounterEvidence.Any(text => text.Contains(reference, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return claim.EvidenceMatrix.Any(entry => entry.Category == implied
            && entry.Stance == EvidenceStance.RulesOut
            && string.Equals(entry.EvidenceId.ToString(), note.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static string Label(EvidenceNote note) => string.IsNullOrWhiteSpace(note.ExternalRef) ? note.Title : note.ExternalRef;

    private static string Name(RootCauseCategory? category) =>
        category is null ? "no class" : JsonNamingPolicy.CamelCase.ConvertName(category.Value.ToString());
}
