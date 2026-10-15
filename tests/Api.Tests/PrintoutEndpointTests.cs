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
        second.ToString().Should().Be(first.ToString());
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
