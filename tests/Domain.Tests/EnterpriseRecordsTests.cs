// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class EnterpriseRecordsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private static readonly ToolId Tool = new("E-02");

    [Fact]
    public void ProcessRunTrimsRequiredTextAndDropsABlankSite()
    {
        var run = new ProcessRun("  R-1  ", Tool, "  RCP  ", "  3  ", "  LOT  ", At, "  austin  ");

        run.RunId.Should().Be("R-1");
        run.ToolId.Should().Be(Tool);
        run.RecipeId.Should().Be("RCP");
        run.RecipeRevision.Should().Be("3");
        run.LotId.Should().Be("LOT");
        run.StartedAt.Should().Be(At);
        run.SiteId.Should().Be("austin");

        new ProcessRun("R-1", Tool, "RCP", "3", "LOT", At, null).SiteId.Should().BeNull();
        new ProcessRun("R-1", Tool, "RCP", "3", "LOT", At, "  ").SiteId.Should().BeNull();

        Rejects(() => new ProcessRun(" ", Tool, "RCP", "3", "LOT", At), "Run id is required.");
        Rejects(() => new ProcessRun(null!, Tool, "RCP", "3", "LOT", At), "Run id is required.");
        Rejects(() => new ProcessRun("R-1", default, "RCP", "3", "LOT", At), "Tool id is required.");
        Rejects(() => new ProcessRun("R-1", Tool, " ", "3", "LOT", At), "Recipe id is required.");
        Rejects(() => new ProcessRun("R-1", Tool, null!, "3", "LOT", At), "Recipe id is required.");
        Rejects(() => new ProcessRun("R-1", Tool, "RCP", " ", "LOT", At), "Recipe revision is required.");
        Rejects(() => new ProcessRun("R-1", Tool, "RCP", null!, "LOT", At), "Recipe revision is required.");
        Rejects(() => new ProcessRun("R-1", Tool, "RCP", "3", " ", At), "Lot id is required.");
        Rejects(() => new ProcessRun("R-1", Tool, "RCP", "3", null!, At), "Lot id is required.");
    }

    [Fact]
    public void LotReferenceTrimsRequiredTextAndDropsABlankSite()
    {
        var lot = new LotReference("  LOT  ", "  GAS  ", Tool, "  austin  ");

        lot.LotId.Should().Be("LOT");
        lot.MaterialCode.Should().Be("GAS");
        lot.ToolId.Should().Be(Tool);
        lot.SiteId.Should().Be("austin");
        new LotReference("LOT", "GAS", Tool, null).SiteId.Should().BeNull();
        new LotReference("LOT", "GAS", Tool, " ").SiteId.Should().BeNull();

        Rejects(() => new LotReference(" ", "GAS", Tool), "Lot id is required.");
        Rejects(() => new LotReference(null!, "GAS", Tool), "Lot id is required.");
        Rejects(() => new LotReference("LOT", " ", Tool), "Material code is required.");
        Rejects(() => new LotReference("LOT", null!, Tool), "Material code is required.");
        Rejects(() => new LotReference("LOT", "GAS", default), "Tool id is required.");
    }

    [Fact]
    public void NcrRecordTrimsRequiredTextAndDropsABlankLot()
    {
        var ncr = new NcrRecord("  NCR-1  ", "  E-02  ", "  Drift  ", At, "  LOT  ");

        ncr.NcrId.Should().Be("NCR-1");
        ncr.ToolId.Should().Be("E-02");
        ncr.Title.Should().Be("Drift");
        ncr.OpenedAt.Should().Be(At);
        ncr.LotId.Should().Be("LOT");
        new NcrRecord("NCR-1", "E-02", "Drift", At, null).LotId.Should().BeNull();
        new NcrRecord("NCR-1", "E-02", "Drift", At, " ").LotId.Should().BeNull();

        Rejects(() => new NcrRecord(" ", "E-02", "Drift", At, null), "NCR id is required.");
        Rejects(() => new NcrRecord(null!, "E-02", "Drift", At, null), "NCR id is required.");
        Rejects(() => new NcrRecord("NCR-1", " ", "Drift", At, null), "Tool id is required.");
        Rejects(() => new NcrRecord("NCR-1", null!, "Drift", At, null), "Tool id is required.");
        Rejects(() => new NcrRecord("NCR-1", "E-02", " ", At, null), "NCR title is required.");
        Rejects(() => new NcrRecord("NCR-1", "E-02", null!, At, null), "NCR title is required.");
    }

    [Fact]
    public void EcoRecordTrimsRequiredTextAndDropsBlankTargets()
    {
        var eco = new EcoRecord("  ECO-1  ", "  Ring  ", "  E-02  ", "  RCP  ", At, "  Changed the ring.  ");

        eco.EcoId.Should().Be("ECO-1");
        eco.Title.Should().Be("Ring");
        eco.ToolId.Should().Be("E-02");
        eco.RecipeId.Should().Be("RCP");
        eco.EffectiveAt.Should().Be(At);
        eco.ChangeSummary.Should().Be("Changed the ring.");
        new EcoRecord("ECO-1", "Ring", null, " ", At, "Changed.").ToolId.Should().BeNull();
        new EcoRecord("ECO-1", "Ring", " ", null, At, "Changed.").RecipeId.Should().BeNull();

        Rejects(() => new EcoRecord(" ", "Ring", "E-02", "RCP", At, "Changed."), "ECO id is required.");
        Rejects(() => new EcoRecord(null!, "Ring", "E-02", "RCP", At, "Changed."), "ECO id is required.");
        Rejects(() => new EcoRecord("ECO-1", " ", "E-02", "RCP", At, "Changed."), "ECO title is required.");
        Rejects(() => new EcoRecord("ECO-1", null!, "E-02", "RCP", At, "Changed."), "ECO title is required.");
        Rejects(() => new EcoRecord("ECO-1", "Ring", "E-02", "RCP", At, " "), "ECO change summary is required.");
        Rejects(() => new EcoRecord("ECO-1", "Ring", "E-02", "RCP", At, null!), "ECO change summary is required.");
    }

    [Fact]
    public void ConfigRevisionTrimsRequiredText()
    {
        var config = new ConfigRevision("  CFG  ", Tool, "  B  ", "  edge ring  ");

        config.ConfigId.Should().Be("CFG");
        config.ToolId.Should().Be(Tool);
        config.Revision.Should().Be("B");
        config.PartName.Should().Be("edge ring");

        Rejects(() => new ConfigRevision(" ", Tool, "B", "ring"), "Config id is required.");
        Rejects(() => new ConfigRevision(null!, Tool, "B", "ring"), "Config id is required.");
        Rejects(() => new ConfigRevision("CFG", default, "B", "ring"), "Tool id is required.");
        Rejects(() => new ConfigRevision("CFG", Tool, " ", "ring"), "Config revision is required.");
        Rejects(() => new ConfigRevision("CFG", Tool, null!, "ring"), "Config revision is required.");
        Rejects(() => new ConfigRevision("CFG", Tool, "B", " "), "Part name is required.");
        Rejects(() => new ConfigRevision("CFG", Tool, "B", null!), "Part name is required.");
    }

    [Fact]
    public void MaterialLotTrimsRequiredTextAndDropsABlankNotice()
    {
        var lot = new MaterialLot("  LOT  ", "  GAS  ", "  Acme  ", At, "  N-1  ");

        lot.LotId.Should().Be("LOT");
        lot.MaterialCode.Should().Be("GAS");
        lot.SupplierName.Should().Be("Acme");
        lot.ReceivedAt.Should().Be(At);
        lot.NoticeId.Should().Be("N-1");
        new MaterialLot("LOT", "GAS", "Acme", At, null).NoticeId.Should().BeNull();
        new MaterialLot("LOT", "GAS", "Acme", At, " ").NoticeId.Should().BeNull();

        Rejects(() => new MaterialLot(" ", "GAS", "Acme", At, null), "Lot id is required.");
        Rejects(() => new MaterialLot(null!, "GAS", "Acme", At, null), "Lot id is required.");
        Rejects(() => new MaterialLot("LOT", " ", "Acme", At, null), "Material code is required.");
        Rejects(() => new MaterialLot("LOT", null!, "Acme", At, null), "Material code is required.");
        Rejects(() => new MaterialLot("LOT", "GAS", " ", At, null), "Supplier name is required.");
        Rejects(() => new MaterialLot("LOT", "GAS", null!, At, null), "Supplier name is required.");
    }

    [Fact]
    public void SupplierNoticeTrimsRequiredText()
    {
        var notice = new SupplierNotice("  N-1  ", "  Acme  ", "  GAS  ", "  LOT  ", "  Moisture.  ", At);

        notice.NoticeId.Should().Be("N-1");
        notice.SupplierName.Should().Be("Acme");
        notice.MaterialCode.Should().Be("GAS");
        notice.LotId.Should().Be("LOT");
        notice.Summary.Should().Be("Moisture.");
        notice.IssuedAt.Should().Be(At);

        Rejects(() => new SupplierNotice(" ", "Acme", "GAS", "LOT", "Moisture.", At), "Supplier notice id is required.");
        Rejects(() => new SupplierNotice(null!, "Acme", "GAS", "LOT", "Moisture.", At), "Supplier notice id is required.");
        Rejects(() => new SupplierNotice("N-1", " ", "GAS", "LOT", "Moisture.", At), "Supplier name is required.");
        Rejects(() => new SupplierNotice("N-1", null!, "GAS", "LOT", "Moisture.", At), "Supplier name is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", " ", "LOT", "Moisture.", At), "Material code is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", null!, "LOT", "Moisture.", At), "Material code is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", "GAS", " ", "Moisture.", At), "Lot id is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", "GAS", null!, "Moisture.", At), "Lot id is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", "GAS", "LOT", " ", At), "Supplier notice summary is required.");
        Rejects(() => new SupplierNotice("N-1", "Acme", "GAS", "LOT", null!, At), "Supplier notice summary is required.");
    }

    [Fact]
    public void WorkOrderTrimsRequiredTextAndDropsABlankChamber()
    {
        var order = new WorkOrder("  WO-1  ", Tool, "  C1  ", "  PM  ", At, "  Replaced the ring.  ");

        order.WorkOrderId.Should().Be("WO-1");
        order.ToolId.Should().Be(Tool);
        order.ChamberId.Should().Be("C1");
        order.Kind.Should().Be("PM");
        order.PerformedAt.Should().Be(At);
        order.Summary.Should().Be("Replaced the ring.");
        new WorkOrder("WO-1", Tool, null, "PM", At, "Done.").ChamberId.Should().BeNull();
        new WorkOrder("WO-1", Tool, " ", "PM", At, "Done.").ChamberId.Should().BeNull();

        Rejects(() => new WorkOrder(" ", Tool, "C1", "PM", At, "Done."), "Work order id is required.");
        Rejects(() => new WorkOrder(null!, Tool, "C1", "PM", At, "Done."), "Work order id is required.");
        Rejects(() => new WorkOrder("WO-1", default, "C1", "PM", At, "Done."), "Tool id is required.");
        Rejects(() => new WorkOrder("WO-1", Tool, "C1", " ", At, "Done."), "Work order kind is required.");
        Rejects(() => new WorkOrder("WO-1", Tool, "C1", null!, At, "Done."), "Work order kind is required.");
        Rejects(() => new WorkOrder("WO-1", Tool, "C1", "PM", At, " "), "Work order summary is required.");
        Rejects(() => new WorkOrder("WO-1", Tool, "C1", "PM", At, null!), "Work order summary is required.");
    }

    [Fact]
    public void CalibrationRecordTrimsRequiredTextAndKeepsTheOverdueFlag()
    {
        var completed = At.AddDays(-1);
        var record = new CalibrationRecord("  CAL-1  ", Tool, "  manometer  ", At, completed, true);

        record.CalibrationId.Should().Be("CAL-1");
        record.ToolId.Should().Be(Tool);
        record.Instrument.Should().Be("manometer");
        record.DueAt.Should().Be(At);
        record.CompletedAt.Should().Be(completed);
        record.IsOverdue.Should().BeTrue();

        var open = new CalibrationRecord("CAL-1", Tool, "manometer", At, null, false);
        open.CompletedAt.Should().BeNull();
        open.IsOverdue.Should().BeFalse();

        Rejects(() => new CalibrationRecord(" ", Tool, "manometer", At, null, false), "Calibration id is required.");
        Rejects(() => new CalibrationRecord(null!, Tool, "manometer", At, null, false), "Calibration id is required.");
        Rejects(() => new CalibrationRecord("CAL-1", default, "manometer", At, null, false), "Tool id is required.");
        Rejects(() => new CalibrationRecord("CAL-1", Tool, " ", At, null, false), "Instrument is required.");
        Rejects(() => new CalibrationRecord("CAL-1", Tool, null!, At, null, false), "Instrument is required.");
    }

    [Fact]
    public void DocumentHitTrimsRequiredTextAndDropsABlankFamily()
    {
        var hit = new DocumentHit("  DOC-1  ", "  Procedure  ", "  Hold the lot.  ", "  Etch  ", "  C  ");

        hit.DocumentId.Should().Be("DOC-1");
        hit.Title.Should().Be("Procedure");
        hit.Excerpt.Should().Be("Hold the lot.");
        hit.ToolFamily.Should().Be("Etch");
        hit.Revision.Should().Be("C");
        new DocumentHit("DOC-1", "Procedure", "Hold.", null, "C").ToolFamily.Should().BeNull();
        new DocumentHit("DOC-1", "Procedure", "Hold.", " ", "C").ToolFamily.Should().BeNull();

        Rejects(() => new DocumentHit(" ", "Procedure", "Hold.", "Etch", "C"), "Document id is required.");
        Rejects(() => new DocumentHit(null!, "Procedure", "Hold.", "Etch", "C"), "Document id is required.");
        Rejects(() => new DocumentHit("DOC-1", " ", "Hold.", "Etch", "C"), "Document title is required.");
        Rejects(() => new DocumentHit("DOC-1", null!, "Hold.", "Etch", "C"), "Document title is required.");
        Rejects(() => new DocumentHit("DOC-1", "Procedure", " ", "Etch", "C"), "Document excerpt is required.");
        Rejects(() => new DocumentHit("DOC-1", "Procedure", null!, "Etch", "C"), "Document excerpt is required.");
        Rejects(() => new DocumentHit("DOC-1", "Procedure", "Hold.", "Etch", " "), "Document revision is required.");
        Rejects(() => new DocumentHit("DOC-1", "Procedure", "Hold.", "Etch", null!), "Document revision is required.");
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
