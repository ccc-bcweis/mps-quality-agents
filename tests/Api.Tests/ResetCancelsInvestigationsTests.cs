// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Mps.Agents.Investigation;
using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Fakes;
using Mps.Api.Live;
using Mps.Application;
using Mps.Application.Lifecycle;
using Mps.Application.Workflow;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Api.Tests;

/// <summary>
/// Regression (live, Oct 17 10:15 PM ET): a hard reset hung while an investigation was waiting for more readings to
/// classify the signal shape. The reset held the plant gate, so the plant clock stopped and no reading arrived, while
/// the reset waited for that investigation to finish: a deadlock. A reset now cancels in-flight investigations and
/// their waits, and returns within seconds.
/// Runs alone: the reset gate is process-wide, so a reset in a parallel test would add its own time to this one.
/// </summary>
[Collection(nameof(ResetCancelsInvestigationsTests))]
public sealed class ResetCancelsInvestigationsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AResetCancelsAnInvestigationWaitingForReadingsAndReturnsWithinSeconds(bool hard)
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Simulation:RealTime", "false");
            builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
            // Far past the 20 s the test allows, so a pass comes from cancelling the wait, not from giving up on it.
            builder.UseSetting("Mps:Demo:ResetDrainSeconds", "60");
            builder.ConfigureTestServices(services => services.AddScoped<IInvestigationWorkflow>(provider => new WaitsForReadingsWorkflow(
                ActivatorUtilities.CreateInstance<FakeInvestigationWorkflow>(provider, (TimeProvider)provider.GetRequiredService<RecordClock>()))));
        });
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<SpcRuleSubscriber>();
        var inFlight = factory.Services.GetRequiredService<InvestigationsInFlight>();

        // SC-3 steps the gas flow at once: the first readings break a rule and open a ticket, but far fewer readings
        // have arrived since the break than the shape wait needs, so the investigation waits for more.
        (await client.PostAsync("/api/injector/scenarios/SC-3", null)).EnsureSuccessStatusCode();
        plant.Advance(TimeSpan.FromSeconds(60));
        await telemetry.PublishPendingAsync(CancellationToken.None);
        var waiting = DateTime.UtcNow.AddSeconds(10);
        while (inFlight.Running.Count == 0 && DateTime.UtcNow < waiting)
        {
            await Task.Delay(50);
        }

        inFlight.Running.Should().NotBeEmpty("the investigation must be waiting for readings when the reset starts");

        await using var scope = factory.Services.CreateAsyncScope();
        var reset = scope.ServiceProvider.GetRequiredService<DemoResetService>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var finished = reset.ResetAsync(hard, CancellationToken.None);
        (await Task.WhenAny(finished, Task.Delay(TimeSpan.FromSeconds(20)))).Should().BeSameAs(finished, "a reset must not wait on an investigation that waits on the stopped plant");
        await finished;
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        inFlight.Running.Should().BeEmpty();
        factory.Services.GetRequiredService<ResetProgress>().Current.Should().Match<ResetProgressSnapshot>(state => !state.Running && state.Phase == "done");
        if (!hard)
        {
            return;
        }

        var investigations = await client.GetStringAsync("/api/investigations");
        investigations.Trim().Should().Be("[]", "the cancelled investigation must not write into the fresh plant");
    }

    /// <summary>
    /// Stands in for the live investigation agent while it waits for readings: it sleeps out the whole configured shape
    /// wait (data/metadata/investigation.yaml, ten minutes of wall time) and only then writes its result, as the agent
    /// does when the readings never come because the reset has stopped the plant.
    /// </summary>
    private sealed class WaitsForReadingsWorkflow(IInvestigationWorkflow inner) : IInvestigationWorkflow
    {
        private static readonly ShapeWait Wait = ShapeWait.Load();

        public async Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
        {
            await Task.Delay(Wait.Delay * Wait.Attempts, TimeProvider.System, cancellationToken);
            return await inner.StartAsync(incidentId, cancellationToken);
        }

        public Task<InvestigationSnapshot> StartRoundAsync(IncidentId incidentId, IReadOnlyList<EvidenceItem> userEvidence, CancellationToken cancellationToken) =>
            inner.StartRoundAsync(incidentId, userEvidence, cancellationToken);

        public Task<InvestigationSnapshot> ResumeWithHumanInputAsync(InvestigationId investigationId, HumanDecision decision, CancellationToken cancellationToken) =>
            inner.ResumeWithHumanInputAsync(investigationId, decision, cancellationToken);

        public Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken) =>
            inner.GetStateAsync(investigationId, cancellationToken);
    }
}

[CollectionDefinition(nameof(ResetCancelsInvestigationsTests), DisableParallelization = true)]
public sealed class ResetCancelsInvestigationsRunAlone;
