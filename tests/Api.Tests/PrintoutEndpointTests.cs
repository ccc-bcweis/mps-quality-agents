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
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var readings = await client.GetFromJsonAsync<JsonElement>("/api/manual-readings");
        var runtime = factory.Services.GetRequiredService<DemoRuntime>();

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("catalogParameter").GetString().Should().Be("rfSourcePower");
        body.GetProperty("printedParameter").GetString().Should().Be("rfPower");
        body.GetProperty("appliedToSimulation").GetBoolean().Should().BeTrue();
        body.GetProperty("faultId").ValueKind.Should().Be(JsonValueKind.Null);
        runtime.Simulator.ActiveFaults.Should().BeEmpty("a printout is a sample, not a fault");
        alerts.EnumerateArray().Should().NotContain(alert => alert.GetProperty("rule").GetString() == "manualMeasurement");
        readings.EnumerateArray().Should().ContainSingle(item => item.GetProperty("parameter").GetString() == "rfSourcePower");
    }

    [Theory]
    [InlineData("oscilloscope-e01.pdf", "application/octet-stream", "application/pdf")]
    [InlineData("thermocouple-logger-e01.png", "application/octet-stream", "image/png")]
    public async Task ThePrintoutFileOpensInlineWithItsRealType(string fileName, string uploadedAs, string expected)
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var inspection = await InspectAsync(client, fileName, uploadedAs);
        var body = JsonSerializer.SerializeToNode(ConfirmBody(inspection), Json)!.AsObject();
        body["fileId"] = inspection.GetProperty("fileId").GetString();
        var confirmed = await client.PostAsJsonAsync("/api/printouts/confirm", body);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        var readings = await client.GetFromJsonAsync<JsonElement>("/api/manual-readings");
        var url = readings.EnumerateArray().Single().GetProperty("fileUrl").GetString()!;

        var file = await client.GetAsync(url);

        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be(expected);
        file.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        file.Content.Headers.ContentDisposition.FileNameStar.Should().Be(fileName);
        file.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(await File.ReadAllBytesAsync(Path.Combine(Root(), "data", "samples", fileName)));
    }

    [Fact]
    public void UnknownBytesDownloadInsteadOfOpening()
    {
        Mps.Api.Printouts.PrintoutContentType.Detect("<html>"u8).Should().Be("application/octet-stream");
        Mps.Api.Printouts.PrintoutContentType.ShowsInline("application/octet-stream").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mock")]
    public async Task ThermocoupleScanParseIsDeterministic(string? mode)
    {
        await using var factory = mode is null
            ? new ApiFactory()
            : new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Model:Mode", mode));
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
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var readings = await client.GetFromJsonAsync<JsonElement>("/api/manual-readings");
        var runtime = factory.Services.GetRequiredService<DemoRuntime>();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        alerts.EnumerateArray().Should().NotContain(alert => alert.GetProperty("rule").GetString() == "manualMeasurement");
        readings.EnumerateArray().Should().ContainSingle();
        runtime.Simulator.ActiveFaults.Should().ContainSingle();
        runtime.Simulator.ActiveFaults[0].Parameter.Should().Be("rfSourcePower");
        runtime.Simulator.ActiveFaults[0].Shape.Should().Be(FaultShape.Oscillation);
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



    private static object Corrected(JsonElement inspection, DateTimeOffset observedAt, DateTimeOffset printed, Guid fileId, double? value = null)
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
