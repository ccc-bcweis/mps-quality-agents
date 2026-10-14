// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI.Workflows;

namespace Mps.Orchestration;

/// <summary>
/// Applies the human decision stashed on the session. It does not open a CAPA.
/// </summary>
[YieldsOutput(typeof(string))]
public sealed class ApprovalExecutor : Executor<string>
{
    public const string ExecutorId = "approval-step";

    private readonly MafInvestigationWorkflow _host;

    public ApprovalExecutor(MafInvestigationWorkflow host)
        : base(ExecutorId, ExecutorOptions.Default, declareCrossRunShareable: false)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        _host.ApplyHumanDecision(message);
        await context.YieldOutputAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
