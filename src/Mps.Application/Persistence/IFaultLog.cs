// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Persistence;

public sealed class FaultRecord
{
    public FaultRecord(
        Guid id,
        string toolId,
        string? chamberId,
        string parameter,
        string shape,
        double magnitude,
        string rootCauseCategory,
        bool confounders,
        string correlationId,
        DateTimeOffset acceptedAt)
    {
        Id = id;
        ToolId = toolId;
        ChamberId = chamberId;
        Parameter = parameter;
        Shape = shape;
        Magnitude = magnitude;
        RootCauseCategory = rootCauseCategory;
        Confounders = confounders;
        CorrelationId = correlationId;
        AcceptedAt = acceptedAt;
    }

    public Guid Id { get; }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public string Shape { get; }

    public double Magnitude { get; }

    public string RootCauseCategory { get; }

    public bool Confounders { get; }

    public string CorrelationId { get; }

    public DateTimeOffset AcceptedAt { get; }
}

public interface IFaultLog
{
    Task AddAsync(FaultRecord fault, CancellationToken cancellationToken);
}
