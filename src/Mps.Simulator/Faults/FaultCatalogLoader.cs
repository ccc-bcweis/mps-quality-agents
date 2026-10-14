// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Faults;

/// <summary>
/// Loads <c>data/faults/*.yaml</c>. Each entry names the signal and the enterprise records its writer must create.
/// </summary>
public static class FaultCatalogLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static FaultCatalog Load(string directory, FleetCatalog fleet)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new FaultCatalogException([$"Fault directory does not exist: {directory}"]);
        }

        var errors = new List<string>();
        var faults = new List<FaultDefinition>();
        foreach (var file in Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            var source = Path.GetFileName(file);
            FaultFileDto dto;
            try
            {
                dto = Deserializer.Deserialize<FaultFileDto>(File.ReadAllText(file)) ?? new FaultFileDto();
            }
            catch (Exception exception)
            {
                errors.Add($"{source}: {exception.Message}");
                continue;
            }

            if (TryMap(dto, source, fleet, errors, out var fault))
            {
                faults.Add(fault);
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fault in faults)
        {
            if (!ids.Add(fault.Id))
            {
                errors.Add($"Duplicate fault id {fault.Id}.");
            }
        }

        foreach (var category in FaultCategories.All)
        {
            if (!faults.Exists(fault => fault.Category == category))
            {
                errors.Add($"The catalog is missing a {category} fault.");
            }
        }

        if (!faults.Exists(fault => fault.Evidence.Contradictory) || !faults.Exists(fault => fault.Evidence.MissingEvidence))
        {
            errors.Add("The catalog needs a contradictory fault and a missing-evidence fault.");
        }

        if (errors.Count > 0)
        {
            throw new FaultCatalogException(errors);
        }

        return new FaultCatalog(faults.OrderBy(fault => fault.Id, StringComparer.Ordinal).ToArray());
    }

    private static bool TryMap(FaultFileDto dto, string source, FleetCatalog fleet, List<string> errors, out FaultDefinition fault)
    {
        fault = null!;
        var before = errors.Count;
        var id = Text(dto.Id, source, "id", errors);
        var category = Text(dto.Category, source, "category", errors);
        var behavior = Text(dto.ExpectedBehavior, source, "expectedBehavior", errors);
        var parameter = Text(dto.Parameter, source, "parameter", errors);
        var cause = Text(dto.Cause, source, "cause", errors);
        var title = Text(dto.Title, source, "title", errors);
        var scenario = Text(dto.Scenario, source, "scenario", errors);
        if (!FaultCategories.All.Contains(category, StringComparer.Ordinal))
        {
            errors.Add($"{source}: category '{dto.Category}' is not one of the four root-cause categories.");
        }

        if (!FaultCategories.Behaviors.Contains(behavior, StringComparer.Ordinal))
        {
            errors.Add($"{source}: expectedBehavior must be recommend, escalate, or requestInfo.");
        }

        if (!TryShape(dto.Shape, out var shape))
        {
            errors.Add($"{source}: shape must be step, ramp, oscillation, or intermittent.");
        }

        if (!TryMagnitude(dto.Magnitude, out var magnitudeKind, out var magnitude))
        {
            errors.Add($"{source}: magnitude.kind (sigma or percent) and magnitude.value are required.");
        }

        if (!TryTime(dto.Onset, out var onset))
        {
            errors.Add($"{source}: onset must be an ISO-8601 timestamp.");
        }

        var period = dto.PeriodSeconds ?? 30d;
        var ramp = dto.RampSeconds ?? 600d;
        var duty = dto.DutyCycle ?? 0.5d;
        if (period <= 0 || ramp <= 0 || duty is < 0d or > 1d)
        {
            errors.Add($"{source}: period, ramp, and duty cycle are out of range.");
        }

        var tools = dto.Tools?.Where(tool => !string.IsNullOrWhiteSpace(tool)).Select(tool => tool.Trim()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (tools.Length == 0)
        {
            errors.Add($"{source}: at least one tool is required.");
        }

        foreach (var toolId in tools)
        {
            if (!fleet.Tools.Any(tool => string.Equals(tool.Id, toolId, StringComparison.Ordinal)))
            {
                errors.Add($"{source}: tool {toolId} is not in the fleet.");
                continue;
            }

            var tool = fleet.GetRequired(toolId);
            if (!FamilyParameterCatalog.For(tool.Family).ContainsKey(parameter))
            {
                errors.Add($"{source}: {parameter} is not a {tool.Family} parameter.");
            }
        }

        var chamberId = string.IsNullOrWhiteSpace(dto.ChamberId) ? null : dto.ChamberId.Trim();
        if (chamberId is not null)
        {
            if (tools.Length != 1 || !fleet.Tools.Any(tool => tool.Id == tools[0] && tool.Chambers.Any(chamber => chamber.Id == chamberId)))
            {
                errors.Add($"{source}: chamber {chamberId} must belong to the single listed tool.");
            }
        }

        var evidence = MapEvidence(dto.Evidence, source, errors);
        var notes = new List<EvidenceNote>();
        CollectNotes(dto, id, source, errors, notes);
        if (evidence.Eco?.ToolId is { } ecoTool && !tools.Contains(ecoTool, StringComparer.Ordinal))
        {
            errors.Add($"{source}: ECO tool {ecoTool} is not one of the fault tools.");
        }

        if (category == FaultCategories.Material && tools.Any(tool => !evidence.MaterialToolIds.Contains(tool, StringComparer.Ordinal)))
        {
            errors.Add($"{source}: the material lot must be linked to every fault tool.");
        }

        ValidateCategory(source, category, behavior, dto.AssertCategory ?? true, evidence, onset, errors);
        if (errors.Count > before)
        {
            return false;
        }

        fault = new FaultDefinition(
            id,
            title,
            category,
            scenario,
            behavior,
            dto.AssertCategory ?? true,
            tools,
            chamberId,
            parameter,
            shape,
            magnitudeKind,
            magnitude,
            onset,
            period,
            ramp,
            duty,
            cause,
            dto.CounterEvidence ?? [],
            dto.Actions ?? [],
            evidence,
            notes.First(note => note.Kind == "fault").WhyItMatters,
            notes);
        return true;
    }

    private static void CollectNotes(FaultFileDto dto, string faultId, string source, List<string> errors, List<EvidenceNote> notes)
    {
        AddNote(notes, "fault", faultId, dto.WhyItMatters, source, "whyItMatters", errors);
        var evidence = dto.Evidence;
        if (evidence is null)
        {
            return;
        }

        if (evidence.Eco is not null)
        {
            AddNote(notes, "eco", evidence.Eco.EcoId, evidence.Eco.WhyItMatters, source, "evidence.eco.whyItMatters", errors);
        }

        if (evidence.Config is not null)
        {
            AddNote(notes, "config", evidence.Config.ConfigId, evidence.Config.WhyItMatters, source, "evidence.config.whyItMatters", errors);
        }

        if (evidence.Calibration is not null)
        {
            AddNote(notes, "calibration", evidence.Calibration.CalibrationId, evidence.Calibration.WhyItMatters, source, "evidence.calibration.whyItMatters", errors);
        }

        if (evidence.MaterialLot is not null)
        {
            AddNote(notes, "materialLot", evidence.MaterialLot.LotId, evidence.MaterialLot.WhyItMatters, source, "evidence.materialLot.whyItMatters", errors);
        }

        if (evidence.SupplierNotice is not null)
        {
            AddNote(notes, "supplierNotice", evidence.SupplierNotice.NoticeId, evidence.SupplierNotice.WhyItMatters, source, "evidence.supplierNotice.whyItMatters", errors);
        }

        if (evidence.WorkOrder is not null)
        {
            AddNote(notes, "workOrder", evidence.WorkOrder.WorkOrderId, evidence.WorkOrder.WhyItMatters, source, "evidence.workOrder.whyItMatters", errors);
        }

        if (evidence.Runs is not null)
        {
            AddNote(notes, "runs", evidence.Runs.LotId, evidence.Runs.WhyItMatters, source, "evidence.runs.whyItMatters", errors);
        }

        foreach (var confounder in evidence.Confounders ?? [])
        {
            AddNote(notes, "confounder", ConfounderRecordId(confounder), confounder.WhyItMatters, source, "evidence.confounder.whyItMatters", errors);
        }

        foreach (var missing in evidence.Missing ?? [])
        {
            AddNote(notes, "missing", missing.RecordId, missing.WhyItMatters, source, "evidence.missing.whyItMatters", errors);
        }

        if (evidence.Contradiction is not null)
        {
            AddNote(notes, "contradiction", "contradiction", evidence.Contradiction.WhyItMatters, source, "evidence.contradiction.whyItMatters", errors);
        }
    }

    private static string? ConfounderRecordId(ConfounderDto confounder)
    {
        return confounder.Kind switch
        {
            "pm" => confounder.WorkOrderId,
            "supplierLot" => confounder.Notice?.NoticeId ?? confounder.Material?.LotId,
            "eco" => confounder.Eco?.EcoId,
            _ => null,
        };
    }

    private static void AddNote(List<EvidenceNote> notes, string kind, string? recordId, string? whyItMatters, string source, string name, List<string> errors)
    {
        var line = OneLine(whyItMatters, source, name, errors);
        if (string.IsNullOrWhiteSpace(recordId) || line is null)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                errors.Add($"{source}: {name} needs a record id.");
            }

            return;
        }

        notes.Add(new EvidenceNote(kind, recordId.Trim(), line));
    }

    private static string? OneLine(string? value, string source, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{source}: {name} is required.");
            return null;
        }

        var text = value.Trim();
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal) || text.Length > 220)
        {
            errors.Add($"{source}: {name} must be one line of at most 220 characters.");
            return null;
        }

        foreach (var token in FaultPlainLanguage.HiddenLabels)
        {
            if (text.Contains(token, StringComparison.Ordinal))
            {
                errors.Add($"{source}: {name} must stay in plain language.");
                return null;
            }
        }

        return text;
    }

    private static void ValidateCategory(string source, string category, string behavior, bool assertCategory, FaultEvidence? evidence, DateTimeOffset onset, List<string> errors)
    {
        if (evidence is null)
        {
            errors.Add($"{source}: evidence is required.");
            return;
        }

        if (behavior == FaultCategories.Recommend && (!assertCategory || evidence.Contradictory || evidence.MissingEvidence))
        {
            errors.Add($"{source}: a recommend fault asserts its category and does not drop or contradict evidence.");
        }

        if (behavior == FaultCategories.Escalate && !evidence.Contradictory && !evidence.MissingEvidence)
        {
            errors.Add($"{source}: an escalate fault names a contradiction or a missing record.");
        }

        if (category == FaultCategories.EngineeringChange && evidence.Eco is null)
        {
            errors.Add($"{source}: an engineering-change fault requires an ECO.");
        }

        if (evidence.Eco is not null && evidence.Eco.EffectiveAt > onset)
        {
            errors.Add($"{source}: ECO effective time is after onset.");
        }

        if (category == FaultCategories.Calibration && evidence.Calibration is null && !evidence.Missing.Any(record => record.RecordType == "calibration"))
        {
            errors.Add($"{source}: a calibration fault requires a calibration record or a missing calibration id.");
        }

        if (evidence.Calibration is not null && (!evidence.Calibration.IsOverdue || evidence.Calibration.DueAt >= onset || evidence.Calibration.CompletedAt is not null))
        {
            errors.Add($"{source}: the calibration record must be overdue, due before onset, and not completed.");
        }

        if (category == FaultCategories.Material && (evidence.MaterialLot is null || evidence.Notice is null))
        {
            errors.Add($"{source}: a material fault requires a lot and a supplier notice.");
        }

        if (evidence.MaterialLot is not null && evidence.Notice is not null)
        {
            if (!string.Equals(evidence.MaterialLot.LotId, evidence.Notice.LotId, StringComparison.Ordinal))
            {
                errors.Add($"{source}: the supplier notice lot does not match the material lot.");
            }

            if (evidence.MaterialLot.ReceivedAt > onset || evidence.Notice.IssuedAt > onset)
            {
                errors.Add($"{source}: the material lot and notice must be on or before onset.");
            }
        }

        if (category == FaultCategories.Drift)
        {
            if (evidence.WorkOrder is null || evidence.Eco is not null || evidence.Calibration is not null || evidence.MaterialLot is not null || evidence.Notice is not null || evidence.Confounders.Count > 0)
            {
                errors.Add($"{source}: process drift has a prior clean and no discrete ECO, calibration, lot, or confounder.");
            }
        }

        if (evidence.WorkOrder is not null && evidence.WorkOrder.PerformedAt >= onset && category == FaultCategories.Drift)
        {
            errors.Add($"{source}: the drift clean must be before onset.");
        }

        if (evidence.Contradictory)
        {
            if (evidence.Contradiction is null || evidence.Config is null || evidence.Runs is null)
            {
                errors.Add($"{source}: a contradiction needs a PLM config, MES runs, and a conflict note.");
            }
            else if (string.Equals(evidence.Contradiction.PlmRevision, evidence.Contradiction.MesRevision, StringComparison.Ordinal)
                || !string.Equals(evidence.Config.Revision, evidence.Contradiction.PlmRevision, StringComparison.Ordinal)
                || !string.Equals(evidence.Runs.RecipeRevision, evidence.Contradiction.MesRevision, StringComparison.Ordinal))
            {
                errors.Add($"{source}: PLM revision and MES run revision must disagree and match the conflict note.");
            }
        }

        if (evidence.MissingEvidence && evidence.Missing.Count == 0)
        {
            errors.Add($"{source}: missing evidence needs at least one record id.");
        }
    }

    private static FaultEvidence MapEvidence(EvidenceDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            errors.Add($"{source}: evidence is required.");
            return new FaultEvidence(null, null, null, null, [], null, null, null, [], [], null, false, false);
        }

        var confounders = new List<Confounder>();
        foreach (var confounder in dto.Confounders ?? [])
        {
            var mapped = MapConfounder(confounder, source, errors);
            if (mapped is not null)
            {
                confounders.Add(mapped);
            }
        }

        var missing = new List<MissingRecord>();
        foreach (var record in dto.Missing ?? [])
        {
            if (string.IsNullOrWhiteSpace(record.RecordId) || string.IsNullOrWhiteSpace(record.RecordType) || string.IsNullOrWhiteSpace(record.Reason))
            {
                errors.Add($"{source}: each missing record needs an id, a type, and a reason.");
                continue;
            }

            missing.Add(new MissingRecord(record.RecordId.Trim(), record.RecordType.Trim(), record.Reason.Trim()));
        }

        RevisionConflict? conflict = null;
        if (dto.Contradiction is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Contradiction.PlmRevision) || string.IsNullOrWhiteSpace(dto.Contradiction.MesRevision) || string.IsNullOrWhiteSpace(dto.Contradiction.Detail))
            {
                errors.Add($"{source}: contradiction needs plmRevision, mesRevision, and detail.");
            }
            else
            {
                conflict = new RevisionConflict(dto.Contradiction.PlmRevision.Trim(), dto.Contradiction.MesRevision.Trim(), dto.Contradiction.PartName?.Trim() ?? "part", dto.Contradiction.Detail.Trim());
            }
        }

        return new FaultEvidence(
            MapEco(dto.Eco, source, errors),
            MapConfig(dto.Config, source, errors),
            MapCalibration(dto.Calibration, source, errors),
            MapMaterial(dto.MaterialLot, source, errors, out var materialTools),
            materialTools,
            MapNotice(dto.SupplierNotice, source, errors),
            MapWorkOrder(dto.WorkOrder, source, errors),
            MapRuns(dto.Runs, source, errors),
            confounders,
            missing,
            conflict,
            dto.Perturbation?.Contradictory ?? false,
            dto.Perturbation?.Missing ?? false);
    }

    private static Confounder? MapConfounder(ConfounderDto dto, string source, List<string> errors)
    {
        switch (dto.Kind)
        {
            case "pm":
                var order = MapWorkOrder(dto, source, errors);
                return order is null ? null : new Confounder.PreventiveMaintenance(order);
            case "supplierLot":
                var lot = MapMaterial(dto.Material, source, errors, out var tools);
                var notice = MapNotice(dto.Notice, source, errors);
                if (lot is null || notice is null || tools.Count == 0)
                {
                    errors.Add($"{source}: a supplier-lot confounder needs a lot, a notice, and tool ids.");
                    return null;
                }

                return new Confounder.SupplierLotChange(lot, notice, tools);
            case "eco":
                var eco = MapEco(dto.Eco, source, errors);
                return eco is null ? null : new Confounder.ChangeOrder(eco);
            default:
                errors.Add($"{source}: confounder kind must be pm, supplierLot, or eco.");
                return null;
        }
    }

    private static EcoRecord? MapEco(EcoDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.EcoId) || string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.ChangeSummary) || !TryTime(dto.EffectiveAt, out var at))
        {
            errors.Add($"{source}: ECO {dto.EcoId} needs an id, title, summary, and effective time.");
            return null;
        }

        return new EcoRecord(dto.EcoId, dto.Title, dto.ToolId, dto.RecipeId, at, dto.ChangeSummary);
    }

    private static ConfigRevision? MapConfig(ConfigDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.ConfigId) || string.IsNullOrWhiteSpace(dto.ToolId) || string.IsNullOrWhiteSpace(dto.Revision) || string.IsNullOrWhiteSpace(dto.PartName))
        {
            errors.Add($"{source}: config needs an id, tool, revision, and part name.");
            return null;
        }

        return new ConfigRevision(dto.ConfigId, new ToolId(dto.ToolId), dto.Revision, dto.PartName);
    }

    private static CalibrationRecord? MapCalibration(CalibrationDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.CalibrationId) || string.IsNullOrWhiteSpace(dto.ToolId) || string.IsNullOrWhiteSpace(dto.Instrument) || !TryTime(dto.DueAt, out var due))
        {
            errors.Add($"{source}: calibration needs an id, tool, instrument, and due time.");
            return null;
        }

        DateTimeOffset? completed = null;
        if (!string.IsNullOrWhiteSpace(dto.CompletedAt))
        {
            if (!TryTime(dto.CompletedAt, out var parsed))
            {
                errors.Add($"{source}: calibration completedAt is not a timestamp.");
                return null;
            }

            completed = parsed;
        }

        return new CalibrationRecord(dto.CalibrationId, new ToolId(dto.ToolId), dto.Instrument, due, completed, dto.IsOverdue);
    }

    private static MaterialLot? MapMaterial(MaterialDto? dto, string source, List<string> errors, out List<string> toolIds)
    {
        toolIds = dto?.ToolIds?.Where(tool => !string.IsNullOrWhiteSpace(tool)).Select(tool => tool.Trim()).Distinct(StringComparer.Ordinal).ToList() ?? [];
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.LotId) || string.IsNullOrWhiteSpace(dto.MaterialCode) || string.IsNullOrWhiteSpace(dto.SupplierName) || !TryTime(dto.ReceivedAt, out var received))
        {
            errors.Add($"{source}: material lot needs an id, code, supplier, and received time.");
            return null;
        }

        return new MaterialLot(dto.LotId, dto.MaterialCode, dto.SupplierName, received, dto.NoticeId);
    }

    private static SupplierNotice? MapNotice(NoticeDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.NoticeId) || string.IsNullOrWhiteSpace(dto.SupplierName) || string.IsNullOrWhiteSpace(dto.MaterialCode) || string.IsNullOrWhiteSpace(dto.LotId) || string.IsNullOrWhiteSpace(dto.Summary) || !TryTime(dto.IssuedAt, out var issued))
        {
            errors.Add($"{source}: supplier notice needs an id, supplier, material, lot, summary, and issued time.");
            return null;
        }

        return new SupplierNotice(dto.NoticeId, dto.SupplierName, dto.MaterialCode, dto.LotId, dto.Summary, issued);
    }

    private static WorkOrder? MapWorkOrder(WorkOrderDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.WorkOrderId) || string.IsNullOrWhiteSpace(dto.ToolId) || string.IsNullOrWhiteSpace(dto.Kind) || string.IsNullOrWhiteSpace(dto.Summary) || !TryTime(dto.PerformedAt, out var at))
        {
            errors.Add($"{source}: work order needs an id, tool, kind, summary, and performed time.");
            return null;
        }

        return new WorkOrder(dto.WorkOrderId, new ToolId(dto.ToolId), dto.ChamberId, dto.Kind, at, dto.Summary);
    }

    private static FaultRuns? MapRuns(RunsDto? dto, string source, List<string> errors)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.RecipeId) || string.IsNullOrWhiteSpace(dto.RecipeRevision) || string.IsNullOrWhiteSpace(dto.LotId) || string.IsNullOrWhiteSpace(dto.MaterialCode) || dto.Count is null or < 1)
        {
            errors.Add($"{source}: runs need a recipe, revision, lot, material code, and a positive count.");
            return null;
        }

        return new FaultRuns(dto.RecipeId.Trim(), dto.RecipeRevision.Trim(), dto.LotId.Trim(), dto.MaterialCode.Trim(), dto.Count.Value);
    }

    private static string Text(string? value, string source, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{source}: {name} is required.");
            return string.Empty;
        }

        return value.Trim();
    }

    private static bool TryShape(string? text, out FaultShape shape)
    {
        switch (text?.Trim())
        {
            case "step":
                shape = FaultShape.Step;
                return true;
            case "ramp":
                shape = FaultShape.Ramp;
                return true;
            case "oscillation":
                shape = FaultShape.Oscillation;
                return true;
            case "intermittent":
                shape = FaultShape.Intermittent;
                return true;
            default:
                shape = default;
                return false;
        }
    }

    private static bool TryMagnitude(MagnitudeDto? dto, out MagnitudeKind kind, out double value)
    {
        kind = default;
        value = 0;
        if (dto?.Value is null || !double.IsFinite(dto.Value.Value))
        {
            return false;
        }

        value = dto.Value.Value;
        switch (dto.Kind?.Trim())
        {
            case "sigma":
                kind = MagnitudeKind.Sigma;
                return true;
            case "percent":
                kind = MagnitudeKind.Percent;
                return true;
            default:
                return false;
        }
    }

    private static bool TryTime(string? text, out DateTimeOffset at)
    {
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out at);
    }

    private sealed class FaultFileDto
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Category { get; set; }

        public string? Scenario { get; set; }

        public string? ExpectedBehavior { get; set; }

        public bool? AssertCategory { get; set; }

        public List<string>? Tools { get; set; }

        public string? ChamberId { get; set; }

        public string? Parameter { get; set; }

        public string? Shape { get; set; }

        public MagnitudeDto? Magnitude { get; set; }

        public string? Onset { get; set; }

        public double? PeriodSeconds { get; set; }

        public double? RampSeconds { get; set; }

        public double? DutyCycle { get; set; }

        public string? Cause { get; set; }

        public List<string>? CounterEvidence { get; set; }

        public List<string>? Actions { get; set; }

        public string? WhyItMatters { get; set; }

        public EvidenceDto? Evidence { get; set; }
    }

    private sealed class EvidenceDto
    {
        public EcoDto? Eco { get; set; }

        public ConfigDto? Config { get; set; }

        public CalibrationDto? Calibration { get; set; }

        public MaterialDto? MaterialLot { get; set; }

        public NoticeDto? SupplierNotice { get; set; }

        public WorkOrderDto? WorkOrder { get; set; }

        public RunsDto? Runs { get; set; }

        public List<ConfounderDto>? Confounders { get; set; }

        public List<MissingDto>? Missing { get; set; }

        public ConflictDto? Contradiction { get; set; }

        public PerturbationDto? Perturbation { get; set; }
    }

    private sealed class MagnitudeDto
    {
        public string? Kind { get; set; }

        public double? Value { get; set; }
    }

    private sealed class EcoDto
    {
        public string? EcoId { get; set; }

        public string? Title { get; set; }

        public string? ToolId { get; set; }

        public string? RecipeId { get; set; }

        public string? EffectiveAt { get; set; }

        public string? ChangeSummary { get; set; }

        public string? WhyItMatters { get; set; }

        public string? PartName { get; set; }

        public string? Revision { get; set; }
    }

    private sealed class ConfigDto
    {
        public string? ConfigId { get; set; }

        public string? ToolId { get; set; }

        public string? Revision { get; set; }

        public string? PartName { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class CalibrationDto
    {
        public string? CalibrationId { get; set; }

        public string? ToolId { get; set; }

        public string? Instrument { get; set; }

        public string? DueAt { get; set; }

        public string? CompletedAt { get; set; }

        public bool IsOverdue { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class MaterialDto
    {
        public string? LotId { get; set; }

        public string? MaterialCode { get; set; }

        public string? SupplierName { get; set; }

        public string? ReceivedAt { get; set; }

        public string? NoticeId { get; set; }

        public List<string>? ToolIds { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class NoticeDto
    {
        public string? NoticeId { get; set; }

        public string? SupplierName { get; set; }

        public string? MaterialCode { get; set; }

        public string? LotId { get; set; }

        public string? Summary { get; set; }

        public string? IssuedAt { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private class WorkOrderDto
    {
        public string? WorkOrderId { get; set; }

        public string? ToolId { get; set; }

        public string? ChamberId { get; set; }

        public string? Kind { get; set; }

        public string? PerformedAt { get; set; }

        public string? Summary { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class RunsDto
    {
        public string? RecipeId { get; set; }

        public string? RecipeRevision { get; set; }

        public string? LotId { get; set; }

        public string? MaterialCode { get; set; }

        public int? Count { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class ConfounderDto : WorkOrderDto
    {
        public MaterialDto? Material { get; set; }

        public NoticeDto? Notice { get; set; }

        public EcoDto? Eco { get; set; }
    }

    private sealed class MissingDto
    {
        public string? RecordId { get; set; }

        public string? RecordType { get; set; }

        public string? Reason { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class ConflictDto
    {
        public string? PlmRevision { get; set; }

        public string? MesRevision { get; set; }

        public string? PartName { get; set; }

        public string? Detail { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class PerturbationDto
    {
        public bool Contradictory { get; set; }

        public bool Missing { get; set; }
    }
}
