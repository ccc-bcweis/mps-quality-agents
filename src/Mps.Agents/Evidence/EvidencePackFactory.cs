// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Agents.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Incidents;

namespace Mps.Agents.Evidence;

/// <summary>
/// Builds an evidence pack from tool rows. A MES recipe id is treated as a part name so a header revision can contradict PLM.
/// </summary>
public static class EvidencePackFactory
{
    public static GatheredFacts Build(Incident incident, ToolHarvest harvest)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(harvest);
        var items = new List<EvidenceItem>();
        var claims = new List<ConfigClaim>();
        var observations = new List<ImpactObservation>();
        var events = new List<AlignedEvent>();

        foreach (var violation in harvest.Violations)
        {
            var reference = string.Join(
                ':',
                "spc",
                violation.ToolId.Value,
                violation.Parameter,
                violation.At.ToString("O", CultureInfo.InvariantCulture),
                violation.Rule);
            items.Add(Item(incident, EvidenceSource.Spc, Mps.Application.Spc.SpcRuleNames.Plain(violation.Rule) + " on " + Mps.Application.Metadata.ParameterNames.Display(violation.Parameter), reference, violation.At, violation.ToolId.Value, Text($"{Mps.Application.Spc.SpcRuleNames.Plain(violation.Rule)} broke on {violation.ToolId.Value} {violation.ChamberId ?? string.Empty} {Mps.Application.Metadata.ParameterNames.Display(violation.Parameter)} at {Stamp(violation.At)}: value {Num(violation.Value)} (one sigma is {Num(violation.Sigma)}){LimitsText(violation)}, severity {violation.Severity}."), violation.ChamberId, violation.Value, Limits.From(violation)));
            observations.Add(new ImpactObservation(violation.ToolId.Value, violation.ChamberId, null, null));
        }

        var shape = ShapeOf(harvest.Telemetry, harvest.Violations);
        if (harvest.Telemetry is not null)
        {
            var window = harvest.Telemetry;
            var reference = string.Join(
                ':',
                "telemetry",
                window.ToolId.Value,
                window.Parameter,
                window.From.ToString("O", CultureInfo.InvariantCulture));
            var latest = window.Points.Count == 0 ? (double?)null : window.Points[^1].Value;
            var lastAt = window.Points.Count == 0 ? window.To : window.Points[^1].At;
            var judged = harvest.Violations.LastOrDefault(item => string.Equals(item.Parameter, window.Parameter, StringComparison.Ordinal));
            items.Add(Item(incident, EvidenceSource.Telemetry, Mps.Application.Metadata.ParameterNames.Display(window.Parameter) + " readings", reference, lastAt, window.ToolId.Value, DescribeWindow(window, shape), incident.ChamberId, latest, judged is null ? null : Limits.From(judged)));
        }

        foreach (var eco in harvest.Ecos)
        {
            items.Add(Item(incident, EvidenceSource.Plm, RuledOut.Title(eco.Title, RuledOut.Reason(eco)), eco.EcoId, eco.EffectiveAt, eco.ToolId, Text($"{eco.EcoId} \"{eco.Title}\" effective {Stamp(eco.EffectiveAt)} on tool {eco.ToolId ?? "fleet"}, recipe {eco.RecipeId ?? "any"}. Change: {eco.ChangeSummary}")));
            events.Add(new AlignedEvent("eco", eco.EcoId, eco.EffectiveAt));
        }

        if (harvest.Config is not null)
        {
            var config = harvest.Config;
            items.Add(Item(incident, EvidenceSource.Plm, config.PartName + " rev " + config.Revision, config.ConfigId, incident.OpenedAt, config.ToolId.Value, Text($"PLM configuration {config.ConfigId}: {config.ToolId.Value} carries {config.PartName} revision {config.Revision}.")));
            claims.Add(new ConfigClaim(EvidenceSource.Plm, config.PartName, config.Revision, config.ConfigId));
        }

        foreach (var run in harvest.Runs)
        {
            items.Add(Item(incident, EvidenceSource.Mes, run.RecipeId + " rev " + run.RecipeRevision, run.RunId, run.StartedAt, run.ToolId.Value, Text($"MES run {run.RunId} on {run.ToolId.Value} started {Stamp(run.StartedAt)}: recipe {run.RecipeId} revision {run.RecipeRevision}, lot {run.LotId}.")));
            claims.Add(new ConfigClaim(EvidenceSource.Mes, run.RecipeId, run.RecipeRevision, run.RunId));
            observations.Add(new ImpactObservation(run.ToolId.Value, null, run.RecipeId, run.LotId));
        }

        foreach (var lot in harvest.Lots)
        {
            items.Add(Item(incident, EvidenceSource.Mes, lot.MaterialCode, lot.LotId, incident.OpenedAt, lot.ToolId.Value, Text($"Lot {lot.LotId} ({lot.MaterialCode}) processed on {lot.ToolId.Value}.")));
            observations.Add(new ImpactObservation(lot.ToolId.Value, null, null, lot.LotId));
        }

