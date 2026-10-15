// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;

namespace Mps.Domain.Enterprise;

public sealed record ProcessRun
{
    public ProcessRun(string runId, ToolId toolId, string recipeId, string recipeRevision, string lotId, DateTimeOffset startedAt, string? siteId = null)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        RunId = RequiredText.Value(runId, "Run id is required.");
        ToolId = toolId;
        RecipeId = RequiredText.Value(recipeId, "Recipe id is required.");
        RecipeRevision = RequiredText.Value(recipeRevision, "Recipe revision is required.");
        LotId = RequiredText.Value(lotId, "Lot id is required.");
        StartedAt = startedAt;
        SiteId = string.IsNullOrWhiteSpace(siteId) ? null : siteId.Trim();
    }

    public string RunId { get; }

    public ToolId ToolId { get; }

    public string RecipeId { get; }

    public string RecipeRevision { get; }

    public string LotId { get; }

    public DateTimeOffset StartedAt { get; }

    public string? SiteId { get; }
}

public sealed record LotReference
{
    public LotReference(string lotId, string materialCode, ToolId toolId, string? siteId = null)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        LotId = RequiredText.Value(lotId, "Lot id is required.");
        MaterialCode = RequiredText.Value(materialCode, "Material code is required.");
        ToolId = toolId;
        SiteId = string.IsNullOrWhiteSpace(siteId) ? null : siteId.Trim();
    }

    public string LotId { get; }

    public string MaterialCode { get; }

    public ToolId ToolId { get; }

    public string? SiteId { get; }
}

public sealed record NcrRecord
{
    public NcrRecord(string ncrId, string toolId, string title, DateTimeOffset openedAt, string? lotId)
    {
        NcrId = RequiredText.Value(ncrId, "NCR id is required.");
        ToolId = RequiredText.Value(toolId, "Tool id is required.");
        Title = RequiredText.Value(title, "NCR title is required.");
        OpenedAt = openedAt;
        LotId = string.IsNullOrWhiteSpace(lotId) ? null : lotId.Trim();
    }

    public string NcrId { get; }

    public string ToolId { get; }

    public string Title { get; }

    public DateTimeOffset OpenedAt { get; }

    public string? LotId { get; }
}

public sealed record EcoRecord
{
    public EcoRecord(string ecoId, string title, string? toolId, string? recipeId, DateTimeOffset effectiveAt, string changeSummary)
    {
        EcoId = RequiredText.Value(ecoId, "ECO id is required.");
        Title = RequiredText.Value(title, "ECO title is required.");
        ToolId = string.IsNullOrWhiteSpace(toolId) ? null : toolId.Trim();
        RecipeId = string.IsNullOrWhiteSpace(recipeId) ? null : recipeId.Trim();
        EffectiveAt = effectiveAt;
        ChangeSummary = RequiredText.Value(changeSummary, "ECO change summary is required.");
    }

    public string EcoId { get; }

    public string Title { get; }

    public string? ToolId { get; }

    public string? RecipeId { get; }

    public DateTimeOffset EffectiveAt { get; }

    public string ChangeSummary { get; }
}

public sealed record ConfigRevision
{
    public ConfigRevision(string configId, ToolId toolId, string revision, string partName)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        ConfigId = RequiredText.Value(configId, "Config id is required.");
        ToolId = toolId;
        Revision = RequiredText.Value(revision, "Config revision is required.");
        PartName = RequiredText.Value(partName, "Part name is required.");
    }

    public string ConfigId { get; }

    public ToolId ToolId { get; }

    public string Revision { get; }

    public string PartName { get; }
}

public sealed record MaterialLot
{
    public MaterialLot(string lotId, string materialCode, string supplierName, DateTimeOffset receivedAt, string? noticeId)
    {
        LotId = RequiredText.Value(lotId, "Lot id is required.");
        MaterialCode = RequiredText.Value(materialCode, "Material code is required.");
        SupplierName = RequiredText.Value(supplierName, "Supplier name is required.");
        ReceivedAt = receivedAt;
        NoticeId = string.IsNullOrWhiteSpace(noticeId) ? null : noticeId.Trim();
    }

    public string LotId { get; }

    public string MaterialCode { get; }

    public string SupplierName { get; }

    public DateTimeOffset ReceivedAt { get; }

    public string? NoticeId { get; }
}

public sealed record SupplierNotice
{
    public SupplierNotice(string noticeId, string supplierName, string materialCode, string lotId, string summary, DateTimeOffset issuedAt)
    {
        NoticeId = RequiredText.Value(noticeId, "Supplier notice id is required.");
        SupplierName = RequiredText.Value(supplierName, "Supplier name is required.");
        MaterialCode = RequiredText.Value(materialCode, "Material code is required.");
        LotId = RequiredText.Value(lotId, "Lot id is required.");
        Summary = RequiredText.Value(summary, "Supplier notice summary is required.");
        IssuedAt = issuedAt;
    }

    public string NoticeId { get; }

    public string SupplierName { get; }

    public string MaterialCode { get; }

    public string LotId { get; }

    public string Summary { get; }

    public DateTimeOffset IssuedAt { get; }
}

public sealed record WorkOrder
{
    public WorkOrder(string workOrderId, ToolId toolId, string? chamberId, string kind, DateTimeOffset performedAt, string summary)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        WorkOrderId = RequiredText.Value(workOrderId, "Work order id is required.");
        ToolId = toolId;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Kind = RequiredText.Value(kind, "Work order kind is required.");
        PerformedAt = performedAt;
        Summary = RequiredText.Value(summary, "Work order summary is required.");
    }

    public string WorkOrderId { get; }

    public ToolId ToolId { get; }

    public string? ChamberId { get; }

    public string Kind { get; }

    public DateTimeOffset PerformedAt { get; }

    public string Summary { get; }
}

public sealed record CalibrationRecord
{
    public CalibrationRecord(
        string calibrationId,
        ToolId toolId,
        string instrument,
        DateTimeOffset dueAt,
        DateTimeOffset? completedAt,
        bool isOverdue)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        CalibrationId = RequiredText.Value(calibrationId, "Calibration id is required.");
        ToolId = toolId;
        Instrument = RequiredText.Value(instrument, "Instrument is required.");
        DueAt = dueAt;
        CompletedAt = completedAt;
        IsOverdue = isOverdue;
    }

    public string CalibrationId { get; }

    public ToolId ToolId { get; }

    public string Instrument { get; }

    public DateTimeOffset DueAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public bool IsOverdue { get; }
}

public sealed record DocumentHit
{
    public DocumentHit(string documentId, string title, string excerpt, string? toolFamily, string revision)
    {
        DocumentId = RequiredText.Value(documentId, "Document id is required.");
        Title = RequiredText.Value(title, "Document title is required.");
        Excerpt = RequiredText.Value(excerpt, "Document excerpt is required.");
        ToolFamily = string.IsNullOrWhiteSpace(toolFamily) ? null : toolFamily.Trim();
        Revision = RequiredText.Value(revision, "Document revision is required.");
    }

    public string DocumentId { get; }

    public string Title { get; }

    public string Excerpt { get; }

    public string? ToolFamily { get; }

    public string Revision { get; }
}
