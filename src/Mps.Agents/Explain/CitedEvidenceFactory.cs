// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Ranking;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Agents.Explain;

/// <summary>
/// Builds whyItMatters for every cited evidence id. Sensor evidence also gets a signal id.
/// </summary>
public static class CitedEvidenceFactory
{
    public static IReadOnlyList<CitedEvidence> ForItems(IReadOnlyList<EvidenceItem> items, WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        return items.Select(catalog.ExplainItem).ToArray();
    }

    public static IReadOnlyList<CitedEvidence> ForRequest(
        ResolutionRequest request,
        IReadOnlyList<EvidenceId> cited,
        WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cited);
        ArgumentNullException.ThrowIfNull(catalog);
        var list = new List<CitedEvidence>(cited.Count);
        foreach (var id in cited)
        {
            list.Add(ForOne(request, id, catalog));
        }

        return list;
    }

    private static CitedEvidence ForOne(ResolutionRequest request, EvidenceId id, WhyItMattersCatalog catalog)
    {
        var note = request.Notes.FirstOrDefault(item => Guid.TryParse(item.Id, out var parsed) && parsed == id.Value);
        if (note is not null)
        {
            return catalog.Explain(id, note.Source, note.Parameter, note.Title, note.ExternalRef, note.SignalId);
        }

        if (Contains(request.SignalEvidenceIds, id))
        {
            return catalog.Explain(id, "telemetry", null, null, null, null);
        }

        if (Contains(request.EcoEvidenceIds, id))
        {
            return catalog.Explain(id, "plm", null, null, null, null);
        }

        if (Contains(request.OverdueEvidenceIds, id) || Contains(request.AlignmentEvidenceIds, id))
        {
            return catalog.Explain(id, "cmms", null, "calibration", null, null);
        }

        if (Contains(request.PmEvidenceIds, id))
        {
            return catalog.Explain(id, "cmms", null, "maintenance", null, null);
        }

        if (Contains(request.LotEvidenceIds, id))
        {
            return catalog.Explain(id, "erp", null, null, null, null);
        }

        return new CitedEvidence(id, WhyItMattersCatalog.GenericSentence, null);
    }

    private static bool Contains(IReadOnlyList<string>? values, EvidenceId id)
    {
        if (values is null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var parsed) && parsed == id.Value)
            {
                return true;
            }
        }

        return false;
    }
}
