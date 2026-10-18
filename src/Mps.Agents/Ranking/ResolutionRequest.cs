// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json.Serialization;

using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Impact;

namespace Mps.Agents.Ranking;

/// <summary>
/// Features and citations the ranker uses. The resolution prompt carries this JSON.
/// </summary>
public sealed class ResolutionRequest
{
    public string ScopeKind { get; set; } = "tool";

    public string ScopeKey { get; set; } = string.Empty;

    public string SignalShape { get; set; } = "unknown";

    /// <summary>Which class a cited record implies, from data/metadata/category-evidence.yaml ("description: class").</summary>
    public string[] CategoryRules { get; set; } = [];

    /// <summary>The shape ids the reply's "shape" field may use, from the signal shape catalog.</summary>
    public string[] ShapeOptions { get; set; } = [];

    public string[] EcoIds { get; set; } = [];

    public string[] EcoEvidenceIds { get; set; } = [];

    public string[] PmSummaries { get; set; } = [];

    public string[] OverdueInstruments { get; set; } = [];

    public string[] OverdueEvidenceIds { get; set; } = [];

    public string[] AlignmentSummaries { get; set; } = [];

    public string[] AlignmentEvidenceIds { get; set; } = [];

    public string[] LotIds { get; set; } = [];

    public string[] LotEvidenceIds { get; set; } = [];

    public string[] NoticeIds { get; set; } = [];

    public string[] SignalEvidenceIds { get; set; } = [];

    public string[] PmEvidenceIds { get; set; } = [];

    /// <summary>What people added in the evidence loop (source userProvided), in the order they added it.</summary>
    public string[] UserEvidence { get; set; } = [];

    public string[] UserEvidenceIds { get; set; } = [];

    public CandidateNote[] Candidates { get; set; } = [];

    public RuledOutNote[] RuledOut { get; set; } = [];

    public EvidenceNote[] Notes { get; set; } = [];

    /// <summary>Handle to pack id for a request produced by <see cref="WithHandles"/>. Absent on the wire.</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, string> HandleMap { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set only on a retry: what was wrong with the previous answer for this same pack.</summary>
    public string? Feedback { get; set; }