        foreach (var ncr in harvest.Ncrs)
        {
            items.Add(Item(incident, EvidenceSource.Qms, ncr.Title, ncr.NcrId, ncr.OpenedAt, ncr.ToolId, Text($"NCR {ncr.NcrId} opened {Stamp(ncr.OpenedAt)} on {ncr.ToolId}: {ncr.Title}. Lot {ncr.LotId ?? "none"}.")));
        }

        foreach (var material in harvest.MaterialLots)
        {
            items.Add(Item(incident, EvidenceSource.Erp, material.MaterialCode + " " + material.LotId, material.LotId, material.ReceivedAt, incident.ToolId.Value, Text($"Material lot {material.LotId} ({material.MaterialCode}) from {material.SupplierName} received {Stamp(material.ReceivedAt)}; supplier notice {material.NoticeId ?? "none"}.")));
            events.Add(new AlignedEvent("lot", material.LotId, material.ReceivedAt));
            observations.Add(new ImpactObservation(incident.ToolId.Value, null, null, material.LotId));
        }

        foreach (var notice in harvest.Notices)
        {
            items.Add(Item(incident, EvidenceSource.Erp, RuledOut.Title(notice.Summary, RuledOut.Reason(notice)), notice.NoticeId, notice.IssuedAt, null, Text($"Supplier notice {notice.NoticeId} from {notice.SupplierName} issued {Stamp(notice.IssuedAt)} for {notice.MaterialCode} lot {notice.LotId}: {notice.Summary}")));
            events.Add(new AlignedEvent("notice", notice.NoticeId, notice.IssuedAt));
        }

        foreach (var order in harvest.WorkOrders)
        {
            items.Add(Item(incident, EvidenceSource.Cmms, RuledOut.Title(order.Summary, RuledOut.Reason(order)), order.WorkOrderId, order.PerformedAt, order.ToolId.Value, Text($"Work order {order.WorkOrderId} ({order.Kind}) on {order.ToolId.Value} {order.ChamberId ?? string.Empty} at {Stamp(order.PerformedAt)}: {order.Summary}"), order.ChamberId));
            events.Add(new AlignedEvent(order.Kind, order.WorkOrderId, order.PerformedAt));
        }

        foreach (var calibration in harvest.Calibrations)
        {
            var at = calibration.CompletedAt ?? calibration.DueAt;
            items.Add(Item(incident, EvidenceSource.Cmms, calibration.Instrument, calibration.CalibrationId, at, calibration.ToolId.Value, Text($"Calibration {calibration.CalibrationId} for {calibration.Instrument} on {calibration.ToolId.Value}: due {Stamp(calibration.DueAt)}, completed {(calibration.CompletedAt is { } done ? Stamp(done) : "never")}, {(calibration.IsOverdue ? "OVERDUE" : "in date")}.")));
            if (calibration.IsOverdue)
            {
                events.Add(new AlignedEvent("calibration", calibration.CalibrationId, calibration.DueAt));
            }
        }

        foreach (var document in harvest.Documents)
        {
            items.Add(Item(incident, EvidenceSource.Document, document.Title, document.DocumentId, incident.OpenedAt, null, Text($"{document.Title} (rev {document.Revision}): {document.Excerpt}")));
        }

        if (observations.Count == 0)
        {
            observations.Add(new ImpactObservation(incident.ToolId.Value, incident.ChamberId, null, null));
        }

