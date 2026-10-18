// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;

namespace Mps.Api.Http;

/// <summary>Optional enterprise records on <c>POST /api/faults</c>. Dates are shifted from <see cref="AnchorAt"/> to injection time.</summary>
public sealed class FaultEvidenceBody
{
    public DateTimeOffset? AnchorAt { get; set; }

    public FaultEcoBody[]? Ecos { get; set; }

    public FaultNcrBody[]? Ncrs { get; set; }

    public FaultWorkOrderBody[]? WorkOrders { get; set; }

    public FaultCalibrationBody[]? Calibrations { get; set; }

    public FaultMaterialLotBody[]? MaterialLots { get; set; }

    public FaultNoticeBody[]? Notices { get; set; }

    public FaultMaterialUseBody[]? MaterialUses { get; set; }

    public FaultEvidenceAttachment ToAttachment()
    {
        return new FaultEvidenceAttachment(
            AnchorAt,
            Map(Ecos, MapEco),
            Map(Ncrs, MapNcr),
            Map(WorkOrders, MapWorkOrder),
            Map(Calibrations, MapCalibration),
            Map(MaterialLots, MapLot),
            Map(Notices, MapNotice),
            Map(MaterialUses, MapUse));
    }

    private static EcoRecord MapEco(FaultEcoBody row) =>
        new(Required(row.EcoId, "ECO id"), Required(row.Title, "ECO title"), row.ToolId, row.RecipeId, RequiredAt(row.EffectiveAt, "ECO effective time"), Required(row.ChangeSummary, "ECO change summary"));

    private static NcrRecord MapNcr(FaultNcrBody row) =>
        new(Required(row.NcrId, "NCR id"), Required(row.ToolId, "NCR tool"), Required(row.Title, "NCR title"), RequiredAt(row.OpenedAt, "NCR opened time"), row.LotId);

    private static WorkOrder MapWorkOrder(FaultWorkOrderBody row) =>
        new(Required(row.WorkOrderId, "Work order id"), new ToolId(Required(row.ToolId, "Work order tool")), row.ChamberId, Required(row.Kind, "Work order kind"), RequiredAt(row.PerformedAt, "Work order time"), Required(row.Summary, "Work order summary"));

    private static CalibrationRecord MapCalibration(FaultCalibrationBody row) =>
        new(Required(row.CalibrationId, "Calibration id"), new ToolId(Required(row.ToolId, "Calibration tool")), Required(row.Instrument, "Calibration instrument"), RequiredAt(row.DueAt, "Calibration due time"), row.CompletedAt, row.IsOverdue);

    private static MaterialLot MapLot(FaultMaterialLotBody row) =>
        new(Required(row.LotId, "Lot id"), Required(row.MaterialCode, "Material code"), Required(row.SupplierName, "Supplier name"), RequiredAt(row.ReceivedAt, "Lot received time"), row.NoticeId);

    private static SupplierNotice MapNotice(FaultNoticeBody row) =>
        new(Required(row.NoticeId, "Notice id"), Required(row.SupplierName, "Supplier name"), Required(row.MaterialCode, "Material code"), Required(row.LotId, "Notice lot"), Required(row.Summary, "Notice summary"), RequiredAt(row.IssuedAt, "Notice time"));

    private static MaterialUseLink MapUse(FaultMaterialUseBody row) =>
        new(Required(row.LotId, "Material use lot"), Required(row.ToolId, "Material use tool"));

    private static TOut[] Map<TIn, TOut>(TIn[]? rows, Func<TIn, TOut> map)
    {
        if (rows is null || rows.Length == 0)
        {
            return [];
        }

        var mapped = new TOut[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index] is null)
            {
                throw new IntakeValidationException("Fault evidence has an empty record.");
            }

            mapped[index] = map(rows[index]);
        }

        return mapped;
    }

    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IntakeValidationException(name + " is required.");
        }

        return value.Trim();
    }

    private static DateTimeOffset RequiredAt(DateTimeOffset? value, string name)
    {
        if (value is not { } at)
        {
            throw new IntakeValidationException(name + " is required.");
        }

        return at;
    }
}

public sealed class FaultEcoBody
{
    public string? EcoId { get; set; }

    public string? Title { get; set; }

    public string? ToolId { get; set; }

    public string? RecipeId { get; set; }

    public DateTimeOffset? EffectiveAt { get; set; }

    public string? ChangeSummary { get; set; }
}

public sealed class FaultNcrBody
{
    public string? NcrId { get; set; }

    public string? ToolId { get; set; }

    public string? Title { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public string? LotId { get; set; }
}

public sealed class FaultWorkOrderBody
{
    public string? WorkOrderId { get; set; }

    public string? ToolId { get; set; }

    public string? ChamberId { get; set; }

    public string? Kind { get; set; }

    public DateTimeOffset? PerformedAt { get; set; }

    public string? Summary { get; set; }
}

public sealed class FaultCalibrationBody
{
    public string? CalibrationId { get; set; }

    public string? ToolId { get; set; }

    public string? Instrument { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsOverdue { get; set; }
}

public sealed class FaultMaterialLotBody
{
    public string? LotId { get; set; }

    public string? MaterialCode { get; set; }

    public string? SupplierName { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }

    public string? NoticeId { get; set; }
}

public sealed class FaultNoticeBody
{
    public string? NoticeId { get; set; }

    public string? SupplierName { get; set; }

    public string? MaterialCode { get; set; }

    public string? LotId { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
}

public sealed class FaultMaterialUseBody
{
    public string? LotId { get; set; }

    public string? ToolId { get; set; }
}
