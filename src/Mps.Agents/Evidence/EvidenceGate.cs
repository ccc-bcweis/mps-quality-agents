// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;

namespace Mps.Agents.Evidence;

/// <summary>
/// Decides whether evidence is sufficient, incomplete, or contradictory. A conflict escalates. A gap requests information.
/// An empty calibration lookup is named on an escalation and is not itself a gap when another discriminator is present.
/// </summary>
public static class EvidenceGate
{
    public static GateResult Evaluate(EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var gaps = new List<string>();
        if (!HasSignal(pack))
        {
            gaps.Add("missing-signal");
        }

        if (!HasContext(pack))
        {
            gaps.Add("missing-context");
        }

        if (pack.Items.Count < 2)
        {
            gaps.Add("insufficient-count");
        }

        if (!HasDiscriminator(pack))
        {
            gaps.Add("missing-discriminator");
        }

        var conflicts = new List<string>();
        conflicts.AddRange(ConfigConflicts(pack.ConfigClaims));
        conflicts.AddRange(TimestampConflicts(pack.Items));

        if (conflicts.Count > 0)
        {
            var parts = new List<string>(conflicts);
            if (gaps.Count > 0)
            {
                parts.Add("gaps: " + string.Join(", ", gaps));
            }

            if (pack.CalibrationLookupCompleted && pack.Calibrations.Count == 0)
            {
                parts.Add("calibration records missing");
            }

            return new GateResult(GateOutcome.Escalate, gaps, conflicts, string.Join("; ", parts));
        }

        if (gaps.Count > 0)
        {
            return new GateResult(GateOutcome.RequestInfo, gaps, conflicts, "request_info: " + string.Join(", ", gaps));
        }

        return new GateResult(GateOutcome.Sufficient, gaps, conflicts, null);
    }

    private static bool HasSignal(EvidencePack pack)
    {
        return pack.Items.Any(item => item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry);
    }

    private static bool HasContext(EvidencePack pack)
    {
        return pack.Items.Any(item => item.Source is EvidenceSource.Mes or EvidenceSource.Plm or EvidenceSource.Erp or EvidenceSource.Cmms);
    }

    private static bool HasDiscriminator(EvidencePack pack)
    {
        if (pack.Ecos.Count > 0)
        {
            return true;
        }

        if (pack.Calibrations.Any(record => record.IsOverdue))
        {
            return true;
        }

        if (pack.WorkOrders.Any(IsAlignment))
        {
            return true;
        }

        if (pack.MaterialLots.Count > 0 || pack.SupplierNotices.Count > 0)
        {
            return true;
        }

        return IsRampWithoutDiscreteEvent(pack);
    }

    private static bool IsRampWithoutDiscreteEvent(EvidencePack pack)
    {
        return pack.SignalShape == SignalShape.Ramp
            && pack.Ecos.Count == 0
            && !pack.Calibrations.Any(record => record.IsOverdue)
            && !pack.WorkOrders.Any(IsAlignment)
            && pack.MaterialLots.Count == 0
            && pack.SupplierNotices.Count == 0;
    }

    private static bool IsAlignment(WorkOrder order)
    {
        return order.Kind.Contains("align", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ConfigConflicts(IReadOnlyList<ConfigClaim> claims)
    {
        var conflicts = new List<string>();
        var groups = claims
            .GroupBy(claim => claim.PartName.Trim().ToUpperInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var members = group.ToArray();
            for (var i = 0; i < members.Length; i++)
            {
                for (var j = i + 1; j < members.Length; j++)
                {
                    var left = members[i];
                    var right = members[j];
                    if (left.Source == right.Source)
                    {
                        continue;
                    }

                    if (string.Equals(left.Revision.Trim(), right.Revision.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    conflicts.Add(
                        "config-revision: "
                        + left.PartName
                        + " rev "
                        + left.Revision
                        + " ("
                        + left.Source
                        + ") vs rev "
                        + right.Revision
                        + " ("
                        + right.Source
                        + ")");
                }
            }
        }

        return conflicts.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static List<string> TimestampConflicts(IReadOnlyList<EvidenceItem> items)
    {
        var conflicts = new List<string>();
        var groups = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalRef))
            .GroupBy(item => item.ExternalRef!.Trim().ToUpperInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var ordered = group.OrderBy(item => item.ObservedAt.UtcDateTime).ToArray();
            var instants = ordered.Select(item => item.ObservedAt.UtcDateTime).Distinct().ToArray();
            if (instants.Length < 2)
            {
                continue;
            }

            var shown = new List<string>();
            DateTime? previous = null;
            foreach (var item in ordered)
            {
                if (previous == item.ObservedAt.UtcDateTime)
                {
                    continue;
                }

                previous = item.ObservedAt.UtcDateTime;
                shown.Add(item.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
            }

            conflicts.Add("timestamp-conflict: " + ordered[0].ExternalRef + " at " + string.Join(" and ", shown));
        }

        return conflicts;
    }
}
