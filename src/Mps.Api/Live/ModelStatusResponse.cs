// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.ModelGateway.Routing;

namespace Mps.Api.Live;

/// <summary>
/// The model mode in use, for the console banner. <c>Label</c> is the text shown (Offline (saved answers), OpenAI,
/// Azure OpenAI, Placeholder). <c>FallingBack</c> is true while a live mode is answering from saved answers because
/// the model failed or the daily cap was reached; <c>Detail</c> says why in plain words. No key or endpoint is included.
/// </summary>
public sealed record ModelStatusResponse(
    string Mode,
    string Label,
    string? Model,
    bool Live,
    bool FallingBack,
    string? Detail,
    DateTimeOffset? LastFailureAt)
{
    public static ModelStatusResponse From(ModelModeSettings settings, ModelGatewayStatus? status)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var live = settings.Mode is ModelMode.Azure or ModelMode.OpenAI;
        var fallingBack = live && status is not null && (status.CircuitOpen || status.FellBack);
        string? detail = settings.Mode switch
        {
            ModelMode.Placeholder => "A fixed placeholder answers every investigation. Set Mps:Model:Mode to offline, openai or azure.",
            ModelMode.Offline => "No model is configured. The agents use saved answers, with no network call.",
            _ when fallingBack => (status!.LastFailure ?? "Live calls are paused.") + " Using saved answers until the model answers again.",
            _ => null,
        };
        return new ModelStatusResponse(settings.Name, settings.Label, settings.ModelName, live, fallingBack, detail, status?.LastFailureAt);
    }
}
