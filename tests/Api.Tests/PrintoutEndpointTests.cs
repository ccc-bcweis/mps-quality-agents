// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Simulator.Demo;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

public sealed class PrintoutEndpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task OscilloscopePdfConfirmsAsEvidenceAndAFault()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var inspection = await InspectAsync(client, "oscilloscope-e01.pdf", "application/pdf");

        inspection.GetProperty("parseMethod").GetString().Should().Be("text");
        inspection.GetProperty("toolId").GetString().Should().Be("E-01");
        inspection.GetProperty("parameter").GetString().Should().Be("rfPower");
        inspection.GetProperty("value").GetDouble().Should().Be(2140d);
        inspection.GetProperty("outOfSpec").GetBoolean().Should().BeTrue();

        var confirmed = await client.PostAsJsonAsync("/api/printouts/confirm", ConfirmBody(inspection));
        var body = await confirmed.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var incidentId = body.GetProperty("incidentId").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var investigation = await started.Content.ReadFromJsonAsync<JsonElement>(Json);

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("catalogParameter").GetString().Should().Be("rfSourcePower");
        body.GetProperty("printedParameter").GetString().Should().Be("rfPower");
        body.GetProperty("appliedToSimulation").GetBoolean().Should().BeTrue();
        body.GetProperty("faultId").GetGuid().Should().NotBeEmpty();
        incidents.EnumerateArray().Should().ContainSingle();
        incidents[0].GetProperty("summary").GetString().Should().StartWith("Manual measurement");
        incidents[0].GetProperty("parameter").GetString().Should().Be("rfSourcePower");
        alerts.EnumerateArray().Should().Contain(alert => alert.GetProperty("rule").GetString() == "manualMeasurement");
        started.StatusCode.Should().Be(HttpStatusCode.Created);
        investigation.GetProperty("evidence").EnumerateArray().Should().Contain(item => item.GetProperty("source").GetString() == "manualMeasurement");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mock")]
    public async Task ThermocoupleScanParseIsDeterministic(string? mode)
    {
        await using var factory = mode is null
            ? new ApiFactory()
            : new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("LLM_MODE", mode));
        var client = factory.CreateClient();

        var first = await InspectAsync(client, "thermocouple-logger-e01.png", "image/png");
        var second = await InspectAsync(client, "thermocouple-logger-e01.png", "image/png");

        first.GetProperty("parseMethod").GetString().Should().Be("model");
        first.GetProperty("instrument").GetString().Should().Be("thermocoupleLogger");
        first.GetProperty("parameter").GetString().Should().Be("escTemperature");
        first.GetProperty("value").GetDouble().Should().Be(68d);
        first.GetProperty("outOfSpec").GetBoolean().Should().BeTrue();
        // Every upload is kept as its own file; the parsed reading is what must not change.
        WithoutFileId(second).Should().Be(WithoutFileId(first));
    }

    [Fact]
    public async Task OscilloscopeScenarioAppliesThePrintoutOnce()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/injector/scenarios/SC-6", null);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var runtime = factory.Services.GetRequiredService<DemoRuntime>();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        incidents.EnumerateArray().Should().ContainSingle();
        incidents[0].GetProperty("summary").GetString().Should().StartWith("Manual measurement");
        alerts.EnumerateArray().Should().ContainSingle(alert => alert.GetProperty("rule").GetString() == "manualMeasurement");
        runtime.Simulator.ActiveFaults.Should().ContainSingle();
        runtime.Simulator.ActiveFaults[0].Parameter.Should().Be("rfSourcePower");
        runtime.Simulator.ActiveFaults[0].Shape.Should().Be(FaultShape.Oscillation);
    }

    [Theory]
    [InlineData("oscilloscope-e01.pdf", "application/pdf", "E-01")]
    [InlineData("rf-power-meter-e02.pdf", "application/pdf", "E-02")]
    [InlineData("particle-counter-d01.pdf", "application/pdf", "D-01")]
    [InlineData("thermocouple-logger-e01.pdf", "application/pdf", "E-01")]
    public async Task EverySamplePrintoutIsOutOfSpecAndConfirmRaisesAnAlert(string fileName, string mediaType, string toolId)
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var inspection = await InspectAsync(client, fileName, mediaType);
        var confirmed = await client.PostAsJsonAsync("/api/printouts/confirm", ConfirmBody(inspection));
        var body = await confirmed.Content.ReadFromJsonAsync<JsonElement>(Json);
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");

        inspection.GetProperty("toolId").GetString().Should().Be(toolId);
        inspection.GetProperty("outOfSpec").GetBoolean().Should().BeTrue();
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("appliedToSimulation").GetBoolean().Should().BeTrue();
        alerts.EnumerateArray().Should().Contain(alert => alert.GetProperty("toolId").GetString() == toolId);
    }

    [Fact]
    public async Task ANowCorrectedPrintoutKeepsThePrintedTimeAndShowsAsAManualReadingWithItsFileAndAlert()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var inspection = await InspectAsync(client, "thermocouple-logger-e01.pdf", "application/pdf");
        var printed = inspection.GetProperty("observedAt").GetDateTimeOffset();
        var now = new DateTimeOffset(2026, 10, 15, 23, 0, 0, TimeSpan.Zero);
        var fileId = inspection.GetProperty("fileId").GetGuid();

        var confirmed = await client.PostAsJsonAsync("/api/printouts/confirm", Corrected(inspection, now, printed, fileId));
        var body = await confirmed.Content.ReadFromJsonAsync<JsonElement>(Json);
        var rows = await client.GetFromJsonAsync<JsonElement>("/api/manual-readings?toolId=E-01");
        var row = rows.EnumerateArray().Single();

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        row.GetProperty("observedAt").GetDateTimeOffset().Should().Be(now);
        row.GetProperty("printedObservedAt").GetDateTimeOffset().Should().Be(printed);
        row.GetProperty("parameter").GetString().Should().Be("escTemperature");
        row.GetProperty("value").GetDouble().Should().Be(68d);
        row.GetProperty("outOfSpec").GetBoolean().Should().BeTrue();
        row.GetProperty("siteId").GetString().Should().Be("northwater");
        row.GetProperty("alertId").GetGuid().Should().Be(body.GetProperty("alertId").GetGuid());
        row.GetProperty("source").GetString().Should().Contain("thermocouple logger").And.Contain("thermocouple-logger-e01.pdf").And.Contain("out of spec");
        var file = await client.GetAsync(row.GetProperty("fileUrl").GetString());
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var alert = (await client.GetFromJsonAsync<JsonElement>("/api/alerts")).EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == body.GetProperty("alertId").GetGuid());
        alert.GetProperty("severity").GetString().Should().Be("high", "a reading outside the printed spec is an alarm, not a watch");
        var tool = await client.GetFromJsonAsync<JsonElement>("/api/tools/E-01/live");
        tool.GetProperty("status").GetString().Should().Be("alarm");
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit");
        audit.GetProperty("entries").EnumerateArray().Should().Contain(entry =>
            entry.GetProperty("kind").GetString() == "manualReading"
            && entry.GetProperty("summary").GetString()!.Contains("corrected by the user from the printed", StringComparison.Ordinal));
    }

    [Fact]
    public void AnObservedTimeBeforeTheChartWindowIsFlagged()
    {
        using var factory = new ApiFactory();
        var plant = factory.Services.GetRequiredService<Mps.Api.Live.PlantSimulation>();
        for (var tick = 0; tick < 3; tick++)
        {
            plant.AdvanceTick();
        }

        var first = plant.Log.Window("E-01", "E-01-A", "escTemperature")[0].At;
        Mps.Api.Http.ManualReadings.OlderThanWindow(plant, "E-01", "E-01-A", "escTemperature", first.AddDays(-3)).Should().BeTrue();
        Mps.Api.Http.ManualReadings.OlderThanWindow(plant, "E-01", "E-01-A", "escTemperature", first.AddMinutes(1)).Should().BeFalse();
    }

    private static object Corrected(JsonElement inspection, DateTimeOffset observedAt, DateTimeOffset printed, Guid fileId)
    {
        return new
        {
            toolId = inspection.GetProperty("toolId").GetString(),
            chamberId = inspection.GetProperty("chamberId").GetString(),
            observedAt,
            printedObservedAt = printed,
            fileId,
            instrument = inspection.GetProperty("instrument").GetString(),
            parameter = inspection.GetProperty("parameter").GetString(),
            value = inspection.GetProperty("value").GetDouble(),
            unit = inspection.GetProperty("unit").GetString(),
            specMinimum = inspection.GetProperty("specMinimum").GetDouble(),
            specMaximum = inspection.GetProperty("specMaximum").GetDouble(),
            shape = inspection.GetProperty("shape").ValueKind == JsonValueKind.Null ? null : inspection.GetProperty("shape").GetString(),
            operatorInitials = inspection.GetProperty("operatorInitials").GetString(),
        };
    }

    private static string WithoutFileId(JsonElement inspection) =>
        string.Join("|", inspection.EnumerateObject().Where(property => property.Name != "fileId").Select(property => property.Name + "=" + property.Value));

    private static async Task<JsonElement> InspectAsync(HttpClient client, string fileName, string mediaType)
    {
        using var content = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Root(), "data", "samples", fileName));
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(file, "file", fileName);
        var response = await client.PostAsync("/api/printouts/inspect", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json))!;
    }

    private static object ConfirmBody(JsonElement inspection)
    {
        return new
        {
            toolId = inspection.GetProperty("toolId").GetString(),
            chamberId = inspection.GetProperty("chamberId").GetString(),
            observedAt = inspection.GetProperty("observedAt").GetDateTimeOffset(),
            instrument = inspection.GetProperty("instrument").GetString(),
            parameter = inspection.GetProperty("parameter").GetString(),
            value = inspection.GetProperty("value").GetDouble(),
            unit = inspection.GetProperty("unit").GetString(),
            specMinimum = inspection.GetProperty("specMinimum").GetDouble(),
            specMaximum = inspection.GetProperty("specMaximum").GetDouble(),
            shape = inspection.GetProperty("shape").ValueKind == JsonValueKind.Null ? null : inspection.GetProperty("shape").GetString(),
            operatorInitials = inspection.GetProperty("operatorInitials").GetString(),
        };
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("MPS.sln was not found above the test output directory.");
    }
}
