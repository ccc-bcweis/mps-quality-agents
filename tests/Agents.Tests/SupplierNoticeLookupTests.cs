// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Tools;
using Mps.Application.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Incidents;

namespace Mps.Agents.Tests;

/// <summary>
/// Live SC-8 (Oct 18): etch E-03 uses no material lots, the notice lookup ran with no material filter, and the
/// in-spec silane notice for deposition tools landed in E-03's evidence.
/// </summary>
public sealed class SupplierNoticeLookupTests
{
    private static readonly SupplierNotice Silane =
        new("SN-SIH4-088", "Cinderwell", "GAS-SIH4", "SIH4-088", "Silane lot SIH4-088 matches the purchase specification.", EvidenceSamples.At);

    private static readonly SupplierNotice Cf4 =
        new("SN-CF4-014", "Cinderwell", "GAS-CF4", "CF4-014", "CF4 lot CF4-014 is out of spec.", EvidenceSamples.At);

    [Fact]
    public async Task AToolWithNoMaterialLotsGetsNoSupplierNotices()
    {
        var erp = new Erp([]);
        var harvest = new ToolHarvest();

        var result = await Session(erp).ExecuteAsync(AgentToolNames.ErpGetSupplierNotices, Incident(), EvidenceSamples.At.AddDays(-1), EvidenceSamples.At, harvest, CancellationToken.None);

        result.Should().Be("notices:0");
        erp.NoticeQueries.Should().BeEmpty("an unfiltered lookup returns every notice in the plant");
    }

    [Fact]
    public async Task NoticesComeOnlyForTheMaterialsTheToolUsedEvenWhenAskedBeforeTheLots()
    {
        var erp = new Erp([new MaterialLot("CF4-014", "GAS-CF4", "Cinderwell", EvidenceSamples.At.AddDays(-3), null)]);
        var harvest = new ToolHarvest();
        var session = Session(erp);

        var notices = await session.ExecuteAsync(AgentToolNames.ErpGetSupplierNotices, Incident(), EvidenceSamples.At.AddDays(-1), EvidenceSamples.At, harvest, CancellationToken.None);
        var lots = await session.ExecuteAsync(AgentToolNames.ErpGetMaterialLots, Incident(), EvidenceSamples.At.AddDays(-1), EvidenceSamples.At, harvest, CancellationToken.None);

        notices.Should().Be("notices:1");
        lots.Should().Be("materialLots:1");
        erp.NoticeQueries.Should().Equal("GAS-CF4");
    }

    private static ReadOnlyToolSession Session(Erp erp)
    {
        var tools = new ScriptedTools();
        return new ReadOnlyToolSession(tools, tools, tools, tools, tools, erp, tools, tools, tools);
    }

    private static Incident Incident() => Domain.Incidents.Incident.Open(
        Domain.Identity.IncidentId.New(),
        new Domain.Identity.ToolId("E-03"),
        "E-03-A",
        "rfBiasPower",
        IncidentChannel.IotSpcAlert,
        Severity.High,
        "Bias RF power step",
        "corr-sc8",
        EvidenceSamples.At);

    private sealed class Erp(IReadOnlyList<MaterialLot> lots) : IErpTool
    {
        public List<string?> NoticeQueries { get; } = [];

        public Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(Domain.Identity.ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
            Task.FromResult(lots);

        public Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
        {
            NoticeQueries.Add(materialCode);
            IReadOnlyList<SupplierNotice> all = [Silane, Cf4];
            return Task.FromResult<IReadOnlyList<SupplierNotice>>(all.Where(notice => materialCode is null || notice.MaterialCode == materialCode).ToArray());
        }
    }
}
