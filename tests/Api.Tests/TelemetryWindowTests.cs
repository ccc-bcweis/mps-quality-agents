// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Api.Bus;
using Mps.Bus;
using Mps.Domain.Events;
using Mps.Simulator;

namespace Mps.Api.Tests;

/// <summary>Telemetry is a fixed rolling window per sensor, so memory and the database stay one size however long the plant runs.</summary>
public sealed class TelemetryWindowTests
{
    [Fact]
    public void TheWindowComesFromMetadataAndCoversWhatChartsRulesAndTheAiRead()
    {
        // Charts, the Western Electric rules and the AI's telemetry tool read at most the newest 600 readings of a series.
        SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor.Should().BeGreaterThanOrEqualTo(600);
        new Mps.Api.Live.SimulationOptions().MaxPointsPerSeries.Should().Be(SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor);
    }

    [Fact]
    public void ThePruneKeepsTheNewestReadingsOfEachSensor()
    {
        var path = Path.Combine(Path.GetTempPath(), "mps-window-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var store = new SqliteEventStore("Data Source=" + path) { TelemetryRowsPerSeries = 10 };
            var start = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
            // 30 readings for each of two sensors; one sensor has two chambers that are kept apart.
            var batch = new List<EventEnvelope>();
            for (var index = 0; index < 30; index++)
            {
                batch.Add(Reading("E-01", "E-01-A", "escTemperature", start.AddSeconds(index), index));
                batch.Add(Reading("E-01", "E-01-B", "escTemperature", start.AddSeconds(index), index));
                batch.Add(Reading("D-01", "D-01-A", "rfPower", start.AddSeconds(index), index));
            }

            store.AppendTelemetry(batch);
            store.CountTelemetry().Should().Be(90);

            store.PruneTelemetry(10).Should().Be(60);

            store.CountTelemetry().Should().Be(30);
            store.ReadChart("E-01", "escTemperature", start, start.AddSeconds(29)).Should().Be(20);
            store.ReadChart("D-01", "rfPower", start.AddSeconds(20), start.AddSeconds(29)).Should().Be(10, "the newest ten are kept");
            store.ReadChart("D-01", "rfPower", start, start.AddSeconds(19)).Should().Be(0, "older readings roll off");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void AppendingPrunesOnItsOwnOnceFiftyWindowsHaveArrived()
    {
        var path = Path.Combine(Path.GetTempPath(), "mps-window-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var store = new SqliteEventStore("Data Source=" + path) { TelemetryRowsPerSeries = 2 };
            var start = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
            // 100 rows for one sensor = 50 windows of 2: the append that reaches it prunes back to the window.
            store.AppendTelemetry(Enumerable.Range(0, 100).Select(index => Reading("E-01", "E-01-A", "escTemperature", start.AddSeconds(index), index)).ToList());
            store.CountTelemetry().Should().Be(2);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }

    private static EventEnvelope Reading(string tool, string chamber, string parameter, DateTimeOffset at, double value)
    {
        var payload = JsonSerializer.SerializeToElement(new { parameter, value, unit = "C", low = 0d, high = 100d });
        return new EventEnvelope(Guid.NewGuid().ToString("N"), EventTypes.TelemetryReading, "c-" + tool, null, null, 0, "test", "northwater", tool, chamber, at, at, EventEnvelope.CurrentSchemaVersion, payload);
    }
}
