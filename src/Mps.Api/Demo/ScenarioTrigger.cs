// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Application.Bus;
using Mps.Application.Faults;
using Mps.Application.Printouts;
using Mps.Domain.Hypotheses;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Demo;

/// <summary>
/// Runs a named scenario: deterministic telemetry from the demo seed, plus the free-form fault record.
/// </summary>
public sealed class ScenarioTrigger
{
    private readonly PlantSimulation _plant;
    private readonly IFaultInjectionService _faults;
    private readonly ScriptedScenarioContext _context;
    private readonly IDocumentIntake _printouts;
    private readonly ScenarioEvidenceBoard? _board;
    private readonly ILifecycleEventSink? _events;
    private readonly TimeProvider? _time;

    public ScenarioTrigger(
        PlantSimulation plant,
        IFaultInjectionService faults,
        ScriptedScenarioContext context,
        IDocumentIntake printouts,
        ScenarioEvidenceBoard? board = null,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? cascade = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(printouts);
        _ = cascade;
        _plant = plant;
        _faults = faults;
        _context = context;
        _printouts = printouts;
        _board = board;
        _events = events;
        _time = time;
    }

    public async Task<ScenarioRun?> RunAsync(string scenarioId, CancellationToken cancellationToken)
    {
        if (!ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario))
        {
            return null;
        }

        _context.Set(scenario.Id);
        _board?.Activate(scenario.Id);
        var opened = await OpenCommandAsync(scenario, cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);
        try
        {
            if (string.Equals(scenario.Id, "SC-6", StringComparison.OrdinalIgnoreCase))
            {
                var fromPrintout = await RunOscilloscopePrintoutAsync(scenario, cancellationToken).ConfigureAwait(false);
                if (fromPrintout is not null)
                {
                    return fromPrintout;
                }

                // The file is missing, so the command subscriber did not apply a signal. Apply the stream signal once.
                _plant.ApplyStream(scenario);
            }

            var telemetry = ScriptedScenarioCatalog.Sample(scenario, _plant.Runtime.Fleet, _plant.Runtime.Seed);
            FaultAccepted? first = null;
            foreach (var toolId in scenario.ToolIds)
            {
                var accepted = await _faults.InjectAsync(
                    new FaultInjectionRequest(
                        toolId,
                        ScriptedScenario.ChamberIdFor(toolId),
                        scenario.Parameter,
                        scenario.ShapeName,
                        SigmaOf(scenario),
                        scenario.Category,
                        scenario.Confounders,
                        Scripted: true),
                    cancellationToken).ConfigureAwait(false);
                first ??= accepted;
            }

            var points = telemetry.Select(point => new ScenarioPointResponse(point.ToolId, point.ChamberId, point.Parameter, point.At, point.Value)).ToArray();
            return new ScenarioRun(
                scenario.Id,
                _plant.Runtime.Seed,
                first!.FaultId,
                first.CorrelationId,
                points);
        }
        finally
        {
            _context.Set(null);
        }
    }

    /// <summary>The injector takes sigma. A percent scenario (for example a 12 percent flow drop) maps to a clear 4.5 sigma step with the same sign.</summary>
    private static double SigmaOf(ScriptedScenario scenario) =>
        scenario.MagnitudeKind == MagnitudeKind.Percent ? Math.CopySign(4.5d, scenario.Magnitude) : scenario.Magnitude;

    /// <summary>
    /// SC-6 is the oscilloscope sheet. Confirming it injects the oscillation. The catalog signal is not applied as well.
    /// A missing file falls back to the normal scenario path.
    /// </summary>
    private async Task<ScenarioRun?> RunOscilloscopePrintoutAsync(ScriptedScenario scenario, CancellationToken cancellationToken)
    {
        var path = FindSample("oscilloscope-e01.pdf");
        if (path is null)
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var inspection = await _printouts.InspectAsync(bytes, "application/pdf", "oscilloscope-e01.pdf", cancellationToken).ConfigureAwait(false);
        var confirmed = await _printouts.ConfirmAsync(inspection.Reading, cancellationToken).ConfigureAwait(false);
        if (confirmed.FaultId is not Guid faultId || string.IsNullOrWhiteSpace(confirmed.CorrelationId))
        {
            return null;
        }

        var telemetry = ScriptedScenarioCatalog.Sample(scenario, _plant.Runtime.Fleet, _plant.Runtime.Seed);
        var points = telemetry.Select(point => new ScenarioPointResponse(point.ToolId, point.ChamberId, point.Parameter, point.At, point.Value)).ToArray();
        return new ScenarioRun(
            scenario.Id,
            _plant.Runtime.Seed,
            faultId,
            confirmed.CorrelationId,
            points);
    }

    private async ValueTask<CommandAmbient.Opened?> OpenCommandAsync(ScriptedScenario scenario, CancellationToken cancellationToken)
    {
        if (_events is null || CommandAmbient.EventId is not null)
        {
            return null;
        }

        var correlationId = "scenario-" + scenario.Id + "-" + Guid.NewGuid().ToString("N");
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.ScenarioStart,
                correlationId,
                null,
                null,
                "scenario",
                null,
                scenario.ToolIds.Count == 0 ? null : scenario.ToolIds[0],
                null,
                _time?.GetUtcNow() ?? DateTimeOffset.UtcNow,
                new { scenarioId = scenario.Id }),
            cancellationToken).ConfigureAwait(false);
        return new CommandAmbient.Opened(correlationId, eventId);
    }

    private static string? FindSample(string fileName)
    {
        // The hosted image keeps content under Mps__DataDirectory (/app/content/data); /app/data is the database folder.
        var configured = Environment.GetEnvironmentVariable("Mps__DataDirectory");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var hosted = Path.Combine(configured, "samples", fileName);
            if (File.Exists(hosted))
            {
                return hosted;
            }
        }

        var seeds = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var seed in seeds)
        {
            var dir = new DirectoryInfo(seed);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "data", "samples", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}

public sealed record ScenarioRun(
    string ScenarioId,
    int Seed,
    Guid FaultId,
    string CorrelationId,
    IReadOnlyList<ScenarioPointResponse> Telemetry);

public sealed record ScenarioPointResponse(string ToolId, string ChamberId, string Parameter, DateTimeOffset At, double Value);

public sealed record ScenarioDescription(
    string Id,
    string Title,
    RootCauseCategory RootCauseCategory,
    IReadOnlyList<string> ToolIds,
    string ChamberId,
    string Parameter,
    string Shape,
    string Summary,
    string WhatsHappening,
    string WhatWeSimulate,
    string WhatAiShouldFind,
    string ExpectedCause);
