// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Ranking;

/// <summary>
/// Deterministic category ranking. An out-of-spec lot that is shared, or that stepped on more than one tool, wins.
/// Then a real engineering change, then a single-chamber calibration or alignment, then a ramp with no discrete event.
/// A document-only note is not an engineering change. An in-spec lot is not a material primary.
/// Three primaries on an ambiguous scope escalate.
/// </summary>
public static class RootCauseRanker
{
    private static readonly Regex ToolToken = new(@"\b(?:I-)?[A-Z]-\d{2}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static RankDecision Rank(ResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var documentOnly = request.EcoIds.Where(id => IsDocumentOnly(request, id)).ToArray();
        var realEcos = request.EcoIds.Where(id => !documentOnly.Contains(id, StringComparer.OrdinalIgnoreCase)).ToArray();
        var eco = realEcos.Length > 0;
        var calibration = request.OverdueInstruments.Length > 0 || request.AlignmentSummaries.Length > 0;
        var material = HasOutOfSpecMaterial(request);
        var primaries = (eco ? 1 : 0) + (calibration ? 1 : 0) + (material ? 1 : 0);
        var scope = request.ScopeKind ?? string.Empty;
        if (primaries >= 3 && !scope.Equals("materialLot", StringComparison.OrdinalIgnoreCase))
        {
            return RankDecision.Escalation("overlapping primaries: eco, calibration, and material lot");
        }

        if (material && (scope.Equals("materialLot", StringComparison.OrdinalIgnoreCase) || (IsStep(request) && NoticeOrLotNamesSeveralTools(request))))
        {
            return SupplyChain(request);
        }

        if (eco)
        {
            var ecoId = realEcos[0];
            var counters = request.PmSummaries.Length == 0
                ? DocumentOnlyCounters(documentOnly)
                : request.PmSummaries.Concat(DocumentOnlyCounters(documentOnly)).ToArray();
            return Recommend(
                request,
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
            var counters = request.LotIds.Select(id => "Supplier lot " + id + " is a confounder.")
                .Concat(DocumentOnlyCounters(documentOnly))
                .ToArray();
            var cited = Prefer(
                request.OverdueEvidenceIds.Length > 0 ? request.OverdueEvidenceIds : request.AlignmentEvidenceIds,
                request.SignalEvidenceIds);
            return Recommend(request, RootCauseCategory.EquipmentCalibrationOrAlignment, cause, 0.82d, cited, counters, ["Restore calibration or alignment on chamber " + request.ScopeKey + " before the next run."]);
        }

        if (!eco && !material && Mentions(request, "bearing"))
        {
            return Recommend(
                request,
                RootCauseCategory.EquipmentCalibrationOrAlignment,
                "A maintenance record shows the pump bearing is past its service interval, and vibration is rising.",
                0.83d,
                Prefer(request.PmEvidenceIds, request.SignalEvidenceIds),
                request.LotIds.Select(id => "Supplier lot " + id + " is a confounder.").Concat(DocumentOnlyCounters(documentOnly)).ToArray(),
                ["Replace the pump bearing before the next production lot."]);
        }

        if (!eco && !material && Mentions(request, "recipe setpoint"))
        {
            return Recommend(
                request,
                RootCauseCategory.ProcessDrift,
                "An operator changed a recipe setpoint and there is no engineering change order for that edit.",
                0.8d,
                Prefer(request.PmEvidenceIds, request.SignalEvidenceIds),
                DocumentOnlyCounters(documentOnly),
                ["Restore the approved recipe setpoint and record the edit."]);
        }

        if (string.Equals(request.SignalShape, "ramp", StringComparison.OrdinalIgnoreCase) && !eco && !calibration && !material)
        {
            var seasoning = request.PmSummaries.FirstOrDefault(summary =>
                summary.Contains("season", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("clean", StringComparison.OrdinalIgnoreCase));
            var cause = seasoning is null
                ? "The parameter ramps with no discrete ECO, calibration, or material event in the window."
                : "Chamber seasoning after a clean is driving the parameter ramp.";
            return Recommend(
                request,
                RootCauseCategory.ProcessDrift,
                cause,
                0.74d,
                request.SignalEvidenceIds,
                DocumentOnlyCounters(documentOnly),
                ["Recommend a chamber clean. Do not open a CAPA."]);
        }

        return RankDecision.Escalation("no primary discriminator");
    }

    private static RankDecision SupplyChain(ResolutionRequest request)
    {
        var lot = CitedLot(request);
        var notice = First(request.NoticeIds);
        var cause = notice is null
            ? "Material lot " + lot + " spans the affected tools."
            : "Material lot " + lot + " spans the affected tools and supplier notice " + notice + " matches it.";
        var counters = request.EcoIds.Select(id => "ECO " + id + " is a confounder.")
            .Concat(request.PmSummaries
                .Where(summary => summary.Contains("clean", StringComparison.OrdinalIgnoreCase))
                .Select(summary => summary + " is a confounder on one tool."))
            .ToArray();
        var cited = Prefer(request.LotEvidenceIds, request.SignalEvidenceIds);
        return Recommend(request, RootCauseCategory.SupplyChainMaterial, cause, 0.84d, cited, counters, ["Hold material lot " + lot + " pending supplier review."]);
    }

    private static string CitedLot(ResolutionRequest request)
    {
        foreach (var noticeId in request.NoticeIds)
        {
            var text = NoteText(request, noticeId);
            if (text.Length == 0 || !CountsAsOutOfSpec(text))
            {
                continue;
            }

            foreach (var lotId in request.LotIds)
            {
                if (text.Contains(lotId, StringComparison.OrdinalIgnoreCase))
                {
                    return lotId;
                }
            }
        }

        return First(request.LotIds) ?? First(request.NoticeIds)!;
    }

    private static bool HasOutOfSpecMaterial(ResolutionRequest request)
    {
        if (request.NoticeIds.Length == 0)
        {
            return false;
        }

        foreach (var noticeId in request.NoticeIds)
        {
            var text = NoteText(request, noticeId);
            if (text.Length == 0 || CountsAsOutOfSpec(text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CountsAsOutOfSpec(string text)
    {
        if (text.Contains("out of spec", StringComparison.OrdinalIgnoreCase)
            || text.Contains("out of specification", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Contains("inside", StringComparison.OrdinalIgnoreCase)
            || text.Contains("matches the purchase specification", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not a moisture failure", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsDocumentOnly(ResolutionRequest request, string ecoId)
    {
        var text = NoteText(request, ecoId);
        if (text.Length == 0)
        {
            return false;
        }

        var documentOnly = text.Contains("document-only", StringComparison.OrdinalIgnoreCase)
            || text.Contains("document only", StringComparison.OrdinalIgnoreCase);
        if (!documentOnly)
        {
            return false;
        }

        if (text.Contains("replace", StringComparison.OrdinalIgnoreCase)
            || text.Contains("install", StringComparison.OrdinalIgnoreCase)
            || text.Contains("edge ring", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool NoticeOrLotNamesSeveralTools(ResolutionRequest request)
    {
        var refs = new HashSet<string>(request.NoticeIds.Concat(request.LotIds), StringComparer.OrdinalIgnoreCase);
        if (refs.Count == 0)
        {
            return false;
        }

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in request.Notes)
        {
            var blob = NoteBlob(note);
            var linked = (note.ExternalRef is not null && refs.Contains(note.ExternalRef))
                || refs.Any(id => blob.Contains(id, StringComparison.OrdinalIgnoreCase));
            if (!linked)
            {
                continue;
            }

            foreach (Match match in ToolToken.Matches(blob))
            {
                found.Add(match.Value);
            }
        }

        return found.Count >= 2;
    }

    private static string NoteText(ResolutionRequest request, string id)
    {
        var parts = new List<string>();
        foreach (var note in request.Notes)
        {
            var blob = NoteBlob(note);
            if ((note.ExternalRef is not null && note.ExternalRef.Equals(id, StringComparison.OrdinalIgnoreCase))
                || blob.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(blob);
            }
        }

        return string.Join('\n', parts);
    }

    private static string NoteBlob(EvidenceNote note)
    {
        return (note.Title ?? string.Empty) + "\n" + (note.Detail ?? string.Empty) + "\n" + (note.ExternalRef ?? string.Empty);
    }

    private static string[] DocumentOnlyCounters(IReadOnlyList<string> ecoIds)
    {
        return ecoIds.Select(id => "ECO " + id + " is a document-only note. Setpoints were not changed.").ToArray();
    }

    private static bool IsStep(ResolutionRequest request)
    {
        return string.Equals(request.SignalShape, "step", StringComparison.OrdinalIgnoreCase);
    }

    private static RankDecision Recommend(
        ResolutionRequest request,
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

        var catalog = WhyItMattersCatalog.Shared;
        var explanations = CitedEvidenceFactory.ForRequest(request, ids, catalog);
        var matrix = EvidenceMatrixBuilder.FromRequest(request, category, ids, catalog);
        var claim = new ResolutionClaim(category, cause, confidence, ids, counterEvidence, actions, [], explanations, matrix);
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

    private static bool Mentions(ResolutionRequest request, string phrase) =>
        request.PmSummaries.Any(summary => summary.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static string? First(string[] values)
    {
        return values.Length == 0 ? null : values[0];
    }
}
