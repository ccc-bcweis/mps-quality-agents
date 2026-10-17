// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Domain.Telemetry;
using Mps.Simulator.Demo;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Live;

/// <summary>
/// One seeded simulator for every tool at every plant, plus the decimated series the UI and the agents read.
/// Fault onset is the simulator clock, so a prime of nominal history stays nominal.
/// </summary>
public sealed class PlantSimulation : Mps.Application.Lifecycle.IPlantFaultControl
{
    private readonly object _gate = new();
    private readonly SimulationOptions _options;
    private readonly LiveSeriesLog _log;
    private readonly List<TelemetryFrame> _pending = [];
    private readonly TimeProvider _time;
    private bool _primed;

    public PlantSimulation(SiteWorld world, TimeProvider time, IConfiguration configuration, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _options = options;
        var seed = configuration.GetValue("Mps:Demo:Seed", DemoRuntime.DefaultSeed);
        var tools = world.Sites
            .SelectMany(site => site.Tools)
            .Select(tool => tool.Spec)
            .GroupBy(tool => tool.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        _time = time;
        Runtime = new DemoRuntime(new FleetCatalog(tools), seed, time, options.RealTime ? RealTimeOrigin() : null);
        _log = new LiveSeriesLog(options.MaxPointsPerSeries);
    }

    public SiteWorld World { get; }

    public DemoRuntime Runtime { get; }

    public LiveSeriesLog Log => _log;

    public DateTimeOffset SimulatedNow => Runtime.Simulator.SimulatedNow;

    public SimulationOptions Options => _options;

    public void EnsurePrimed()
    {
        lock (_gate)
        {
            EnsurePrimedUnlocked();
        }
    }

    private int _speed = 1;
    private int _strideSeconds;

    /// <summary>Allowed harness speeds. 1x is the configured rate (1 wall second = 1 simulated minute by default).</summary>
    public static readonly IReadOnlyList<int> Speeds = [1, 2, 5, 10];

    /// <summary>Allowed seconds between kept samples.</summary>
    public static readonly IReadOnlyList<int> Strides = [5, 15, 30, 60];

    public int Speed => Volatile.Read(ref _speed);

    public int StrideSeconds => Volatile.Read(ref _strideSeconds) is var stride && stride > 0 ? stride : _options.SampleStrideSeconds;

    /// <summary>Sets the flow rate from the harness. Returns false for a value outside the allowed lists.</summary>
    public bool TrySetSpeed(int speed, int strideSeconds)
    {
        if (!Speeds.Contains(speed) || !Strides.Contains(strideSeconds))
        {
            return false;
        }

        Volatile.Write(ref _speed, speed);
        Volatile.Write(ref _strideSeconds, strideSeconds);
        return true;
    }

    public void AdvanceTick()
    {
        lock (_gate)
        {
            EnsurePrimedUnlocked();
            if (_options.RealTime)
            {
                // Catch up to the wall clock in whole seconds.
                var behind = Math.Floor((_time.GetUtcNow() - Runtime.Simulator.SimulatedNow).TotalSeconds);
                if (behind >= 1)
                {
                    AppendUnlocked(TimeSpan.FromSeconds(behind));
                }

                return;
            }

            var seconds = checked(_options.TickSeconds * _options.SimSecondsPerWallSecond * Speed);
            AppendUnlocked(TimeSpan.FromSeconds(seconds));
        }
    }

    /// <summary>Real time starts the plant clock just far enough back that priming ends at now().</summary>
    private DateTimeOffset RealTimeOrigin()
    {
        var now = _time.GetUtcNow();
        var whole = new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        return whole.AddSeconds(-_options.PrimeSeconds);
    }

    /// <summary>
    /// Real time keeps timestamps honest but would make a two-hour ramp take two hours. Faults injected in real time
    /// therefore develop <see cref="RealTimeFaultPace"/> x speed faster (period and ramp shortened). Readings still land at now().
    /// </summary>
    public const double RealTimeFaultPace = 6d;

    public double FaultPace => _options.RealTime ? RealTimeFaultPace * Math.Max(1, Speed) : 1d;

    private FaultSignal[] Pace(IEnumerable<FaultSignal> faults)
    {
        var pace = FaultPace;
        return faults.Select(fault => fault.Paced(pace)).ToArray();
    }

    /// <summary>In real time, speed shortens the gap between kept readings (stride / speed, at least one second).</summary>
    private int EffectiveStride => _options.RealTime ? Math.Max(1, StrideSeconds / Math.Max(1, Speed)) : StrideSeconds;

    public void Advance(TimeSpan simulatedElapsed)
    {
        lock (_gate)
        {
            AppendUnlocked(simulatedElapsed);
            _primed = true;
        }
    }

    public void Apply(ScriptedScenario scenario)
    {
        lock (_gate)
        {
            Runtime.Simulator.InjectAtSimulatedNow(onset => Pace(scenario.Signals(onset)));
        }
    }

    /// <summary>Applies the stream signal, which is a clear statistical step when the catalog magnitude is a percent.</summary>
    public void ApplyStream(ScriptedScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        lock (_gate)
        {
            Runtime.Simulator.InjectAtSimulatedNow(onset => Pace(scenario.StreamSignals(onset)));
        }
    }

    public bool TryApplyFreeForm(string toolId, string? chamberId, string parameter, string shape, double magnitude, TimeSpan? duration = null)
    {
        lock (_gate)
        {
            return Runtime.TryApplyFreeForm(toolId, chamberId, parameter, shape, magnitude, FaultPace, duration);
        }
    }

    public void Inject(FaultSignal fault)
    {
        lock (_gate)
        {
            Runtime.Simulator.Inject(fault.Paced(FaultPace));
        }
    }

    /// <summary>Removes the injected faults on one tool. Records and history stay.</summary>
    public int ClearTool(string toolId)
    {
        lock (_gate)
        {
            return Runtime.Simulator.RemoveFaults(fault => string.Equals(fault.ToolId, toolId, StringComparison.Ordinal));
        }
    }

    /// <summary>Removes every injected fault, so all telemetry returns to nominal. Records and history stay.</summary>
    public int ClearAll()
    {
        lock (_gate)
        {
            return Runtime.Simulator.RemoveFaults(_ => true);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (_options.RealTime)
            {
                Runtime.Reset(RealTimeOrigin());
            }
            else
            {
                Runtime.Reset();
            }

            _log.Clear();
            _pending.Clear();
            _primed = false;
            if (_options.Enabled)
            {
                EnsurePrimedUnlocked();
            }
        }
    }

    public Tool? FindTool(string toolId)
    {
        var placed = World.FindTool(toolId);
        return placed is null ? null : ToDomain(placed);
    }

    public IReadOnlyList<Tool> ToolsForSite(string siteId)
    {
        var site = World.Find(siteId);
        return site is null ? [] : site.Tools.Select(ToDomain).ToArray();
    }

    public IReadOnlyList<TelemetryFrame> TakePendingFrames()
    {
        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return [];
            }

            var copy = _pending.ToArray();
            _pending.Clear();
            return copy;
        }
    }

    /// <summary>Plain family word for a tool, such as Etch or Deposition. Null when the tool is unknown.</summary>
    public string? FamilyLabel(string toolId)
    {
        var tool = Runtime.Fleet.Tools.FirstOrDefault(candidate => string.Equals(candidate.Id, toolId, StringComparison.Ordinal));
        return tool?.Family.ToString();
    }

    public string DefaultChamber(string toolId)
    {
        var tool = Runtime.Fleet.Tools.FirstOrDefault(candidate => string.Equals(candidate.Id, toolId, StringComparison.Ordinal));
        return tool?.Chambers.Count > 0 ? tool.Chambers[0].Id : toolId + "-A";
    }

    /// <summary>Readable name of the recipe the tool runs at <paramref name="at"/>, from the fleet data; null for an unknown tool.</summary>
    public string? RecipeNameAt(string toolId, DateTimeOffset at)
    {
        try
        {
            var tool = Runtime.Fleet.GetRequired(toolId);
            var elapsed = (int)Math.Max(0, (at - Runtime.Simulator.Origin).TotalSeconds);
            return RecipeSchedule.Locate(tool, elapsed).Recipe.Name;
        }
        catch (FleetSpecException)
        {
            return null;
        }
    }

    public OperatingWindow? WindowAt(string toolId, string parameter, DateTimeOffset at)
    {
        ToolSpec tool;
        try
        {
            tool = Runtime.Fleet.GetRequired(toolId);
        }
        catch (FleetSpecException)
        {
            return null;
        }

        var elapsed = (int)Math.Max(0, (at - Runtime.Simulator.Origin).TotalSeconds);
        var schedule = RecipeSchedule.Locate(tool, elapsed);
        if (RecipeSchedule.Parameters(schedule.Recipe).Contains(parameter, StringComparer.Ordinal))
        {
            return RecipeSchedule.Resolve(schedule, parameter);
        }

        var sensor = tool.Sensors.FirstOrDefault(item =>
            string.Equals(item.Parameter, parameter, StringComparison.Ordinal)
            || string.Equals(item.Id, parameter, StringComparison.Ordinal));
        if (sensor is null || sensor.Limits.Maximum <= sensor.Limits.Minimum)
        {
            return null;
        }

        return new OperatingWindow(parameter, sensor.Unit, sensor.Limits.Minimum, sensor.Limits.Maximum, sensor.Limits.Nominal);
    }

    public IReadOnlyList<TelemetryPoint> ReadParameter(string toolId, string parameter, string? chamberId, DateTimeOffset from, DateTimeOffset until)
    {
        var points = _log.WindowForParameter(toolId, parameter, chamberId);
        if (points.Count == 0)
        {
            return [];
        }

        var inRange = points.Where(point => point.At >= from && point.At <= until).ToArray();
        var chosen = inRange.Length > 0 ? inRange : points;
        return chosen.Select(point => new TelemetryPoint(point.At, point.Value)).ToArray();
    }

    public IReadOnlyList<SpcViolation> Evaluate(string toolId, string parameter, DateTimeOffset from, DateTimeOffset until)
    {
        var chamber = Runtime.Simulator.ActiveFaults
            .LastOrDefault(fault => string.Equals(fault.ToolId, toolId, StringComparison.Ordinal)
                && string.Equals(fault.Parameter, parameter, StringComparison.Ordinal))
            ?.ChamberId
            ?? _log.NewestChamber(toolId, parameter);
        var points = ReadParameter(toolId, parameter, chamber, from, until);
        if (points.Count == 0)
        {
            return [];
        }

        var window = WindowAt(toolId, parameter, points[^1].At);
        if (window is null)
        {
            return [];
        }

        var sigma = Math.Max(window.Sigma, 1e-6);
        var baseline = new ControlBaseline(window.Nominal, sigma, window.Minimum, window.Maximum);
        return WesternElectricEvaluator.Evaluate(new ToolId(toolId), chamber ?? DefaultChamber(toolId), parameter, baseline, points);
    }

    private void EnsurePrimedUnlocked()
    {
        if (_primed)
        {
            return;
        }

        if (_options.PrimeSeconds > 0)
        {
            AppendUnlocked(TimeSpan.FromSeconds(_options.PrimeSeconds));
        }

        _primed = true;
    }

    private void AppendUnlocked(TimeSpan simulatedElapsed)
    {
        IngestUnlocked(Runtime.Simulator.Advance(simulatedElapsed, EffectiveStride));
    }

    /// <summary>
    /// The one ingest path for samples, from the tool's sensors and from bench printouts alike. Everything after this
    /// (charts, spec window, SPC and Western Electric rules, alerts, tickets, auto-clear) treats them the same.
    /// </summary>
    public void Ingest(IReadOnlyList<TelemetryFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        lock (_gate)
        {
            IngestUnlocked(frames);
        }
    }

    private void IngestUnlocked(IReadOnlyList<TelemetryFrame> frames)
    {
        _log.Append(frames);
        _pending.AddRange(frames);
    }

    private static Tool ToDomain(PlacedTool placed)
    {
        var chambers = placed.Spec.Chambers.Select(chamber => new Chamber(chamber.Id, chamber.Name)).ToArray();
        return new Tool(
            new ToolId(placed.Spec.Id),
            placed.Spec.Name,
            placed.Spec.Family,
            placed.Spec.Model,
            placed.Spec.Bay,
            chambers,
            placed.SiteId);
    }
}
