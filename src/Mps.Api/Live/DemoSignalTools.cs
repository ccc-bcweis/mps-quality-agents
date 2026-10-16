// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Persistence;
using Mps.Application.Tools;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Telemetry;

namespace Mps.Api.Live;

/// <summary>
/// Telemetry, SPC and impact tools for the hosted demo. When the plant simulator has samples, the agent
/// reads that stream. Without samples it returns no readings and no violations; it never reads the injected fault record.
/// </summary>
public sealed class DemoSignalTools : ITelemetryTool, ISpcTool, IImpactScopeTool
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;

    private readonly CrossSiteMaterialCatalog _sharedLots;

    private readonly PlantSimulation? _plant;

    public DemoSignalTools(IServiceScopeFactory scopes, CrossSiteMaterialCatalog sharedLots, PlantSimulation? plant = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(sharedLots);
        _scopes = scopes;
        _sharedLots = sharedLots;
        _plant = plant;
    }

    public Task<TelemetryWindow> GetWindowAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var live = LivePoints(toolId, parameter, from, until);
        if (live.Count == 0)
        {
            return Task.FromResult(new TelemetryWindow(toolId, parameter, from, until < from ? from : until, []));
        }

        var start = live.Min(point => point.At);
        var end = live.Max(point => point.At);
        return Task.FromResult(new TelemetryWindow(toolId, parameter, from < start ? from : start, end, live));
    }

    public Task<IReadOnlyList<SpcViolation>> GetViolationsAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        IReadOnlyList<SpcViolation> live = _plant is null ? [] : _plant.Evaluate(toolId.Value, parameter, from, until);
        return Task.FromResult(live);
    }

    private IReadOnlyList<TelemetryPoint> LivePoints(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until)
    {
        if (_plant is null)
        {
            return [];
        }

        var chamber = _plant.Runtime.Simulator.ActiveFaults
            .LastOrDefault(fault => string.Equals(fault.ToolId, toolId.Value, StringComparison.Ordinal)
                && string.Equals(fault.Parameter, parameter, StringComparison.Ordinal))
            ?.ChamberId;
        return _plant.ReadParameter(toolId.Value, parameter, chamber, from, until);
    }

    public async Task<ImpactScope> GetScopeAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var shared = _sharedLots.Find(toolId.Value);
        if (shared is not null)
        {
            return shared;
        }

        using var scope = _scopes.CreateScope();
        var alerts = await scope.ServiceProvider.GetRequiredService<IAlertLog>().ListAsync(toolId, cancellationToken).ConfigureAwait(false);
        var latest = alerts
            .Where(alert => string.Equals(alert.Parameter, parameter, StringComparison.OrdinalIgnoreCase) && alert.RaisedAt <= until)
            .OrderBy(alert => alert.RaisedAt)
            .LastOrDefault();
        if (latest?.ChamberId is { Length: > 0 } chamber)
        {
            return new ImpactScope(ImpactScopeKind.Chamber, toolId.Value + "/" + chamber, latest.RaisedAt, "The alarm on this parameter is on this chamber only.");
        }

        return new ImpactScope(ImpactScopeKind.Tool, toolId.Value, latest?.RaisedAt, latest is null ? "No alarm was found for this parameter." : "The alarm is on the tool.");
    }
}
