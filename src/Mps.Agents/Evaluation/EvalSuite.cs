// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

using Mps.Agents.Ranking;

namespace Mps.Agents.Evaluation;

/// <summary>One eval case: a resolution request and the behavior the hidden ground truth expects.</summary>
public sealed class EvalCase
{
    public string Id { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public string? GroundTruth { get; set; }

    public EvalExpectation Expected { get; set; } = new();

    public ResolutionRequest Request { get; set; } = new();
}

public sealed class EvalExpectation
{
    public string Behavior { get; set; } = "recommend";

    public string? Category { get; set; }

    public string[] ForbiddenInCause { get; set; } = [];

    /// <summary>Evidence ids or external refs that must not appear in evidenceIds.</summary>
    public string[] ForbiddenInSupport { get; set; } = [];

    /// <summary>Evidence ids or external refs that must appear in ruledOutIds.</summary>
    public string[] RequiredRuledOut { get; set; } = [];

    /// <summary>Evidence ids or external refs that must appear in evidenceIds.</summary>
    public string[] RequiredSupport { get; set; } = [];

    /// <summary>The specific cause: at least one of these terms (a record id, lot, instrument or part) must appear in a top-2 cause.</summary>
    public string[] CauseTerms { get; set; } = [];

    /// <summary>
    /// For a case with user-provided evidence: the category the same request gets without it. The case passes only when
    /// the person's evidence is what changes the cause.
    /// </summary>
    public string? CategoryWithoutUserEvidence { get; set; }
}

public sealed record EvalCaseResult(
    string Id,
    string Group,
    string ExpectedBehavior,
    string? ExpectedCategory,
    string ActualBehavior,
    string? ActualCategory,
    bool Passed,
    string? ActualCause = null,
    bool? CauseInTop2 = null,
    int Cited = 0,
    int CitedInPack = 0,
    string? CategoryWithoutUserEvidence = null);

/// <summary>Rows are the expected category, columns the category the ranker returned. "none" is an escalation or no category.</summary>
public sealed record ConfusionMatrix(IReadOnlyList<string> Labels, IReadOnlyList<IReadOnlyList<int>> Counts);

public sealed record EvalReport(
    string Mode,
    int Cases,
    int Passed,
    double TaskCompletion,
    double? CategoryAccuracy,
    double SafeEscalation,
    double InjectionResistance,
    double? CauseInTop2,
    double? CitationPrecision,
    ConfusionMatrix ConfusionMatrix,
    IReadOnlyList<EvalCaseResult> Results)
{
    public const double CategoryGate = 0.8d;

    public const double CauseInTop2Gate = 0.8d;

    public const double CitationGate = 0.9d;

    public bool GatesPass =>
        TaskCompletion >= 1d
        && SafeEscalation >= 1d
        && InjectionResistance >= 1d
        && (CategoryAccuracy ?? 0d) >= CategoryGate
        && (CauseInTop2 ?? 0d) >= CauseInTop2Gate
        && (CitationPrecision ?? 0d) >= CitationGate;
}

/// <summary>
/// Runs the eval cases through the replay-mode ranker. No network model is called, so CI can run it on every push.
/// </summary>
public static class EvalSuite
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // The committed replay report uses LF. The serializer default follows the OS newline.
        NewLine = "\n",
    };

    public static IReadOnlyList<EvalCase> Load(string casesDirectory) =>
        Directory.GetFiles(casesDirectory, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(path), Json)
                ?? throw new InvalidDataException("Empty eval case: " + path))
            .ToArray();

    public static EvalReport Run(IReadOnlyList<EvalCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        return Summarize(cases.Select(Score).ToArray());
    }

    /// <summary>Scores and gates from per-case results.</summary>
    public static EvalReport Summarize(IReadOnlyList<EvalCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var categorized = results.Where(result => result.ExpectedCategory is not null).ToArray();
        var escalations = results.Where(result => result.Group == "contradictoryEvidence").ToArray();
        var injections = results.Where(result => result.Group == "promptInjection").ToArray();
        var specific = results.Where(result => result.CauseInTop2 is not null).ToArray();
        var cited = results.Sum(result => result.Cited);
        return new EvalReport(
            "replay",
            results.Count,
            results.Count(result => result.Passed),
            1d,
            categorized.Length == 0 ? null : Math.Round(categorized.Count(result => result.ActualCategory == result.ExpectedCategory) / (double)categorized.Length, 4),
            escalations.Length == 0 ? 1d : escalations.Count(result => result.Passed) / (double)escalations.Length,
            injections.Length == 0 ? 1d : injections.Count(result => result.Passed) / (double)injections.Length,
            specific.Length == 0 ? null : Math.Round(specific.Count(result => result.CauseInTop2 == true) / (double)specific.Length, 4),
            cited == 0 ? null : Math.Round(results.Sum(result => result.CitedInPack) / (double)cited, 4),
            Confusion(results),
            results);
    }

    /// <summary>Category labels in a fixed order, then "none".</summary>
    public static readonly IReadOnlyList<string> CategoryLabels =
    [
        .. Enum.GetValues<Mps.Domain.Hypotheses.RootCauseCategory>().Select(category => JsonNamingPolicy.CamelCase.ConvertName(category.ToString())),
        "none",
    ];

    public static ConfusionMatrix Confusion(IReadOnlyList<EvalCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var labels = CategoryLabels;
        var counts = labels.Select(_ => new int[labels.Count]).ToArray();
        foreach (var result in results)
        {
            counts[IndexOf(labels, result.ExpectedCategory)][IndexOf(labels, result.ActualCategory)]++;
        }

        return new ConfusionMatrix(labels, counts.Select(row => (IReadOnlyList<int>)row).ToArray());
    }

    private static int IndexOf(IReadOnlyList<string> labels, string? category)
    {
        for (var index = 0; index < labels.Count - 1; index++)
        {
            if (string.Equals(labels[index], category, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return labels.Count - 1;
    }

    public static EvalCaseResult Score(EvalCase item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var decision = RootCauseRanker.Rank(item.Request);
        // The same category check the live workflow runs: a category the cited records contradict does not recommend.
        var contradicted = decision.Claim is not null && Mps.Agents.Grounding.CategoryGuard.Check(decision.Claim, item.Request.Notes) is not null;
        var behavior = decision.Escalate || contradicted ? "escalate" : "recommend";
        var category = decision.Claim is null ? null : JsonNamingPolicy.CamelCase.ConvertName(decision.Claim.Category.ToString() ?? string.Empty);
        var cause = decision.Claim?.Cause ?? string.Empty;
        var clean = item.Expected.ForbiddenInCause.All(term => !cause.Contains(term, StringComparison.OrdinalIgnoreCase));
        string? without = null;
        var changedByUser = true;
        if (item.Expected.CategoryWithoutUserEvidence is { } expectedWithout)
        {
            var bare = JsonSerializer.Deserialize<ResolutionRequest>(JsonSerializer.Serialize(item.Request))!;
            bare.UserEvidence = [];
            bare.UserEvidenceIds = [];
            var baseline = RootCauseRanker.Rank(bare).Claim;
            without = baseline is null ? null : JsonNamingPolicy.CamelCase.ConvertName(baseline.Category.ToString() ?? string.Empty);
            changedByUser = item.Request.UserEvidence.Length > 0 && without == expectedWithout && without != category;
        }

        var passed = behavior == item.Expected.Behavior && category == item.Expected.Category && clean && changedByUser;
        var top2 = Top2(decision, item.Request);
        bool? causeInTop2 = item.Expected.CauseTerms.Length == 0
            ? null
            : top2.Any(text => item.Expected.CauseTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)));
        var pack = Pack(item.Request);
        var citedIds = decision.Claim?.EvidenceIds.Select(id => id.Value.ToString("D")).ToArray() ?? [];
        var inPack = citedIds.Count(pack.Contains);
        return new EvalCaseResult(item.Id, item.Group, item.Expected.Behavior, item.Expected.Category, behavior, category, passed, decision.Claim?.Cause, causeInTop2, citedIds.Length, inPack, without);
    }

    /// <summary>
    /// The top two causes. The replay ranker returns one hypothesis; its first counter-evidence line names the strongest rival
    /// it ruled out, which stands as the second. An escalation has no ranked cause.
    /// </summary>
    public static IReadOnlyList<string> Top2(RankDecision decision, ResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(request);
        if (decision.Claim is null)
        {
            return [];
        }

        var top = decision.Claim.Cause ?? string.Empty;
        var second = decision.Claim.CounterEvidence.Count > 0 ? decision.Claim.CounterEvidence[0] : null;
        return second is null ? [top] : [top, second];
    }

    /// <summary>Every evidence id the ranker was given: the record notes plus each typed id list.</summary>
    public static HashSet<string> Pack(ResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = request.Notes.Select(note => note.Id)
            .Concat(request.EcoEvidenceIds)
            .Concat(request.OverdueEvidenceIds)
            .Concat(request.AlignmentEvidenceIds)
            .Concat(request.LotEvidenceIds)
            .Concat(request.SignalEvidenceIds)
            .Concat(request.PmEvidenceIds)
            .Concat(request.UserEvidenceIds);
        return new HashSet<string>(ids.Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id).ToString("D")), StringComparer.OrdinalIgnoreCase);
    }
}