        var catalog = WhyItMattersCatalog.Shared;
        var pack = new EvidencePack(
            items,
            claims,
            harvest.Ecos,
            harvest.WorkOrders,
            harvest.Calibrations,
            harvest.MaterialLots,
            harvest.Notices,
            harvest.CalibrationLookupCompleted,
            shape,
            CitedEvidenceFactory.ForItems(items, catalog));
        return new GatheredFacts(pack, observations, events, shape);
    }

    private static EvidenceItem Item(
        Incident incident,
        EvidenceSource source,
        string title,
        string externalRef,
        DateTimeOffset observedAt,
        string? toolId,
        string? excerpt = null,
        string? chamberId = null,
        double? value = null,
        Limits? limits = null)
    {
        var chamber = !string.IsNullOrWhiteSpace(chamberId)
            ? chamberId
            : toolId is not null && string.Equals(toolId, incident.ToolId.Value, StringComparison.Ordinal)
                ? incident.ChamberId
                : null;
        var reading = EvidenceReading.Place(toolId, chamber, value);
        if (limits is { } known)
        {
            reading = reading with { SpecLow = known.SpecLow, SpecHigh = known.SpecHigh, Center = known.Center, Sigma = known.Sigma };
        }

        return new EvidenceItem(
            EvidenceIds.Create(source, externalRef),
            source,
            title,
            string.IsNullOrWhiteSpace(excerpt) ? title : excerpt,
            observedAt,
            toolId,
            externalRef,
            reading.IsEmpty ? null : reading);
    }

    /// <summary>The recipe spec and control-chart centre and sigma a rule was judged against.</summary>
    private readonly record struct Limits(double? SpecLow, double? SpecHigh, double? Center, double? Sigma)
    {
        public static Limits From(Mps.Domain.Telemetry.SpcViolation violation) =>
            new(violation.LowerSpec, violation.UpperSpec, violation.Center, violation.Center is null ? null : violation.Sigma);
    }

    /// <summary>"; recipe spec 46-54, control limits 46-54 (centre 50)": the numbers the rule was judged against, when known.</summary>
    private static string LimitsText(Mps.Domain.Telemetry.SpcViolation violation)
    {
        var text = string.Empty;
        if (violation.LowerSpec is not null || violation.UpperSpec is not null)
        {
            text += "; recipe spec " + (violation.LowerSpec is double low ? Num(low) : "none") + " to " + (violation.UpperSpec is double high ? Num(high) : "none");
        }

        if (violation.Center is double center)
        {
            var (low, high) = Mps.Application.Spc.SpcRuleSettings.Shared.ControlLimits(center, violation.Sigma);
            text += "; control limits " + Num(low) + " to " + Num(high) + " (centre " + Num(center) + ")";
        }

        return text;
    }

    private static string Text(FormattableString value) => System.Text.RegularExpressions.Regex.Replace(value.ToString(CultureInfo.InvariantCulture), @"\s+", " ").Trim();

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Numbers the model needs to judge shape: baseline, latest, extremes and shift in baseline sigmas.</summary>
    public static string DescribeWindow(Mps.Domain.Telemetry.TelemetryWindow window, SignalShape shape = SignalShape.Unknown)
    {
        var points = window.Points;
        if (points.Count == 0)
        {
            return Text($"{Mps.Application.Metadata.ParameterNames.Display(window.Parameter)} on {window.ToolId.Value}: no samples between {Stamp(window.From)} and {Stamp(window.To)}.");
        }

        var head = points.Take(Math.Max(1, points.Count / 3)).Select(point => point.Value).ToArray();
        var tail = points.Skip(points.Count - Math.Max(1, points.Count / 5)).Select(point => point.Value).ToArray();
        var mean = head.Average();
        var sd = Math.Sqrt(head.Select(value => (value - mean) * (value - mean)).Average());
        var late = tail.Average();
        var shift = sd > 1e-9 ? (late - mean) / sd : 0d;
        return Text($"{Mps.Application.Metadata.ParameterNames.Display(window.Parameter)} on {window.ToolId.Value}, {points.Count} samples {Stamp(window.From)} to {Stamp(window.To)}: early mean {Num(mean)} (sd {Num(sd)}), late mean {Num(late)}, min {Num(points.Min(point => point.Value))}, max {Num(points.Max(point => point.Value))}, last {Num(points[^1].Value)}; late shift {Num(shift)} baseline sigmas. Shape from the data since the first rule break: {ShapeWords(shape)}.");
    }

    /// <summary>
    /// The shape of the signal since it started breaking rules. A long window is mostly normal readings, so the shape is
    /// read from the readings since the first violation (less a short lead-in), and from the whole window otherwise.
    /// </summary>
    public static SignalShape ShapeOf(Mps.Domain.Telemetry.TelemetryWindow? window, IReadOnlyList<Mps.Domain.Telemetry.SpcViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        if (window is null)
        {
            return SignalShape.Unknown;
        }

        // Judge the shape on reading minus recipe nominal, so a recipe step change does not hide or fake a shape.
        var nominals = window.Nominals is { } known && known.Count == window.Points.Count ? known : null;
        var points = window.Points
            .Select((point, index) => nominals is null ? point : new Mps.Domain.Telemetry.TelemetryPoint(point.At, point.Value - nominals[index]))
            .OrderBy(point => point.At)
            .ToArray();
        var values = points.Select(point => point.Value).ToArray();
        var lastSix = values.Skip(Math.Max(0, values.Length - 24)).ToArray();
        if (SignalShapeClassifier.IsSwinging(lastSix, values))
        {
            return SignalShape.Oscillation;
        }

        if (violations.Count > 0)
        {
            var onset = violations.Min(violation => violation.At).AddMinutes(-10);
            var recent = points.Where(point => point.At >= onset).ToArray();
            if (recent.Length >= 8)
            {
                var near = SignalShapeClassifier.Classify(recent);
                if (near != SignalShape.Unknown)
                {
                    return near;
                }
            }
        }

        return SignalShapeClassifier.Classify(points);
    }

    /// <summary>Plain words for the shape, so a summary cannot call an oscillation a gradual rise.</summary>
    public static string ShapeWords(SignalShape shape) => shape switch
    {
        SignalShape.Step => "a step (a sudden jump that stays)",
        SignalShape.Ramp => "a gradual drift in one direction",
        SignalShape.Oscillation => "an oscillation (the reading swings up and down repeatedly)",
        SignalShape.Intermittent => "intermittent spikes that come and go",
        _ => "no clear shape",
    };
}
