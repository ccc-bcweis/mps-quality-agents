// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Mps.Domain;
using Mps.Domain.Approval;
using Mps.Domain.Identity;
using Mps.Domain.Lifecycle;

namespace Mps.Domain.Capa;

public enum CapaStage
{
    Open,
    Containment,
    RootCauseConfirmed,
    CorrectiveAction,
    Verification,
    Closed,
}

/// <summary>
/// A corrective action. Construction requires an approval token, and rehydration still requires the token id.
/// </summary>
public sealed class CapaAction
{
    private CapaAction(
        CapaId id,
        IncidentId incidentId,
        string summary,
        ApprovalTokenId approvalTokenId,
        string actionHash,
        CapaStage stage,
        DateTimeOffset openedAt)
    {
        Id = id;
        IncidentId = incidentId;
        Summary = summary;
        ApprovalTokenId = approvalTokenId;
        ActionHash = actionHash;
        Stage = stage;
        OpenedAt = openedAt;
    }

    public CapaId Id { get; }

    public IncidentId IncidentId { get; }

    public string Summary { get; }

    public ApprovalTokenId ApprovalTokenId { get; }

    public string ActionHash { get; }

    public CapaStage Stage { get; private set; }

    public DateTimeOffset OpenedAt { get; }

    public static string ComputeActionHash(IncidentId incidentId, string summary)
    {
        if (incidentId.IsEmpty)
        {
            throw new DomainRuleException("Incident id is required.");
        }

        var normalized = RequiredText.Value(summary, "CAPA summary is required.");
        var payload = string.Concat(incidentId.Value.ToString("D", CultureInfo.InvariantCulture), "|", normalized);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public static CapaAction Open(
        CapaId id,
        IncidentId incidentId,
        string summary,
        ApprovalToken approvalToken,
        DateTimeOffset now,
        ReadOnlySpan<byte> signingKey)
    {
        ArgumentNullException.ThrowIfNull(approvalToken);
        if (id.IsEmpty)
        {
            throw new DomainRuleException("CAPA id is required.");
        }

        var normalized = RequiredText.Value(summary, "CAPA summary is required.");
        var actionHash = ComputeActionHash(incidentId, normalized);
        approvalToken.Consume(actionHash, now, signingKey);
        return new CapaAction(id, incidentId, normalized, approvalToken.Id, actionHash, CapaStage.Open, now);
    }

    public static CapaAction Rehydrate(
        CapaId id,
        IncidentId incidentId,
        string summary,
        ApprovalTokenId approvalTokenId,
        string actionHash,
        CapaStage stage,
        DateTimeOffset openedAt)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("CAPA id is required.");
        }

        if (incidentId.IsEmpty)
        {
            throw new DomainRuleException("Incident id is required.");
        }

        if (approvalTokenId.IsEmpty)
        {
            throw new DomainRuleException("A CAPA cannot exist without an approval token.");
        }

        var normalized = RequiredText.Value(summary, "CAPA summary is required.");
        var normalizedHash = RequiredText.Value(actionHash, "Action hash is required.");
        var expected = ComputeActionHash(incidentId, normalized);
        if (!string.Equals(expected, normalizedHash, StringComparison.Ordinal))
        {
            throw new DomainRuleException("CAPA action hash does not match the incident and summary.");
        }

        return new CapaAction(id, incidentId, normalized, approvalTokenId, normalizedHash, stage, openedAt);
    }

    public void Advance(CapaStage next)
    {
        if (!CapaLifecycle.CanTransition(Stage, next))
        {
            throw new DomainRuleException($"Cannot move a CAPA from {Stage} to {next}.");
        }

        Stage = next;
    }
}
