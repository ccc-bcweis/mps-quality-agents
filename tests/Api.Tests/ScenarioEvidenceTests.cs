// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Domain.Identity;

using Xunit;

namespace Mps.Api.Tests;

public sealed class ScenarioEvidenceTests
{
    private static readonly DateTimeOffset Until = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EachScriptedScenarioServesItsOwnRecordsOnlyWhileActive()
    {
        var root = Repo();
        var board = new ScenarioEvidenceBoard();
        var tools = new SeedEnterpriseTools(
            Path.Combine(root, "data", "seed", "enterprise.json"),
            Path.Combine(root, "data", "scenarios", "evidence.json"),
            board);
        var e01 = new ToolId("E-01");

        (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");

        board.Activate("SC-2");
        (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().Contain(record => record.CalibrationId == "CAL-E01-ESC-TC" && record.IsOverdue);

        board.Activate("SC-3");
        (await tools.GetMaterialLotsAsync(new ToolId("E-02"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "PL-2044");
        (await tools.GetSupplierNoticesAsync("GAS-CF4", Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(notice => notice.NoticeId == "SN-PL-2044");

        board.Activate("SC-4");
        (await tools.GetWorkOrdersAsync(e01, Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-CLN-E01-220");

        board.Activate("SC-5");
        (await tools.GetMaterialLotsAsync(new ToolId("D-03"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "MX4419");
        (await tools.GetMaterialLotsAsync(new ToolId("I-D-01"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(lot => lot.LotId == "MX4419");
        (await tools.GetSupplierNoticesAsync("PREC-FILM", Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(notice => notice.NoticeId == "SN-MX4419" && notice.Summary.Contains("Ironwood", StringComparison.Ordinal));
        (await tools.SearchNcrsAsync(new ToolId("D-03"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(ncr => ncr.NcrId == "QR-MX4419" && ncr.Title.Contains("Northwater", StringComparison.Ordinal) && ncr.Title.Contains("Ironwood", StringComparison.Ordinal));
        (await tools.SearchNcrsAsync(new ToolId("I-D-01"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(ncr => ncr.NcrId == "QR-MX4419-IW");
        (await tools.GetWorkOrdersAsync(new ToolId("D-03"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-CLN-D03-A" && order.Summary.Contains("Ironwood", StringComparison.Ordinal));

        board.Activate("SC-7");
        (await tools.GetWorkOrdersAsync(new ToolId("D-02"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-PMP-D02-BRG" && order.Summary.Contains("bearing", StringComparison.OrdinalIgnoreCase));

        board.Activate("SC-8");
        (await tools.GetWorkOrdersAsync(new ToolId("E-03"), Until.AddDays(-2), Until, CancellationToken.None)).Should().Contain(order => order.WorkOrderId == "WO-RCP-E03-RF" && order.Summary.Contains("recipe setpoint", StringComparison.OrdinalIgnoreCase));
        (await tools.GetEcosAsync(new ToolId("E-03"), Until.AddDays(-2), Until, CancellationToken.None)).Should().BeEmpty();

        board.Clear();
        (await tools.GetCalibrationsAsync(e01, CancellationToken.None)).Should().NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
    }

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
