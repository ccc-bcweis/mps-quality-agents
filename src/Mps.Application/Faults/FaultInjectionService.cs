// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Spc;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Faults;

public sealed record FaultInjectionRequest(
    string ToolId,
    string? ChamberId,
    string Parameter,
    string Shape,
    double Magnitude,
    RootCauseCategory RootCauseCategory,
    bool Confounders);

public sealed record FaultAccepted(Guid FaultId, string CorrelationId, Guid IncidentId);

public interface IFaultInjectionService
{
    Task<FaultAccepted> InjectAsync(FaultInjectionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Records the injected fault. Direct ticket creation is off unless <see cref="InjectorPolicy.DirectTicket"/> is set.
/// When that flag is on, the injector opens an incident and a magnitude past 3 sigma also raises an SPC alert.
/// </summary>
public sealed class FaultInjectionService : IFaultInjectionService
{
    private static readonly HashSet<string> Shapes = new(StringComparer.OrdinalIgnoreCase)
    {
        "step",
        "ramp",
        "oscillation",
        "intermittent",
    };

    private readonly IFleetCatalog _fleet;
    private readonly IFaultLog _faults;
    private readonly IIntakeService _intake;
    private readonly SpcAlertService _spc;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly InjectorPolicy _injector;

    public FaultInjectionService(
        IFleetCatalog fleet,
        IFaultLog faults,
        IIntakeService intake,
        SpcAlertService spc,
        IQualityNotifier notifier,
        TimeProvider time,
        InjectorPolicy injector)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(spc);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(injector);
        _fleet = fleet;
        _faults = faults;
        _intake = intake;
        _spc = spc;
        _notifier = notifier;
        _time = time;
        _injector = injector;
    }

    public async Task<FaultAccepted> InjectAsync(FaultInjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ToolId))
        {
            throw new IntakeValidationException("Tool id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Parameter))
        {
            throw new IntakeValidationException("Parameter is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Shape) || !Shapes.Contains(request.Shape.Trim()))
        {
            throw new IntakeValidationException("Fault shape must be step, ramp, oscillation, or intermittent.");
        }

        if (double.IsNaN(request.Magnitude) || double.IsInfinity(request.Magnitude))
        {
            throw new IntakeValidationException("Fault magnitude must be a finite number.");
        }

        var toolId = new ToolId(request.ToolId);
        if (await _fleet.FindAsync(toolId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new IntakeValidationException("Tool was not found.");
        }

        var now = _time.GetUtcNow();
        var faultId = Guid.NewGuid();
        var correlationId = "fault-" + faultId.ToString("N");
        var shape = request.Shape.Trim().ToLowerInvariant();
        var category = ToCamelCase(request.RootCauseCategory.ToString());
        var summary = "Injected " + shape + " fault on " + request.Parameter.Trim() + " (" + category + ").";
        var severity = Math.Abs(request.Magnitude) >= 4d ? Severity.Critical : Severity.High;
        var fault = new FaultRecord(
            faultId,
            toolId.ToString(),
            string.IsNullOrWhiteSpace(request.ChamberId) ? null : request.ChamberId.Trim(),
            request.Parameter.Trim(),
            shape,
            request.Magnitude,
            category,
            request.Confounders,
            correlationId,
            now);
        await _faults.AddAsync(fault, cancellationToken).ConfigureAwait(false);
        await _notifier.FaultInjectedAsync(faultId, correlationId, fault.ToolId, cancellationToken).ConfigureAwait(false);
        if (!_injector.DirectTicket)
        {
            return new FaultAccepted(faultId, correlationId, Guid.Empty);
        }

        var intake = await _intake.AcceptAsync(
            new IntakeSubmission(
                IncidentChannel.FaultInjector,
                fault.ToolId,
                fault.ChamberId,
                fault.Parameter,
                summary,
                severity,
                signalSeverity: null,
                correlationId,
                now),
            cancellationToken).ConfigureAwait(false);

        if (Math.Abs(request.Magnitude) > 3d)
        {
            var sample = new SpcSample(
                toolId,
                fault.ChamberId,
                fault.Parameter,
                new ControlBaseline(0d, 1d, null, null),
                [new TelemetryPoint(now, request.Magnitude)]);
            await _spc.RaiseAsync(sample, cancellationToken).ConfigureAwait(false);
        }

        return new FaultAccepted(faultId, correlationId, intake.Incident.Id.Value);
    }

    private static string ToCamelCase(string pascal)
    {
        if (pascal.Length == 0)
        {
            return pascal;
        }

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }
}
