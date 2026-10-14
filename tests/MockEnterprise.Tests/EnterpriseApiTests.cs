// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mps.MockEnterprise.Tests;

public sealed class EnterpriseApiTests : IClassFixture<EnterpriseFactory>
{
    private readonly EnterpriseFactory _factory;

    public EnterpriseApiTests(EnterpriseFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReadsMatchTheOpenApiFiltersAndHideTheAnswer()
    {
        var client = _factory.CreateClient();
        var runs = await JsonArray(client, "/mes/runs?toolId=E-02&from=2026-09-28T14:00:00Z&until=2026-09-28T14:00:00Z");
        runs.Select(run => run.GetProperty("runId").GetString()).Should().Contain("RUN-FLT-ECO-4471-E-02-00");
        runs.Should().OnlyContain(run => run.GetProperty("toolId").ValueKind == JsonValueKind.String);
        runs.Should().OnlyContain(run => run.GetProperty("startedAt").GetString() == "2026-09-28T14:00:00Z");

        var lots = await JsonArray(client, "/mes/lots?toolId=E-02");
        lots.Should().Contain(lot => lot.GetProperty("lotId").GetString() == "NF-3317");

        var ncrs = await JsonArray(client, "/qms/ncrs?toolId=C-01");
        ncrs.Should().Contain(ncr => ncr.GetProperty("ncrId").GetString() == "NCR-C-01-104");

        var ecos = await JsonArray(client, "/plm/ecos?toolId=E-02");
        ecos.Select(eco => eco.GetProperty("ecoId").GetString()).Should().Contain("ECO-4471").And.NotContain("ECO-4502");

        var config = await JsonObject(client, "/plm/config?toolId=D-01");
        config.GetProperty("revision").GetString().Should().Be("C");
        config.GetProperty("partName").GetString().Should().Be("Showerhead");

        var materials = await client.GetStringAsync("/erp/lots?toolId=E-01");
        materials.Should().Contain("NF-3317");
        materials.Should().NotContain("toolIds");
        materials.Should().NotContain("rootCause");
        var depositionLots = await JsonArray(client, "/erp/lots?toolId=D-01");
        depositionLots.Should().NotContain(lot => lot.GetProperty("lotId").GetString() == "NF-3317");

        var notices = await JsonArray(client, "/erp/notices?materialCode=GAS-NF3-O2");
        notices.Select(notice => notice.GetProperty("noticeId").GetString()).Should().Equal("SN-NF-3317");

        var orders = await JsonArray(client, "/cmms/work-orders?toolId=E-02");
        orders.Should().Contain(order => order.GetProperty("workOrderId").GetString() == "WO-PM-8821");

        var calibrations = await JsonArray(client, "/cmms/calibrations?toolId=S-02");
        var overdue = calibrations.Should().ContainSingle().Subject;
        overdue.GetProperty("calibrationId").GetString().Should().Be("CAL-S02-O2-MFC");
        overdue.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
        overdue.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
        var depositionCals = await JsonArray(client, "/cmms/calibrations?toolId=D-01");
        depositionCals.Should().NotContain(record => record.GetProperty("calibrationId").GetString() == "CAL-D01-MANOMETER");
    }

    [Fact]
    public async Task MissingToolIdIsRejectedAndReadsIgnoreTheFaultHeader()
    {
        var client = _factory.CreateClient();
        var missing = await client.GetAsync("/mes/runs");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var request = new HttpRequestMessage(HttpMethod.Get, "/plm/config?toolId=E-02");
        request.Headers.TryAddWithoutValidation("X-Mock-Fault", "500");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WritesReturnCreatedThenConflictAndHonorTheFaultHeader()
    {
        var client = _factory.CreateClient();
        var capa = new
        {
            capaId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            incidentId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            approvalTokenId = "cccccccc-cccc-cccc-cccc-cccccccccccc",
            summary = "Hold the lot.",
        };
        var created = await client.PostAsJsonAsync("/qms/capas", capa);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("externalId").GetString().Should().Be("QMS-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var duplicate = await client.PostAsJsonAsync("/qms/capas", capa);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync("/qms/capas", new
        {
            capaId = "dddddddd-dddd-dddd-dddd-dddddddddddd",
            incidentId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            approvalTokenId = "cccccccc-cccc-cccc-cccc-cccccccccccc",
            summary = "Hold the lot.",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var faulted = new HttpRequestMessage(HttpMethod.Post, "/qms/capas")
        {
            Content = JsonContent.Create(new
            {
                capaId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
                incidentId = "ffffffff-ffff-ffff-ffff-ffffffffffff",
                approvalTokenId = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                summary = "Hold the lot.",
            }),
        };
        faulted.Headers.TryAddWithoutValidation("X-Mock-Fault", "500");
        (await client.SendAsync(faulted)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var conflict = new HttpRequestMessage(HttpMethod.Post, "/ticketing/tickets")
        {
            Content = JsonContent.Create(new
            {
                incidentId = "12121212-1212-1212-1212-121212121212",
                toolId = "E-02",
                title = "Forced conflict",
            }),
        };
        conflict.Headers.TryAddWithoutValidation("X-Mock-Fault", "409");
        (await client.SendAsync(conflict)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var ticket = await client.PostAsJsonAsync("/ticketing/tickets", new
        {
            incidentId = "13131313-1313-1313-1313-131313131313",
            toolId = "E-02",
            title = "ESC oscillation",
        });
        ticket.StatusCode.Should().Be(HttpStatusCode.Created);
        var number = (await ticket.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("number").GetString();
        number.Should().StartWith("TKT-");
        (await client.PostAsJsonAsync("/ticketing/tickets", new
        {
            incidentId = "13131313-1313-1313-1313-131313131313",
            toolId = "E-02",
            title = "ESC oscillation",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TimeoutHeaderDelaysUntilTheClientCancels()
    {
        var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromMilliseconds(500);
        var request = new HttpRequestMessage(HttpMethod.Post, "/qms/capas")
        {
            Content = JsonContent.Create(new
            {
                capaId = "14141414-1414-1414-1414-141414141414",
                incidentId = "15151515-1515-1515-1515-151515151515",
                approvalTokenId = "16161616-1616-1616-1616-161616161616",
                summary = "This write should time out.",
            }),
        };
        request.Headers.TryAddWithoutValidation("X-Mock-Fault", "timeout");
        var act = async () => await client.SendAsync(request);
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    private static async Task<JsonElement[]> JsonArray(HttpClient client, string path)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>(path);
        return payload.EnumerateArray().ToArray();
    }

    private static async Task<JsonElement> JsonObject(HttpClient client, string path)
    {
        return await client.GetFromJsonAsync<JsonElement>(path);
    }
}

public sealed class EnterpriseFactory : WebApplicationFactory<Program>
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
