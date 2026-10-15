// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

using Mps.Api.Persistence;
using Mps.Application.Persistence;
using Mps.Application.Tools;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;
using Mps.Simulator.Fleet;

namespace Mps.Api.Live;

/// <summary>
/// Telemetry, SPC and impact tools for the hosted demo. Telemetry is a deterministic baseline inside the
/// parameter window with the injected fault shape overlaid, so the agent reads the same signal the operator injected.
/// </summary>
public sealed class DemoSignalTools : ITelemetryTool, ISpcTool, IImpactScopeTool
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;

    public DemoSignalTools(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _scopes = scopes;
    }

    public async Task<TelemetryWindow> GetWindowAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var (mean, sigma) = await LimitsAsync(scope, toolId, parameter, cancellationToken).ConfigureAwait(false);
        var faults = await FaultsAsync(scope, toolId, parameter, cancellationToken).ConfigureAwait(false);
        var points = new List<TelemetryPoint>();
        var start = until - from > TimeSpan.FromDays(2) ? until.AddDays(-2) : from;
        var index = 0;
        for (var at = start; at <= until; at += Step, index++)
        {
            var wobble = Math.Sin(index * 0.7) * 0.35 * sigma;
            var value = mean + wobble;
            foreach (var fault in faults.Where(item => item.AcceptedAt <= at))
            {
                value += Overlay(fault, at, sigma, index);
            }

            points.Add(new TelemetryPoint(at, Math.Round(value, 4)));
        }

        return new TelemetryWindow(toolId, parameter, from, until, points);
    }

    public async Task<IReadOnlyList<SpcViolation>> GetViolationsAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var alerts = await scope.ServiceProvider.GetRequiredService<IAlertLog>().ListAsync(toolId, cancellationToken).ConfigureAwait(false);
        var (mean, sigma) = await LimitsAsync(scope, toolId, parameter, cancellationToken).ConfigureAwait(false);
        var faults = await FaultsAsync(scope, toolId, parameter, cancellationToken).ConfigureAwait(false);
        return alerts
            .Where(alert => string.Equals(alert.Parameter, parameter, StringComparison.OrdinalIgnoreCase)
                && alert.RaisedAt >= from && alert.RaisedAt <= until)
            .Select(alert =>
            {
                var magnitude = faults.LastOrDefault(item => item.AcceptedAt <= alert.RaisedAt)?.Magnitude ?? 3.5d;
                return new SpcViolation(alert.Rule, toolId, alert.ChamberId, alert.Parameter, alert.RaisedAt, mean + (magnitude * sigma), sigma, alert.Severity);
            })
            .ToArray();
    }

    public async Task<ImpactScope> GetScopeAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var faults = await FaultsAsync(scope, toolId, parameter, cancellationToken).ConfigureAwait(false);
        var fault = faults.LastOrDefault();
        if (fault?.ChamberId is { Length: > 0 } chamber)
        {
            return new ImpactScope(ImpactScopeKind.Chamber, toolId.Value + "/" + chamber, fault.AcceptedAt, "The shift starts when the fault was injected on this chamber only.");
        }

        return new ImpactScope(ImpactScopeKind.Tool, toolId.Value, fault?.AcceptedAt, fault is null ? "No injected fault was found for this parameter." : "The shift is visible on the tool.");
    }

    private static double Overlay(FaultRow fault, DateTimeOffset at, double sigma, int index)
    {
        var size = fault.Magnitude * sigma;
        var hours = (at - fault.AcceptedAt).TotalHours;
        return fault.Shape switch
        {
            "ramp" => size * Math.Min(1d, hours / 6d),
            "oscillation" => size * Math.Sin(index * 1.3),
            "intermittent" => index % 3 == 0 ? size : 0d,
            _ => size,
        };
    }

    private static async Task<(double Mean, double Sigma)> LimitsAsync(IServiceScope scope, ToolId toolId, string parameter, CancellationToken cancellationToken)
    {
        var tool = await scope.ServiceProvider.GetRequiredService<IFleetCatalog>().FindAsync(toolId, cancellationToken).ConfigureAwait(false);
        if (tool is not null && FamilyParameterCatalog.For(tool.Family).TryGetValue(parameter, out var limit))
        {
            var mean = (limit.Minimum + limit.Maximum) / 2d;
            return (mean, Math.Max((limit.Maximum - limit.Minimum) / 6d, 1e-6));
        }

        return (100d, 2d);
    }

    private static async Task<List<FaultRow>> FaultsAsync(IServiceScope scope, ToolId toolId, string parameter, CancellationToken cancellationToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        var rows = await db.Faults.AsNoTracking()
            .Where(row => row.ToolId == toolId.Value && row.Parameter == parameter)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.OrderBy(row => row.AcceptedAt).ToList();
    }
}
