// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics;
using System.Globalization;

using Mps.Api.Live;
using Mps.Application.Bus;
using Mps.Simulator.Sites;

namespace Mps.Api.Bus;

/// <summary>
/// Fills the telemetry table with a healthy history for every placed tool.
/// The last hour is one point every 25 seconds. Older points are one every 900 seconds.
/// A one-second last hour does not fit the seed time. The stride does not follow the sample rate,
/// so a busy machine does not thin the history. Full-rate totals are projected and are not inserted.
/// </summary>
public sealed class NominalHistorySeeder : INominalHistorySeeder
{
    private const int ChunkSize = EventBatch.Size;
    private const int SampleRows = 8000;
    private const int RecentStrideSeconds = 25;
    private const int OlderStrideSeconds = 900;

    private readonly PlantSimulation _plant;
    private readonly ILifecycleEventSink _sink;
    private readonly SqliteEventStore _store;

    public NominalHistorySeeder(PlantSimulation plant, ILifecycleEventSink sink, SqliteEventStore store)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(store);
        _plant = plant;
        _sink = sink;
        _store = store;
    }

    public async Task<NominalSeedResult> SeedAsync(NominalWindow window, CancellationToken cancellationToken = default)
    {
        var series = ListSeries();
        var until = _plant.SimulatedNow;
        var windowSeconds = Seconds(window);
        var from = until.AddSeconds(-windowSeconds);
        var projectedHour = (long)series.Count * 3600L;
        var projectedDay = (long)series.Count * 86400L;
        var projectedWeek = (long)series.Count * 7L * 86400L;
        if (series.Count == 0)
        {
            return Empty(window, projectedHour, projectedDay, projectedWeek);
        }

        var sample = await TimeSampleAsync(series[0], until, cancellationToken).ConfigureAwait(false);
        var rowsPerSecond = sample.Rows / Math.Max(sample.Seconds, 0.001d);
        var recentStride = RecentStrideSeconds;
        var olderStride = windowSeconds <= 3600 ? RecentStrideSeconds : OlderStrideSeconds;
        _store.ClearTelemetry();
        var seeded = await InsertAsync(series, from, until, recentStride, olderStride, cancellationToken).ConfigureAwait(false);
        var seedRate = seeded.Rows / Math.Max(seeded.Seconds, 0.001d);
        var chartTool = series[0].ToolId;
        var chartParameter = series[0].Parameter;
        var p15 = Percentile(chartTool, chartParameter, until.AddMinutes(-15), until);
        var p60 = Percentile(chartTool, chartParameter, until.AddHours(-1), until);
        var pWeek = Percentile(chartTool, chartParameter, until.AddDays(-7), until);
        var policy = string.Create(
            CultureInfo.InvariantCulture,
            $"Window ends at the simulator clock. The last hour uses a {recentStride} second stride. Older points use a {olderStride} second stride. Full-rate totals are projected from a {SampleRows} row sample and were not inserted.");
        return new NominalSeedResult(
            window,
            series.Select(item => item.ToolId).Distinct(StringComparer.Ordinal).Count(),
            series.Count,
            seeded.Rows,
            seedRate,
            seedRate,
            seeded.Seconds,
            _store.DatabaseBytes(),
            p15,
            p60,
            pWeek,
            policy,
            recentStride,
            olderStride,
            projectedHour,
            projectedDay,
            projectedWeek,
            rowsPerSecond);
    }

    private async Task<(long Rows, double Seconds)> TimeSampleAsync(Series series, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _store.ClearTelemetry();
        var started = Stopwatch.GetTimestamp();
        var rows = await WriteSeriesAsync(series, until.AddSeconds(-SampleRows), until, 1, 1, cancellationToken).ConfigureAwait(false);
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        _store.ClearTelemetry();
        return (rows, seconds);
    }

    private async Task<(long Rows, double Seconds)> InsertAsync(
        IReadOnlyList<Series> series,
        DateTimeOffset from,
        DateTimeOffset until,
        int recentStride,
        int olderStride,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        long rows = 0;
        foreach (var item in series)
        {
            rows += await WriteSeriesAsync(item, from, until, recentStride, olderStride, cancellationToken).ConfigureAwait(false);
        }

        return (rows, Stopwatch.GetElapsedTime(started).TotalSeconds);
    }

    private async Task<long> WriteSeriesAsync(
        Series series,
        DateTimeOffset from,
        DateTimeOffset until,
        int recentStride,
        int olderStride,
        CancellationToken cancellationToken)
    {
        var recentFrom = until.AddHours(-1);
        var state = Mix(series.ToolId + "|" + series.Parameter);
        var chunk = new List<LifecycleEvent>(ChunkSize);
        long rows = 0;
        for (var at = from; at <= until;)
        {
            var stride = at >= recentFrom ? recentStride : olderStride;
            if (stride < 1)
            {
                stride = 1;
            }

            var noise = NextSigned(ref state) * 0.1d * Math.Max((series.High - series.Low) / 6d, 1e-6);
            var value = Math.Clamp(series.Nominal + noise, series.Low, series.High);
            chunk.Add(new LifecycleEvent(
                LifecycleTypes.TelemetryReading,
                "nominal-" + series.ToolId,
                null,
                null,
                "seeder",
                series.SiteId,
                series.ToolId,
                series.ChamberId,
                at,
                new
                {
                    parameter = series.Parameter,
                    value,
                    unit = series.Unit,
                    low = series.Low,
                    high = series.High,
                }));
            if (chunk.Count >= ChunkSize)
            {
                rows += await FlushAsync(chunk, cancellationToken).ConfigureAwait(false);
            }

            var next = at.AddSeconds(stride);
            if (next <= at)
            {
                break;
            }

            at = next;
        }

        rows += await FlushAsync(chunk, cancellationToken).ConfigureAwait(false);
        return rows;
    }

    private async Task<long> FlushAsync(List<LifecycleEvent> chunk, CancellationToken cancellationToken)
    {
        if (chunk.Count == 0)
        {
            return 0;
        }

        var count = chunk.Count;
        await _sink.PublishManyAsync(chunk, cancellationToken).ConfigureAwait(false);
        chunk.Clear();
        return count;
    }

    private double Percentile(string toolId, string parameter, DateTimeOffset from, DateTimeOffset until)
    {
        for (var warmup = 0; warmup < 3; warmup++)
        {
            _ = _store.ReadChart(toolId, parameter, from, until);
        }

        var samples = new double[20];
        for (var index = 0; index < samples.Length; index++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = _store.ReadChart(toolId, parameter, from, until);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(samples);
        var rank = (int)Math.Ceiling(0.95d * samples.Length) - 1;
        return samples[Math.Clamp(rank, 0, samples.Length - 1)];
    }

    private List<Series> ListSeries()
    {
        var series = new List<Series>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in _plant.World.Sites)
        {
            foreach (var tool in site.Tools)
            {
                foreach (var sensor in tool.Spec.Sensors)
                {
                    var parameter = string.IsNullOrWhiteSpace(sensor.Parameter) ? sensor.Id : sensor.Parameter;
                    var key = tool.Id + "|" + parameter;
                    if (!seen.Add(key) || sensor.Limits.Maximum <= sensor.Limits.Minimum)
                    {
                        continue;
                    }

                    series.Add(new Series(
                        tool.SiteId,
                        tool.Id,
                        sensor.FrameChamberId(tool.Id),
                        parameter,
                        sensor.Unit,
                        sensor.Limits.Nominal,
                        sensor.Limits.Minimum,
                        sensor.Limits.Maximum));
                }
            }
        }

        return series;
    }

    private static int Seconds(NominalWindow window) => window switch
    {
        NominalWindow.LastHour => 3600,
        NominalWindow.LastDay => 86400,
        NominalWindow.LastWeek => 7 * 86400,
        _ => throw new ArgumentOutOfRangeException(nameof(window)),
    };

    private static NominalSeedResult Empty(NominalWindow window, long hour, long day, long week) =>
        new(window, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "No sensors were placed.", 1, 60, hour, day, week, 0);

    private static uint Mix(string text)
    {
        var hash = 2166136261u;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash == 0u ? 1u : hash;
    }

    private static double NextSigned(ref uint state)
    {
        state = (1664525u * state) + 1013904223u;
        return ((state / (double)uint.MaxValue) * 2d) - 1d;
    }

    private sealed record Series(
        string SiteId,
        string ToolId,
        string ChamberId,
        string Parameter,
        string Unit,
        double Nominal,
        double Low,
        double High);
}
