// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Application.Bus;

using Xunit.Abstractions;

namespace Mps.Api.Tests;

/// <summary>
/// Seeds the chosen nominal history and records the full-rate sample.
/// Hour, day, and week totals at one reading per second are projections. They are not inserted.
/// </summary>
public sealed class BusThroughputTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly ITestOutputHelper _output;

    public BusThroughputTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("perf", "bus")]
    public async Task NominalWeekPublishesEveryPlantAtTheChosenStride()
    {
        await using var factory = new ApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<INominalHistorySeeder>();

        var result = await seeder.SeedAsync(NominalWindow.LastWeek);

        var json = JsonSerializer.Serialize(result, Json);
        var path = Path.Combine(Path.GetTempPath(), "mps-bus-throughput.json");
        await File.WriteAllTextAsync(path, json);
        _output.WriteLine(path);
        _output.WriteLine(json);

        result.ToolCount.Should().BeGreaterThan(8);
        result.ParameterCount.Should().BeGreaterThan(result.ToolCount);
        result.ReadingCount.Should().BeGreaterThan(0);
        result.SampleEventsPerSecond.Should().BeGreaterThan(0);
        result.EventsPerSecond.Should().BeGreaterThan(0);
        result.RowsPerSecond.Should().BeGreaterThan(0);
        result.SeedSeconds.Should().BeLessThan(60);
        result.DatabaseBytes.Should().BeGreaterThan(0);
        result.ProjectedRows1Hour.Should().BeGreaterThan(0);
        result.ProjectedRows1Day.Should().BeGreaterThan(result.ProjectedRows1Hour);
        result.ProjectedRows7Days.Should().BeGreaterThan(result.ProjectedRows1Day);
        result.ChartP95Milliseconds15Minutes.Should().BeLessThan(500);
        result.ChartP95Milliseconds1Hour.Should().BeLessThan(500);
        result.ChartP95Milliseconds7Days.Should().BeLessThan(500);
        result.Policy.Should().NotBeNullOrWhiteSpace();
    }
}