    public static ResolutionRequest From(EvidencePack pack, ImpactFeatures features)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(features);
        var catalog = WhyItMattersCatalog.Shared;
        return new ResolutionRequest
        {
            ScopeKind = ScopeName(features.Kind),
            ScopeKey = features.Key,
            SignalShape = SignalShapeCatalog.Shared.Of(features.Shape).Id,
            ShapeOptions = [.. SignalShapeCatalog.Shared.Ids],
            CategoryRules = Mps.Agents.Grounding.CategoryEvidenceRules.Shared.Rules
                .Select(rule => rule.Description + ": " + System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(rule.Implies.ToString()))
                .ToArray(),
            EcoIds = pack.Ecos.Select(eco => eco.EcoId).ToArray(),
            EcoEvidenceIds = Ids(pack, pack.Ecos.Select(eco => eco.EcoId)),
            PmSummaries = pack.WorkOrders.Where(order => !IsAlignment(order)).Select(DescribeOrder).ToArray(),
            OverdueInstruments = pack.Calibrations.Where(record => record.IsOverdue).Select(record => record.Instrument).ToArray(),
            OverdueEvidenceIds = Ids(pack, pack.Calibrations.Where(record => record.IsOverdue).Select(record => record.CalibrationId)),
            AlignmentSummaries = pack.WorkOrders.Where(IsAlignment).Select(DescribeOrder).ToArray(),
            AlignmentEvidenceIds = Ids(pack, pack.WorkOrders.Where(IsAlignment).Select(order => order.WorkOrderId)),
            LotIds = pack.MaterialLots.Select(lot => lot.LotId).ToArray(),
            LotEvidenceIds = Ids(pack, pack.MaterialLots.Select(lot => lot.LotId)),
            NoticeIds = pack.SupplierNotices.Select(notice => notice.NoticeId).ToArray(),
            SignalEvidenceIds = pack.Items
                .Where(item => item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry)
                .Select(item => item.Id.ToString())
                .ToArray(),
            PmEvidenceIds = Ids(pack, pack.WorkOrders.Where(order => !IsAlignment(order)).Select(order => order.WorkOrderId)),
            UserEvidence = pack.Items.Where(item => item.Source == EvidenceSource.UserProvided).Select(item => item.Excerpt).ToArray(),
            UserEvidenceIds = pack.Items.Where(item => item.Source == EvidenceSource.UserProvided).Select(item => item.Id.ToString()).ToArray(),
            Notes = pack.Items.Select(item => new EvidenceNote
            {
                Id = item.Id.ToString(),
                Source = SourceName(item.Source),
                Title = item.Title,
                Detail = item.Excerpt,
                Parameter = WhyItMattersCatalog.ParameterOf(item),
                SignalId = catalog.SignalIdFor(item),
                ExternalRef = item.ExternalRef,
                Status = item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry ? "context" : "candidate",
            }).ToArray(),
        };
    }

    /// <summary>
    /// Copy that shows E01, E02, ... instead of pack ids. Handles follow note order: two digits through 99, then three.
    /// </summary>
    public ResolutionRequest WithHandles()
    {
        var notes = Notes ?? [];
        var idToHandle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var handleToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var copiedNotes = new EvidenceNote[notes.Length];
        for (var index = 0; index < notes.Length; index++)
        {
            var note = notes[index] ?? new EvidenceNote();
            var handle = FormatHandle(index + 1);
            var realId = note.Id ?? string.Empty;
            if (realId.Length > 0 && !idToHandle.ContainsKey(realId))
            {
                idToHandle.Add(realId, handle);
            }

            handleToId[handle] = realId;
            copiedNotes[index] = CopyNote(note, handle);
        }

        return new ResolutionRequest
        {
            ScopeKind = ScopeKind,
            ScopeKey = ScopeKey,
            SignalShape = SignalShape,
            CategoryRules = Clone(CategoryRules),
            ShapeOptions = Clone(ShapeOptions),
            EcoIds = Clone(EcoIds),
            EcoEvidenceIds = MapIds(EcoEvidenceIds, idToHandle),
            PmSummaries = Clone(PmSummaries),
            OverdueInstruments = Clone(OverdueInstruments),
            OverdueEvidenceIds = MapIds(OverdueEvidenceIds, idToHandle),
            AlignmentSummaries = Clone(AlignmentSummaries),
            AlignmentEvidenceIds = MapIds(AlignmentEvidenceIds, idToHandle),
            LotIds = Clone(LotIds),
            LotEvidenceIds = MapIds(LotEvidenceIds, idToHandle),
            NoticeIds = Clone(NoticeIds),
            SignalEvidenceIds = MapIds(SignalEvidenceIds, idToHandle),
            PmEvidenceIds = MapIds(PmEvidenceIds, idToHandle),
            UserEvidence = Clone(UserEvidence),
            UserEvidenceIds = MapIds(UserEvidenceIds, idToHandle),
            Candidates = MapCandidates(Candidates, idToHandle),
            RuledOut = MapRuledOut(RuledOut, idToHandle),
            Notes = copiedNotes,
            Feedback = Feedback,
            HandleMap = handleToId,
        };
    }

    /// <summary>Pack id for a handle, or the same id when it is already a pack id. Null when it is neither.</summary>
    public string? ResolveHandle(string idOrHandle)
    {
        if (string.IsNullOrWhiteSpace(idOrHandle))
        {
            return null;
        }

        var token = idOrHandle.Trim();
        if (HandleMap.TryGetValue(token, out var mapped))
        {
            return string.IsNullOrEmpty(mapped) ? null : mapped;
        }

        foreach (var id in HandleMap.Values)
        {
            if (id.Length > 0 && string.Equals(id, token, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return ListedId(token);
    }

    private static string FormatHandle(int number)
    {
        return "E" + number.ToString(number > 99 ? "D3" : "D2", CultureInfo.InvariantCulture);
    }

    private static EvidenceNote CopyNote(EvidenceNote note, string id)
    {
        return new EvidenceNote
        {
            Id = id,
            Source = note.Source,
            Title = note.Title,
            Detail = note.Detail,
            Parameter = note.Parameter,
            SignalId = note.SignalId,
            ExternalRef = note.ExternalRef,
            Status = note.Status,
        };
    }

    private static string MapOne(string? id, Dictionary<string, string> idToHandle)
    {
        if (string.IsNullOrEmpty(id))
        {
            return id ?? string.Empty;
        }

        return idToHandle.TryGetValue(id, out var handle) ? handle : id;
    }

    private static string[] MapIds(string[]? ids, Dictionary<string, string> idToHandle)
    {
        if (ids is null || ids.Length == 0)
        {
            return [];
        }

        var copy = new string[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            copy[index] = MapOne(ids[index], idToHandle);
        }

        return copy;
    }

    private static string[] Clone(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var copy = new string[values.Length];
        values.CopyTo(copy, 0);
        return copy;
    }

    private static CandidateNote[] MapCandidates(CandidateNote[]? notes, Dictionary<string, string> idToHandle)
    {
        if (notes is null || notes.Length == 0)
        {
            return [];
        }

        var copy = new CandidateNote[notes.Length];
        for (var index = 0; index < notes.Length; index++)
        {
            var note = notes[index];
            copy[index] = note is null
                ? new CandidateNote()
                : new CandidateNote
                {
                    Id = MapOne(note.Id, idToHandle),
                    Class = note.Class,
                    Kind = note.Kind,
                };
        }

        return copy;
    }

    private static RuledOutNote[] MapRuledOut(RuledOutNote[]? notes, Dictionary<string, string> idToHandle)
    {
        if (notes is null || notes.Length == 0)
        {
            return [];
        }

        var copy = new RuledOutNote[notes.Length];
        for (var index = 0; index < notes.Length; index++)
        {
            var note = notes[index];
            copy[index] = note is null
                ? new RuledOutNote()
                : new RuledOutNote
                {
                    Id = MapOne(note.Id, idToHandle),
                    Class = note.Class,
                    Reason = note.Reason,
                };
        }

        return copy;
    }

    private string? ListedId(string token)
    {
        foreach (var id in ListedIds())
        {
            if (!string.IsNullOrEmpty(id) && string.Equals(id, token, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private IEnumerable<string?> ListedIds()
    {
        foreach (var note in Notes ?? [])
        {
            yield return note?.Id;
        }

        foreach (var note in Candidates ?? [])
        {
            yield return note?.Id;
        }

        foreach (var note in RuledOut ?? [])
        {
            yield return note?.Id;
        }

        foreach (var id in EcoEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in OverdueEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in AlignmentEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in LotEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in PmEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in UserEvidenceIds ?? [])
        {
            yield return id;
        }

        foreach (var id in SignalEvidenceIds ?? [])
        {
            yield return id;
        }
    }

    private static string SourceName(EvidenceSource source)
    {
        return source switch
        {
            EvidenceSource.Telemetry => "telemetry",
            EvidenceSource.Spc => "spc",
            EvidenceSource.Mes => "mes",
            EvidenceSource.Qms => "qms",
            EvidenceSource.Plm => "plm",
            EvidenceSource.Erp => "erp",
            EvidenceSource.Cmms => "cmms",
            EvidenceSource.Ticketing => "ticketing",
            EvidenceSource.ManualMeasurement => "manualMeasurement",
            EvidenceSource.UserProvided => "userProvided",
            _ => "document",
        };
    }

    private static string[] Ids(EvidencePack pack, IEnumerable<string> externalRefs)
    {
        var wanted = new HashSet<string>(externalRefs, StringComparer.OrdinalIgnoreCase);
        return pack.Items
            .Where(item => item.ExternalRef is not null && wanted.Contains(item.ExternalRef))
            .Select(item => item.Id.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsAlignment(WorkOrder order)
    {
        return order.Kind.Contains("align", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeOrder(WorkOrder order)
    {
        return order.WorkOrderId + " " + order.Kind + ": " + order.Summary;
    }

    private static string ScopeName(ImpactScopeKind kind)
    {
        return kind switch
        {
            ImpactScopeKind.Chamber => "chamber",
            ImpactScopeKind.Recipe => "recipe",
            ImpactScopeKind.MaterialLot => "materialLot",
            _ => "tool",
        };
    }
}

/// <summary>
/// One evidence row the resolution prompt can cite. The model does not invent ids.
/// </summary>
public sealed class EvidenceNote
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Full record text: values, dates and change details. The model reasons from this, not the title.</summary>
    public string? Detail { get; set; }

    public string? Parameter { get; set; }

    public string? SignalId { get; set; }

    public string? ExternalRef { get; set; }

    private string? _status = "context";

    /// <summary>candidate, ruledOut, or context. A missing or blank value reads as context.</summary>
    public string Status
    {
        get => string.IsNullOrWhiteSpace(_status) ? "context" : _status;
        set => _status = value;
    }
}

/// <summary>One record that can support a class. Class is the camelCase category id.</summary>
public sealed class CandidateNote
{
    public string Id { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}

/// <summary>One record the plant data already rules out. Class is the camelCase category id.</summary>
public sealed class RuledOutNote
{
    public string Id { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
