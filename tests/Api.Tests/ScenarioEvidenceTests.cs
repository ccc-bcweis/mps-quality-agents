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
