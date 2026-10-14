// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Connectors.Tests;

public sealed class MockEnterpriseClientTests : IClassFixture<ConnectorFactory>
{
    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ConnectorFactory _factory;

    public MockEnterpriseClientTests(ConnectorFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PortsReadFaultEvidenceAndFilterOnTheRecordTimestamp()
    {
        var client = Client();
        var onset = new DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.Zero);
        var runs = await client.GetRunsAsync(new ToolId("E-02"), onset, onset, CancellationToken.None);
        runs.Should().Contain(run => run.RunId == "RUN-FLT-ECO-4471-E-02-00" && run.RecipeRevision == "C");

        var config = await client.GetConfigAsync(new ToolId("D-01"), CancellationToken.None);
        config.Revision.Should().Be("C");
        config.PartName.Should().Be("Showerhead");

        var received = new DateTimeOffset(2026, 10, 7, 18, 0, 0, TimeSpan.Zero);
        var before = await client.GetMaterialLotsAsync(new ToolId("E-01"), received.AddDays(-30), received.AddHours(-1), CancellationToken.None);
        before.Should().NotContain(lot => lot.LotId == "NF-3317");
        var after = await client.GetMaterialLotsAsync(new ToolId("E-01"), received, received.AddDays(2), CancellationToken.None);
        after.Should().Contain(lot => lot.LotId == "NF-3317" && lot.NoticeId == "SN-NF-3317");

        var lots = await client.GetLotsAsync(new ToolId("E-02"), Now, Now, CancellationToken.None);
        lots.Should().Contain(lot => lot.LotId == "NF-3317");

        var ecos = await client.GetEcosAsync(new ToolId("E-02"), onset.AddDays(-2), onset, CancellationToken.None);
        ecos.Should().Contain(eco => eco.EcoId == "ECO-4471");
        var missed = await client.GetEcosAsync(new ToolId("E-02"), onset.AddDays(-10), onset.AddDays(-2), CancellationToken.None);
        missed.Should().NotContain(eco => eco.EcoId == "ECO-4471");

        var orders = await client.GetWorkOrdersAsync(new ToolId("E-02"), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), Now, CancellationToken.None);
        orders.Should().Contain(order => order.WorkOrderId == "WO-PM-8821");

        var calibrations = await client.GetCalibrationsAsync(new ToolId("S-02"), CancellationToken.None);
        calibrations.Should().ContainSingle(record => record.CalibrationId == "CAL-S02-O2-MFC" && record.IsOverdue && record.CompletedAt == null);

        var ncrs = await client.SearchNcrsAsync(new ToolId("C-01"), new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
        ncrs.Should().Contain(ncr => ncr.NcrId == "NCR-C-01-104");

        var notices = await client.GetSupplierNoticesAsync("GAS-NF3-O2", received.AddDays(-1), Now, CancellationToken.None);
        notices.Should().ContainSingle(notice => notice.NoticeId == "SN-NF-3317");
    }

    [Fact]
    public async Task CapaAndTicketWritesUseTheApprovalAndReturnEnterpriseIds()
    {
        var client = Client();
        var incidentId = new IncidentId(Guid.Parse("17171717-1717-1717-1717-171717171717"));
        const string summary = "Replace the edge ring and verify ESC temperature.";
        var token = ApprovalToken.Issue(
            new ApprovalTokenId(Guid.Parse("18181818-1818-1818-1818-181818181818")),
            "eng-17",
            CapaAction.ComputeActionHash(incidentId, summary),
            Now,
            TimeSpan.FromHours(1),
            Key);
        var capa = CapaAction.Open(new CapaId(Guid.Parse("19191919-1919-1919-1919-191919191919")), incidentId, summary, token, Now, Key);
        var externalId = await client.CreateAsync(capa, CancellationToken.None);
        externalId.Should().Be("QMS-19191919-1919-1919-1919-191919191919");

        var duplicate = async () => await client.CreateAsync(capa, CancellationToken.None);
        (await duplicate.Should().ThrowAsync<EnterpriseFaultException>()).Which.StatusCode.Should().Be(409);

        var incident = Incident.Open(incidentId, new ToolId("E-02"), "E-02-A", "escTemperature", IncidentChannel.FaultInjector, Severity.High, "ESC temperature oscillation.", "corr-eco-4471", Now);
        var ticket = await client.OpenAsync(incident, "ESC oscillation on E-02", CancellationToken.None);
        ticket.Number.Should().StartWith("TKT-");
        ticket.OpenedAt.Should().Be(Now);
        ticket.ToolId.Value.Should().Be("E-02");
    }

    [Fact]
    public async Task InjectedFaultsSurfaceOnTheWritePorts()
    {
        var client = Client();
        client.MockFault = "500";
        var act = async () => await client.CreateAsync(Capa("20202020-2020-2020-2020-202020202020", "21212121-2121-2121-2121-212121212121"), CancellationToken.None);
        (await act.Should().ThrowAsync<EnterpriseFaultException>()).Which.StatusCode.Should().Be(500);

        client.MockFault = "409";
        var conflict = async () => await client.CreateAsync(Capa("22222222-2222-2222-2222-222222222222", "23232323-2323-2323-2323-232323232323"), CancellationToken.None);
        (await conflict.Should().ThrowAsync<EnterpriseFaultException>()).Which.StatusCode.Should().Be(409);

        var http = _factory.CreateClient();
        http.Timeout = TimeSpan.FromMilliseconds(500);
        var slow = new MockEnterpriseClient(http, new FixedClock());
        slow.MockFault = "timeout";
        var timeout = async () => await slow.CreateAsync(Capa("24242424-2424-2424-2424-242424242424", "25252525-2525-2525-2525-252525252525"), CancellationToken.None);
        await timeout.Should().ThrowAsync<TaskCanceledException>();
    }

    private MockEnterpriseClient Client()
    {
        return new MockEnterpriseClient(_factory.CreateClient(), new FixedClock());
    }

    private static CapaAction Capa(string capaId, string incidentIdText)
    {
        var incidentId = new IncidentId(Guid.Parse(incidentIdText));
        const string summary = "Contain the chamber.";
        var token = ApprovalToken.Issue(
            new ApprovalTokenId(Guid.Parse("26262626-2626-2626-2626-262626262626")),
            "eng-17",
            CapaAction.ComputeActionHash(incidentId, summary),
            Now,
            TimeSpan.FromHours(1),
            Key);
        return CapaAction.Open(new CapaId(Guid.Parse(capaId)), incidentId, summary, token, Now, Key);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public sealed class ConnectorFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mock:SeedPath", SeedPath());
        builder.UseSetting("Mock:TimeoutSeconds", "2");
    }

    private static string SeedPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "seed", "enterprise.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("data/seed/enterprise.json was not found.");
    }
}
