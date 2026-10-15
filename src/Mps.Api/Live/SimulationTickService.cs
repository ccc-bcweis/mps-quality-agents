// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Live;

/// <summary>
/// Background tick. Each wait is <see cref="SimulationOptions.TickSeconds"/> of wall time and advances that many simulated minutes.
/// </summary>
public sealed partial class SimulationTickService : BackgroundService
{
    [LoggerMessage(EventId = 7111, Level = LogLevel.Warning, Message = "Plant simulation could not prime nominal history.")]
    private static partial void PrimeFailed(ILogger logger, Exception error);

    [LoggerMessage(EventId = 7112, Level = LogLevel.Warning, Message = "Plant simulation tick failed.")]
    private static partial void TickFailed(ILogger logger, Exception error);

    private readonly PlantSimulation _plant;
    private readonly StreamSpcPublisher _publisher;
    private readonly SimulationOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<SimulationTickService> _logger;

    public SimulationTickService(
        PlantSimulation plant,
        StreamSpcPublisher publisher,
        SimulationOptions options,
        TimeProvider time,
        ILogger<SimulationTickService> logger)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _plant = plant;
        _publisher = publisher;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            _plant.EnsurePrimed();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrimeFailed(_logger, ex);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _plant.AdvanceTick();
                await _publisher.PublishAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                TickFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.TickSeconds), _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
