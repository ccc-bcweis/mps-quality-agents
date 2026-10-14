// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Grounding;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Ranking;

/// <summary>
/// Deterministic category ranking. A shared lot wins, then an ECO, then a single-chamber calibration or alignment, then a ramp with no discrete event.
/// Three primaries on an ambiguous scope escalate.
/// </summary>
public static class RootCauseRanker
{
    public static RankDecision Rank(ResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var eco = request.EcoIds.Length > 0;
        var calibration = request.OverdueInstruments.Length > 0 || request.AlignmentSummaries.Length > 0;
        var material = request.LotIds.Length > 0 || request.NoticeIds.Length > 0;
        var primaries = (eco ? 1 : 0) + (calibration ? 1 : 0) + (material ? 1 : 0);
        var scope = request.ScopeKind ?? string.Empty;
        if (primaries >= 3 && !scope.Equals("materialLot", StringComparison.OrdinalIgnoreCase))
        {
            return RankDecision.Escalation("overlapping primaries: eco, calibration, and material lot");
        }

        if (scope.Equals("materialLot", StringComparison.OrdinalIgnoreCase) && material)
        {
            var lot = First(request.LotIds) ?? First(request.NoticeIds)!;
            var notice = First(request.NoticeIds);
            var cause = notice is null
                ? "Material lot " + lot + " spans the affected tools."
                : "Material lot " + lot + " spans the affected tools and supplier notice " + notice + " matches it.";
            var counters = request.EcoIds.Select(id => "ECO " + id + " is a confounder.").ToArray();
            var cited = Prefer(request.LotEvidenceIds, request.SignalEvidenceIds);
            return Recommend(RootCauseCategory.SupplyChainMaterial, cause, 0.84d, cited, counters, ["Hold material lot " + lot + " pending supplier review."]);
        }

        if (eco)
        {
            var ecoId = request.EcoIds[0];
            var counters = request.PmSummaries.Length == 0
                ? Array.Empty<string>()
                : request.PmSummaries;
            return Recommend(
                RootCauseCategory.EngineeringChange,
                "ECO " + ecoId + " changed the affected scope shortly before onset.",
                0.86d,
                Prefer(request.EcoEvidenceIds, request.SignalEvidenceIds),
                counters,
                ["Review " + ecoId + " before any corrective action."]);
        }

        if (scope.Equals("chamber", StringComparison.OrdinalIgnoreCase) && calibration)
        {
            var instrument = First(request.OverdueInstruments);
            var alignment = First(request.AlignmentSummaries);
            var cause = instrument is not null
                ? "Calibration of " + instrument + " is overdue on chamber " + request.ScopeKey + "."
                : "Alignment work is open on chamber " + request.ScopeKey + ": " + alignment;
            var counters = request.LotIds.Select(id => "Supplier lot " + id + " is a confounder.").ToArray();
            var cited = Prefer(
                request.OverdueEvidenceIds.Length > 0 ? request.OverdueEvidenceIds : request.AlignmentEvidenceIds,
                request.SignalEvidenceIds);
            return Recommend(RootCauseCategory.EquipmentCalibrationOrAlignment, cause, 0.82d, cited, counters, ["Restore calibration or alignment on chamber " + request.ScopeKey + " before the next run."]);
        }

        if (string.Equals(request.SignalShape, "ramp", StringComparison.OrdinalIgnoreCase) && !eco && !calibration && !material)
        {
            return Recommend(
                RootCauseCategory.ProcessDrift,
                "The parameter ramps with no discrete ECO, calibration, or material event in the window.",
                0.74d,
                request.SignalEvidenceIds,
                [],
                ["Recommend a chamber clean. Do not open a CAPA."]);
        }

        return RankDecision.Escalation("no primary discriminator");
    }

    private static RankDecision Recommend(
        RootCauseCategory category,
        string cause,
        double confidence,
        IReadOnlyList<string> evidenceIds,
        IReadOnlyList<string> counterEvidence,
        IReadOnlyList<string> actions)
    {
        var ids = ParseIds(evidenceIds);
        if (ids.Count == 0)
        {
            return RankDecision.Escalation("primary has no evidence id");
        }

        var claim = new ResolutionClaim(category, cause, confidence, ids, counterEvidence, actions, []);
        return RankDecision.Recommendation(claim);
    }

    private static List<EvidenceId> ParseIds(IReadOnlyList<string> values)
    {
        var ids = new List<EvidenceId>();
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                ids.Add(new EvidenceId(parsed));
            }
        }

        return ids;
    }

    private static string[] Prefer(string[] primary, string[] fallback)
    {
        return primary.Length > 0 ? primary : fallback;
    }

    private static string? First(string[] values)
    {
        return values.Length == 0 ? null : values[0];
    }
}
