// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI.Workflows;

namespace Mps.Orchestration;

/// <summary>
/// Runs investigation, the evidence gate, resolution, and grounding. A recommendation is the only path to approval.
/// </summary>
[SendsMessage(typeof(string))]
[YieldsOutput(typeof(string))]
public sealed class InvestigationGateExecutor : Executor<string>
{
    public const string ExecutorId = "investigation-gate";

    private readonly MafInvestigationWorkflow _host;

    public InvestigationGateExecutor(MafInvestigationWorkflow host)
        : base(ExecutorId, ExecutorOptions.Default, declareCrossRunShareable: false)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var pause = await _host.RunToGateAsync(message, cancellationToken).ConfigureAwait(false);
        var investigationId = MafInvestigationWorkflow.InvestigationIdText(message);
        if (pause)
        {
            await context.SendMessageAsync(investigationId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.YieldOutputAsync(investigationId, cancellationToken).ConfigureAwait(false);
    }
}
