// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Approval;
using Mps.Domain.Identity;

namespace Mps.Application.Persistence;

public static class ApprovalOutcomes
{
    public const string Writing = "writing";

    public const string Succeeded = "succeeded";

    public const string Failed = "failed";

    public const string ClosedByDemoReset = "closedByDemoReset";
}

public sealed record StoredApproval(
    ApprovalToken Token,
    InvestigationId InvestigationId,
    IncidentId IncidentId,
    string Summary,
    CapaId CapaId,
    string Decision,
    string Outcome,
    string? Comment,
    string? ExternalCapaId,
    int Attempts,
    string? FailureDetail);

public interface IApprovalLog
{
    Task AddAsync(StoredApproval approval, CancellationToken cancellationToken);

    Task UpdateAsync(StoredApproval approval, CancellationToken cancellationToken);

    Task<StoredApproval?> FindByInvestigationAsync(InvestigationId investigationId, CancellationToken cancellationToken);

    /// <summary>
    /// Every approval attempt for the investigation, oldest first. A retry keeps the earlier row.
    /// </summary>
    Task<IReadOnlyList<StoredApproval>> ListByInvestigationAsync(InvestigationId investigationId, CancellationToken cancellationToken);
}
