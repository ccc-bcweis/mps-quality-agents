// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Live;

/// <summary>Which model answers the agents.</summary>
public enum ModelMode
{
    /// <summary>No network. The agents run on the deterministic reasoner the replay eval scores (saved answers).</summary>
    Offline,

    /// <summary>The OpenAI API (or an OpenAI-compatible endpoint) with an API key.</summary>
    OpenAI,

    /// <summary>Azure OpenAI with managed identity, Azure login, or an optional key.</summary>
    Azure,

    /// <summary>Test-only stand-in that answers every investigation with one fixed placeholder. Never the default.</summary>
    Placeholder,
}

/// <summary>
/// One setting picks the model: <c>Mps:Model:Mode</c> (env <c>Mps__Model__Mode</c>, or <c>MODEL_MODE</c>) =
/// <c>offline</c> | <c>openai</c> | <c>azure</c>. Unset: <c>openai</c> when <c>OPENAI_API_KEY</c> is set, else <c>offline</c>.
/// Older spellings still work: <c>LLM_MODE=replay</c> or <c>mock</c> is offline, <c>live</c> is azure.
/// Every other value (model, endpoint, timeout, prices) comes from configuration; nothing here is a default model.
/// </summary>
public sealed record ModelModeSettings(
    ModelMode Mode,
    string? Endpoint,
    string? Deployment,
    string? ApiKey,
    string? Model,
    string? BaseUrl,
    string? ManagedIdentityClientId,
    TimeSpan Timeout)
{
    public const string ModeSetting = "Mps:Model:Mode";

    /// <summary>Human label the console banner shows.</summary>
    public string Label => Mode switch
    {
        ModelMode.OpenAI => "OpenAI",
        ModelMode.Azure => "Azure OpenAI",
        ModelMode.Placeholder => "Placeholder",
        _ => "Offline (saved answers)",
    };

    /// <summary>Wire name: offline, openai, azure or placeholder.</summary>
    public string Name => Mode.ToString().ToLowerInvariant();

    /// <summary>The model or deployment a live mode calls; null offline.</summary>
    public string? ModelName => Mode switch
    {
        ModelMode.OpenAI => Model,
        ModelMode.Azure => Deployment,
        _ => null,
    };

    /// <summary>How Azure OpenAI authenticates: key, or Azure identity (managed identity or az login).</summary>
    public string AzureAuth => string.IsNullOrWhiteSpace(ApiKey) ? "azure-identity" : "api-key";

    public static ModelModeSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var mode = ParseMode(First(configuration, ModeSetting, "MODEL_MODE", "LLM_MODE"), First(configuration, "Mps:Model:OpenAI:ApiKey", "OPENAI_API_KEY"));
        var timeoutSeconds = configuration.GetValue<double?>("Mps:Model:TimeoutSeconds")
            ?? throw new InvalidOperationException("Mps:Model:TimeoutSeconds is not set. appsettings.json ships the default.");
        var timeout = TimeSpan.FromSeconds(timeoutSeconds <= 0 ? 1 : timeoutSeconds);
        var settings = mode switch
        {
            ModelMode.OpenAI => new ModelModeSettings(
                mode,
                Endpoint: null,
                Deployment: null,
                ApiKey: First(configuration, "Mps:Model:OpenAI:ApiKey", "OPENAI_API_KEY"),
                Model: First(configuration, "OPENAI_MODEL", "Mps:Model:OpenAI:Model"),
                BaseUrl: First(configuration, "OPENAI_BASE_URL", "Mps:Model:OpenAI:BaseUrl"),
                ManagedIdentityClientId: null,
                timeout),
            ModelMode.Azure => new ModelModeSettings(
                mode,
                Endpoint: First(configuration, "Mps:Model:Endpoint", "AZURE_OPENAI_ENDPOINT"),
                Deployment: First(configuration, "Mps:Model:Deployment", "AZURE_OPENAI_DEPLOYMENT"),
                ApiKey: First(configuration, "Mps:Model:ApiKey", "AZURE_OPENAI_API_KEY"),
                Model: null,
                BaseUrl: null,
                ManagedIdentityClientId: First(configuration, "AZURE_CLIENT_ID"),
                timeout),
            _ => new ModelModeSettings(mode, null, null, null, null, null, null, timeout),
        };
        settings.Validate();
        return settings;
    }

    public static ModelMode ParseMode(string? configured, string? openAiKey)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return string.IsNullOrWhiteSpace(openAiKey) ? ModelMode.Offline : ModelMode.OpenAI;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            "offline" or "replay" or "mock" or "mockreasoner" or "saved" => ModelMode.Offline,
            "openai" => ModelMode.OpenAI,
            "azure" or "azureopenai" or "azure-openai" or "live" => ModelMode.Azure,
            "placeholder" => ModelMode.Placeholder,
            _ => throw new InvalidOperationException(
                $"{ModeSetting} '{configured.Trim()}' is not a model mode. Use offline, openai or azure."),
        };
    }

    private void Validate()
    {
        switch (Mode)
        {
            case ModelMode.OpenAI:
                Require(ApiKey, "OPENAI_API_KEY (or Mps:Model:OpenAI:ApiKey)");
                Require(Model, "OPENAI_MODEL (or Mps:Model:OpenAI:Model)");
                if (!string.IsNullOrWhiteSpace(BaseUrl) && !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
                {
                    throw new InvalidOperationException("OPENAI_BASE_URL must be an absolute URL.");
                }

                break;
            case ModelMode.Azure:
                Require(Endpoint, "AZURE_OPENAI_ENDPOINT (or Mps:Model:Endpoint)");
                Require(Deployment, "AZURE_OPENAI_DEPLOYMENT (or Mps:Model:Deployment)");
                if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
                {
                    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT must be an absolute URL.");
                }

                break;
        }
    }

    private void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Model mode '{Name}' needs {name}. Set it in .env or the environment, or set {ModeSetting}=offline to run with saved answers.");
        }
    }

    private static string? First(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
