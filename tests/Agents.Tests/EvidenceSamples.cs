// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

internal static class EvidenceSamples
{
    internal static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    internal static readonly ToolId Tool = new("MPS-E7");

    internal static EvidenceId Id(int number)
    {
        return new EvidenceId(Guid.Parse($"00000000-0000-0000-0000-{number:D12}"));
    }

    internal static EvidenceItem Item(
        int number,
        EvidenceSource source,
        string title,
        string? externalRef = null,
        DateTimeOffset? observedAt = null)
    {
        return new EvidenceItem(Id(number), source, title, title, observedAt ?? At, Tool.Value, externalRef);
    }

    internal static EvidencePack Pack(
        IReadOnlyList<EvidenceItem>? items = null,
        IReadOnlyList<ConfigClaim>? configClaims = null,
        IReadOnlyList<EcoRecord>? ecos = null,
        IReadOnlyList<WorkOrder>? workOrders = null,
        IReadOnlyList<CalibrationRecord>? calibrations = null,
        IReadOnlyList<MaterialLot>? materialLots = null,
        IReadOnlyList<SupplierNotice>? supplierNotices = null,
        bool calibrationLookupCompleted = false,
        SignalShape signalShape = SignalShape.Unknown)
    {
        return new EvidencePack(
            items ?? [],
            configClaims ?? [],
            ecos ?? [],
            workOrders ?? [],
            calibrations ?? [],
            materialLots ?? [],
            supplierNotices ?? [],
            calibrationLookupCompleted,
            signalShape);
    }
}
