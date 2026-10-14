// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Application.Decisions;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Application.Writes;
using Mps.Domain;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Tests.Decisions;

public sealed class ApprovalDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Key = ApprovalPolicy.DemoSigningKey();

    [Fact]
    public async Task ApproveWithoutAnApproverDoesNotWrite()
    {
        var harness = await HarnessAsync();

        var act = () => harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", null, CancellationToken.None);

        await act.Should().ThrowAsync<IntakeValidationException>().WithMessage("*Approver*");
        harness.Writer.Calls.Should().Be(0);
        (await harness.Capas.ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task RejectDoesNotCreateACapa()
    {
        var harness = await HarnessAsync();

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Reject, "Returned to the window.", null, CancellationToken.None);

        outcome.Capa.Should().BeNull();
        outcome.Investigation.Stage.Should().Be(InvestigationStage.Rejected);
        harness.Writer.Calls.Should().Be(0);
        harness.Incident.Stage.Should().Be(IncidentStage.Rejected);
    }

    [Fact]
    public async Task ApproveConsumesATokenAndCreatesOneCapa()
    {
        var harness = await HarnessAsync();

        var outcome = await harness.Service.DecideAsync(
            harness.InvestigationId,
            HumanDecisionKind.Approve,
            "Hold the lot.",
            "eng-17",
            CancellationToken.None);
        var again = await harness.Service.DecideAsync(
            harness.InvestigationId,
            HumanDecisionKind.Approve,
            "Hold the lot.",
            "eng-17",
            CancellationToken.None);

        outcome.Capa.Should().NotBeNull();
        outcome.Capa!.ExternalId.Should().Be("CAPA-900");
        outcome.Capa.Capa.Summary.Should().Be("Hold the lot.");
        outcome.Capa.Capa.Stage.Should().Be(CapaStage.Open);
        outcome.Investigation.Stage.Should().Be(InvestigationStage.Approved);
        harness.Incident.Stage.Should().Be(IncidentStage.Approved);
        harness.Writer.Calls.Should().Be(1);
        again.Capa!.Capa.Id.Should().Be(outcome.Capa.Capa.Id);
        var approval = harness.Approvals.Approvals.Single();
        approval.Outcome.Should().Be(ApprovalOutcomes.Succeeded);
        approval.Token.IsConsumed.Should().BeTrue();
        approval.Attempts.Should().Be(1);
        var reused = () => CapaAction.Open(
            CapaId.New(),
            harness.Incident.Id,
            "Hold the lot.",
            approval.Token,
            Now,
            Key);
        reused.Should().Throw<DomainRuleException>().WithMessage("*single-use*");
    }

    [Fact]
    public async Task AnExpiredTokenIsRejectedBeforeTheWrite()
    {
        var clock = new StepClock(Now, Now.AddHours(1));
        var harness = await HarnessAsync(clock);

        var act = () => harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        await act.Should().ThrowAsync<ApprovalForbiddenException>().WithMessage("*expired*");
        harness.Writer.Calls.Should().Be(0);
        harness.Approvals.Approvals.Should().BeEmpty();
    }

    [Fact]
    public async Task ATamperedSignatureCannotResumeIntoACapa()
    {
        var harness = await HarnessAsync();
        var token = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", CapaAction.ComputeActionHash(harness.Incident.Id, "Hold the lot."), Now, TimeSpan.FromMinutes(15), Key);
        var capa = CapaAction.Open(CapaId.New(), harness.Incident.Id, "Hold the lot.", token, Now, Key);
        var tampered = ApprovalToken.Rehydrate(token.Id, token.ApproverId, token.ActionHash, token.IssuedAt, token.ExpiresAt, new string('a', 64), consumed: true);
        await harness.Approvals.AddAsync(
            new StoredApproval(tampered, harness.InvestigationId, harness.Incident.Id, capa.Summary, capa.Id, "approve", ApprovalOutcomes.Writing, "Hold the lot.", null, 0, null),
            CancellationToken.None);

        var act = () => harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        await act.Should().ThrowAsync<ApprovalForbiddenException>().WithMessage("*signature*");
        harness.Writer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AHashMismatchIsRejected()
    {
        var harness = await HarnessAsync();
        var token = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", CapaAction.ComputeActionHash(harness.Incident.Id, "Hold the lot."), Now, TimeSpan.FromMinutes(15), Key);
        _ = CapaAction.Open(CapaId.New(), harness.Incident.Id, "Hold the lot.", token, Now, Key);
        await harness.Approvals.AddAsync(
            new StoredApproval(token, harness.InvestigationId, harness.Incident.Id, "Scrap the lot.", CapaId.New(), "approve", ApprovalOutcomes.Writing, null, null, 0, null),
            CancellationToken.None);

        var act = () => harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Scrap the lot.", "eng-17", CancellationToken.None);

        await act.Should().ThrowAsync<ApprovalForbiddenException>().WithMessage("*action*");
        harness.Writer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AServerErrorThenSuccessRetriesAndKeepsOneCapa()
    {
        var writer = new ScriptedCapaWriter();
        writer.ThenThrow(500);
        writer.ThenReturn("CAPA-901");
        var harness = await HarnessAsync(writer: writer);

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        writer.Calls.Should().Be(2);
        outcome.Capa!.ExternalId.Should().Be("CAPA-901");
        harness.Approvals.Approvals.Single().Attempts.Should().Be(2);
        harness.Approvals.Approvals.Single().Outcome.Should().Be(ApprovalOutcomes.Succeeded);
    }

    [Fact]
    public async Task ATimeoutThenSuccessRetries()
    {
        var writer = new ScriptedCapaWriter();
        writer.ThenThrow(408);
        writer.ThenReturn("CAPA-902");
        var harness = await HarnessAsync(writer: writer);

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        writer.Calls.Should().Be(2);
        outcome.Capa!.ExternalId.Should().Be("CAPA-902");
    }

    [Fact]
    public async Task RepeatedServerErrorsFailTheInvestigationWithoutACapa()
    {
        var writer = new ScriptedCapaWriter();
        writer.ThenThrow(500, message: "QMS unavailable.");
        writer.ThenThrow(500, message: "QMS unavailable.");
        writer.ThenThrow(500, message: "QMS unavailable.");
        var harness = await HarnessAsync(writer: writer);

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);
        var again = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        writer.Calls.Should().Be(3);
        outcome.Capa.Should().BeNull();
        outcome.Investigation.Stage.Should().Be(InvestigationStage.Failed);
        harness.Incident.Stage.Should().Be(IncidentStage.RecommendationReady);
        harness.Approvals.Approvals.Single().Outcome.Should().Be(ApprovalOutcomes.Failed);
        harness.Approvals.Approvals.Single().Attempts.Should().Be(3);
        harness.Approvals.Approvals.Single().FailureDetail.Should().Be("QMS unavailable.");
        harness.Metrics.Observations.Sum(item => item.ToolFailures).Should().Be(3);
        again.Investigation.Stage.Should().Be(InvestigationStage.Failed);
        again.Capa.Should().BeNull();
    }

    [Fact]
    public async Task AConflictWithAnExistingIdIsIdempotentSuccess()
    {
        var writer = new ScriptedCapaWriter();
        writer.ThenThrow(409, "CAPA-existing");
        var harness = await HarnessAsync(writer: writer);

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        writer.Calls.Should().Be(1);
        outcome.Capa!.ExternalId.Should().Be("CAPA-existing");
        outcome.Investigation.Stage.Should().Be(InvestigationStage.Approved);
        harness.Approvals.Approvals.Single().Attempts.Should().Be(1);
    }

    [Fact]
    public async Task AClientWriteErrorIsNotRetried()
    {
        var writer = new ScriptedCapaWriter();
        writer.ThenThrow(400, message: "QMS rejected the body.");
        var harness = await HarnessAsync(writer: writer);

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, "Hold the lot.", "eng-17", CancellationToken.None);

        writer.Calls.Should().Be(1);
        outcome.Capa.Should().BeNull();
        outcome.Investigation.Stage.Should().Be(InvestigationStage.Failed);
    }

    [Fact]
    public async Task AnInterruptedWriteResumesWithTheSameCapa()
    {
        var harness = await HarnessAsync();
        var summary = "Hold the lot.";
        var token = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", CapaAction.ComputeActionHash(harness.Incident.Id, summary), Now, TimeSpan.FromMinutes(15), Key);
        var capa = CapaAction.Open(CapaId.New(), harness.Incident.Id, summary, token, Now, Key);
        await harness.Approvals.AddAsync(
            new StoredApproval(token, harness.InvestigationId, harness.Incident.Id, summary, capa.Id, "approve", ApprovalOutcomes.Writing, summary, null, 1, null),
            CancellationToken.None);
        harness.Writer.ThenReturn("CAPA-903");

        var outcome = await harness.Service.DecideAsync(harness.InvestigationId, HumanDecisionKind.Approve, summary, "eng-17", CancellationToken.None);

        harness.Writer.Calls.Should().Be(1);
        outcome.Capa!.Capa.Id.Should().Be(capa.Id);
        outcome.Capa.ExternalId.Should().Be("CAPA-903");
        harness.Approvals.Approvals.Single().Attempts.Should().Be(2);
    }

    private static async Task<Harness> HarnessAsync(TimeProvider? time = null, ScriptedCapaWriter? writer = null)
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var capas = new InMemoryCapaLog();
        var approvals = new InMemoryApprovalLog();
        var metrics = new InMemoryMetricsLog();
        var clock = time ?? new FixedClock(Now);
        var qms = writer ?? new ScriptedCapaWriter();
        var incident = IncidentRestorer.Restore(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            Severity.Medium,
            "ESC temperature drifted.",
            "corr-approve",
            Now,
            IncidentStage.RecommendationReady);
        await incidents.AddAsync(new StoredIncident(incident, false, false), CancellationToken.None);
        var evidenceId = EvidenceId.New();
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            incident.Id,
            InvestigationStage.RecommendationReady,
            [
                new EvidenceItem(evidenceId, EvidenceSource.Spc, "SPC chart", "Point outside the window.", Now, "E-02", null),
            ],
            [
                new Hypothesis(
                    HypothesisId.New(),
                    RootCauseCategory.ProcessDrift,
                    "Chamber drift.",
                    0.5d,
                    [evidenceId],
                    [],
                    ["Hold the affected lot."]),
            ],
            null,
            "corr-approve");
        await investigations.AddAsync(snapshot, CancellationToken.None);
        var service = new HumanDecisionService(
            investigations,
            incidents,
            capas,
            approvals,
            qms,
            NullQualityNotifier.Instance,
            clock,
            new ApprovalPolicy(Key, TimeSpan.FromMinutes(15)),
            metrics);
        return new Harness(service, qms, approvals, capas, metrics, incident, snapshot.Id);
    }

    private sealed record Harness(
        HumanDecisionService Service,
        ScriptedCapaWriter Writer,
        InMemoryApprovalLog Approvals,
        InMemoryCapaLog Capas,
        InMemoryMetricsLog Metrics,
        Incident Incident,
        InvestigationId InvestigationId);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class StepClock : TimeProvider
    {
        private readonly DateTimeOffset[] _steps;
        private int _index;

        public StepClock(params DateTimeOffset[] steps) => _steps = steps;

        public override DateTimeOffset GetUtcNow()
        {
            var value = _steps[Math.Min(_index, _steps.Length - 1)];
            if (_index < _steps.Length - 1)
            {
                _index++;
            }

            return value;
        }
    }

    private sealed class ScriptedCapaWriter : IQmsCapaWriter
    {
        private readonly Queue<Func<CapaAction, string>> _steps = new();

        public int Calls { get; private set; }

        public void ThenReturn(string externalId) => _steps.Enqueue(_ => externalId);

        public void ThenThrow(int statusCode, string? existingExternalId = null, string? message = null) =>
            _steps.Enqueue(_ => throw new ConnectorWriteException(
                statusCode,
                existingExternalId,
                message ?? "QMS write failed with status " + statusCode.ToString(CultureInfo.InvariantCulture) + "."));

        public Task<string> CreateAsync(CapaAction capa, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(capa);
            _ = cancellationToken;
            Calls++;
            if (_steps.Count == 0)
            {
                return Task.FromResult("CAPA-900");
            }

            return Task.FromResult(_steps.Dequeue().Invoke(capa));
        }
    }

    private sealed class InMemoryMetricsLog : IMetricsLog
    {
        public List<InvestigationObservation> Observations { get; } = [];

        public Task RecordAsync(InvestigationObservation observation, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(observation);
            _ = cancellationToken;
            Observations.Add(observation);
            return Task.CompletedTask;
        }

        public Task<MetricsRollup> RollupAsync(DateTimeOffset now, double spendCapUsd, string mode, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult(MetricsCalculator.Compute(Observations, now, spendCapUsd, mode));
        }
    }
}
