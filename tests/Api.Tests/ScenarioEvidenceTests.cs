// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Application.Faults;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;

using Xunit;

namespace Mps.Api.Tests;

public sealed class ScenarioEvidenceTests
{
    private static readonly DateTimeOffset Until = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EachCorrelationServesItsScenarioRecordsAndClearRemovesThem()
    {
        var root = Repo();
        var board = new ScenarioEvidenceBoard();
        var tools = new SeedEnterpriseTools(
            Path.Combine(root, "data", "seed", "enterprise.json"),
            Path.Combine(root, "data", "scenarios", "evidence.json"),
            board);
        var e01 = new ToolId("E-01");
        var from = Until.AddDays(-2);
        foreach (var id in new[] { "SC-1", "SC-2", "SC-3", "SC-4", "SC-5", "SC-6", "SC-7", "SC-8" })
        {
            board.RegisterScenario("corr-" + id, id, null);
        }

        (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
        (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-RF-MATCH");

        using (board.Use("corr-SC-2"))
        {
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().Contain(record => record.CalibrationId == "CAL-E01-ESC-TC" && record.IsOverdue);
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-RF-MATCH");
        }

        using (board.Use("corr-SC-3"))
        {
            (await tools.GetMaterialLotsAsync(new ToolId("E-02"), from, Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "PL-2044");
            (await tools.GetSupplierNoticesAsync("GAS-CF4", from, Until, CancellationToken.None)).Should().Contain(notice => notice.NoticeId == "SN-PL-2044");
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
        }

        using (board.Use("corr-SC-4"))
        {
            (await tools.GetWorkOrdersAsync(e01, from, Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-CLN-E01-220");
        }

        using (board.Use("corr-SC-5"))
        {
            (await tools.GetMaterialLotsAsync(new ToolId("D-03"), from, Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "MX4419");
            (await tools.GetMaterialLotsAsync(new ToolId("I-D-01"), from, Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "MX4419");
            (await tools.GetSupplierNoticesAsync("PREC-FILM", from, Until, CancellationToken.None)).Should().Contain(notice => notice.NoticeId == "SN-MX4419" && notice.Summary.Contains("Ironwood", StringComparison.Ordinal));
            (await tools.SearchNcrsAsync(new ToolId("D-03"), from, Until, CancellationToken.None)).Should().Contain(ncr => ncr.NcrId == "QR-MX4419" && ncr.Title.Contains("Northwater", StringComparison.Ordinal) && ncr.Title.Contains("Ironwood", StringComparison.Ordinal));
            (await tools.SearchNcrsAsync(new ToolId("I-D-01"), from, Until, CancellationToken.None)).Should().Contain(ncr => ncr.NcrId == "QR-MX4419-IW");
            (await tools.GetWorkOrdersAsync(new ToolId("D-03"), from, Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-CLN-D03-A" && order.Summary.Contains("Ironwood", StringComparison.Ordinal));
        }

        using (board.Use("corr-SC-6"))
        {
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().Contain(record => record.CalibrationId == "CAL-E01-RF-MATCH" && record.IsOverdue);
            (await tools.GetWorkOrdersAsync(e01, from, Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-RF-E01-MATCH");
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
        }

        using (board.Use("corr-SC-7"))
        {
            (await tools.GetWorkOrdersAsync(new ToolId("D-02"), from, Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-PMP-D02-BRG" && order.Summary.Contains("bearing", StringComparison.OrdinalIgnoreCase));
        }

        using (board.Use("corr-SC-8"))
        {
            (await tools.GetWorkOrdersAsync(new ToolId("E-03"), from, Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-RCP-E03-RF" && order.Summary.Contains("recipe setpoint", StringComparison.OrdinalIgnoreCase));
            (await tools.GetEcosAsync(new ToolId("E-03"), from, Until, CancellationToken.None)).Should().BeEmpty();
        }

        board.RemoveScenario("SC-2");
        using (board.Use("corr-SC-2"))
        {
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
        }

        using (board.Use("corr-SC-6"))
        {
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().Contain(record => record.CalibrationId == "CAL-E01-RF-MATCH");
        }

        board.Clear();
        using (board.Use("corr-SC-6"))
        {
            (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-RF-MATCH");
        }
    }

    [Fact]
    public async Task TwoFaultRecordsStayOnTheirOwnCorrelations()
    {
        var root = Repo();
        var board = new ScenarioEvidenceBoard();
        var tools = new SeedEnterpriseTools(Path.Combine(root, "data", "seed", "enterprise.json"), board: board);
        var when = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        board.RegisterRecords("fault-a", Eco("ECO-BOARD-A", "E-01", when), when);
        board.RegisterRecords("fault-b", Eco("ECO-BOARD-B", "E-02", when), when);

        using (board.Use("fault-a"))
        {
            var ecos = await tools.GetEcosAsync(new ToolId("E-01"), when.AddDays(-1), when.AddDays(1), CancellationToken.None);
            ecos.Should().Contain(eco => eco.EcoId == "ECO-BOARD-A");
            ecos.Should().NotContain(eco => eco.EcoId == "ECO-BOARD-B");
        }

        using (board.Use("fault-b"))
        {
            var ecos = await tools.GetEcosAsync(new ToolId("E-02"), when.AddDays(-1), when.AddDays(1), CancellationToken.None);
            ecos.Should().Contain(eco => eco.EcoId == "ECO-BOARD-B");
            ecos.Should().NotContain(eco => eco.EcoId == "ECO-BOARD-A");
        }

        (await tools.GetEcosAsync(new ToolId("E-01"), when.AddDays(-1), when.AddDays(1), CancellationToken.None))
            .Should().NotContain(eco => eco.EcoId == "ECO-BOARD-A");

        board.Remove("fault-a");
        board.Find("fault-a").Should().BeNull();
        board.Find("fault-b").Should().NotBeNull();
    }

    private static FaultEvidenceAttachment Eco(string ecoId, string toolId, DateTimeOffset at) =>
        new(at, [new EcoRecord(ecoId, "Change " + ecoId, toolId, null, at, "Registered with the fault.")]);

    private static string Repo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "seed", "enterprise.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
