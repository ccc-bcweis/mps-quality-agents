// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

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
/// Records the injected fault. SPC alerts are raised later, when the live stream crosses a rule.
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
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;

    public FaultInjectionService(
        IFleetCatalog fleet,
        IFaultLog faults,
        IQualityNotifier notifier,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        _fleet = fleet;
        _faults = faults;
        _notifier = notifier;
        _time = time;
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
        return new FaultAccepted(faultId, correlationId, Guid.Empty);
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
