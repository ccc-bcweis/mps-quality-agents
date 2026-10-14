// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Mps.Simulator.Fleet;

namespace Mps.Simulator.Telemetry;

public interface ISimTicker
{
    Task WaitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Waits one real second divided by the scale between simulated seconds. Scale 60 is the demo acceleration.
/// </summary>
public sealed class ScaledSimTicker : ISimTicker
{
    private readonly TimeSpan _delay;
    private readonly TimeProvider _time;

    public ScaledSimTicker(double scale, TimeProvider? timeProvider = null)
    {
        if (scale <= 0 || !double.IsFinite(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be a positive finite number.");
        }

        _delay = TimeSpan.FromSeconds(1d / scale);
        _time = timeProvider ?? TimeProvider.System;
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(_delay, _time, cancellationToken);
    }
}

/// <summary>
/// Background pump. Each tick appends one simulated second to <see cref="FleetSimulator.Frames"/>.
/// </summary>
public sealed class FleetSimulatorService : BackgroundService
{
    private readonly FleetSimulator _simulator;
    private readonly ISimTicker _ticker;

    public FleetSimulatorService(FleetSimulator simulator, ISimTicker ticker)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(ticker);
        _simulator = simulator;
        _ticker = ticker;
    }

    public Task PumpAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _simulator.Advance(TimeSpan.FromSeconds(1));
            try
            {
                await _ticker.WaitAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

public static class FleetSimulatorRegistration
{
    public static IServiceCollection AddMpsFleetSimulator(
        this IServiceCollection services,
        FleetCatalog fleet,
        int seed,
        DateTimeOffset origin,
        double scale = 60d)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(fleet);
        services.AddSingleton(fleet);
        services.AddSingleton(new FleetSimulator(fleet, seed, origin));
        services.AddSingleton<ISimTicker>(new ScaledSimTicker(scale));
        services.AddHostedService<FleetSimulatorService>();
        return services;
    }
}
