// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Mps.Api.Hosting;
using Mps.Api.Live;
using Mps.ModelGateway.Routing;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>One setting picks the model; everything else comes from configuration.</summary>
public sealed class ModelModeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(null, null, ModelMode.Offline)]
    [InlineData("", null, ModelMode.Offline)]
    [InlineData(null, "sk-test", ModelMode.OpenAI)]
    [InlineData("offline", "sk-test", ModelMode.Offline)]
    [InlineData("replay", null, ModelMode.Offline)]
    [InlineData("openai", "sk-test", ModelMode.OpenAI)]
    [InlineData("azure", null, ModelMode.Azure)]
    [InlineData("Live", null, ModelMode.Azure)]
    [InlineData("placeholder", null, ModelMode.Placeholder)]
    public void TheModeComesFromOneSettingOrTheKey(string? configured, string? key, ModelMode expected)
    {
        ModelModeSettings.ParseMode(configured, key).Should().Be(expected);
    }

    [Fact]
    public void AnUnknownModeIsRejected()
    {
        var act = () => ModelModeSettings.ParseMode("gpt", null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*offline, openai or azure*");
    }

    [Fact]
    public void OpenAiReadsTheKeyAndTheModelFromConfiguration()
    {
        var settings = ModelModeSettings.Read(Config(("OPENAI_API_KEY", "sk-test"), ("Mps:Model:OpenAI:Model", "model-from-config")));

        settings.Mode.Should().Be(ModelMode.OpenAI);
        settings.Model.Should().Be("model-from-config");
        settings.Label.Should().Be("OpenAI");
        settings.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OpenAiModelEnvironmentVariableWinsOverTheConfigDefault()
    {
        var settings = ModelModeSettings.Read(Config(("OPENAI_API_KEY", "sk-test"), ("Mps:Model:OpenAI:Model", "default"), ("OPENAI_MODEL", "chosen")));

        settings.Model.Should().Be("chosen");
    }

    [Fact]
    public void OpenAiWithoutAKeySaysWhatToSet()
    {
        var act = () => ModelModeSettings.Read(Config(("Mps:Model:Mode", "openai"), ("Mps:Model:OpenAI:Model", "m")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*OPENAI_API_KEY*offline*");
    }

    [Fact]
    public void AzureNeedsAnEndpointAndADeployment()
    {
        var act = () => ModelModeSettings.Read(Config(("Mps:Model:Mode", "azure")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*AZURE_OPENAI_ENDPOINT*");
    }

    [Fact]
    public void AzureUsesAzureIdentityUnlessAKeyIsSet()
    {
        var identity = ModelModeSettings.Read(Config(("Mps:Model:Mode", "azure"), ("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com/"), ("AZURE_OPENAI_DEPLOYMENT", "dep")));
        var keyed = ModelModeSettings.Read(Config(("Mps:Model:Mode", "azure"), ("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com/"), ("AZURE_OPENAI_DEPLOYMENT", "dep"), ("AZURE_OPENAI_API_KEY", "k")));

        identity.AzureAuth.Should().Be("azure-identity");
        keyed.AzureAuth.Should().Be("api-key");
        identity.Label.Should().Be("Azure OpenAI");
        identity.ModelName.Should().Be("dep");
    }

    [Fact]
    public void LiveClientsHaveATimeoutAndNoSdkRetries()
    {
        var settings = ModelModeSettings.Read(Config(("OPENAI_API_KEY", "sk-test"), ("Mps:Model:OpenAI:Model", "m")));

        var options = ModelClientFactory.Options(settings, null);

        options.NetworkTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.RetryPolicy.Should().NotBeNull();
        ModelClientFactory.Create(settings, NullLogger.Instance).Should().NotBeNull();
        ModelClientFactory.Create(ModelModeSettings.Read(Config()), NullLogger.Instance).Should().BeNull("offline never builds a network client");
    }

    [Fact]
    public async Task OpenAiModeComposesTheGatewayOnTheOpenAiRoute()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Model:Mode", "openai");
            builder.UseSetting("OPENAI_API_KEY", "sk-test-not-real");
        });

        var gateway = factory.Services.GetRequiredService<Mps.ModelGateway.Routing.ModelGateway>();
        var status = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/model/status", Json);

        gateway.Status.Mode.Should().Be(ModelRoute.OpenAI);
        status.GetProperty("mode").GetString().Should().Be("openai");
        status.GetProperty("label").GetString().Should().Be("OpenAI");
        status.GetProperty("live").GetBoolean().Should().BeTrue();
        status.GetRawText().Should().NotContain("sk-test", "the status never carries the key");
    }

    [Fact]
    public async Task AzureModeComposesTheGatewayOnTheAzureRoute()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Model:Mode", "azure");
            builder.UseSetting("Mps:Model:Endpoint", "https://example.openai.azure.com/");
            builder.UseSetting("Mps:Model:Deployment", "dep");
        });

        var status = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/model/status", Json);

        factory.Services.GetRequiredService<Mps.ModelGateway.Routing.ModelGateway>().Status.Mode.Should().Be(ModelRoute.AzureOpenAI);
        status.GetProperty("label").GetString().Should().Be("Azure OpenAI");
        status.GetProperty("model").GetString().Should().Be("dep");
    }

    [Fact]
    public async Task OfflineModeBannerSaysSavedAnswers()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Model:Mode", "offline"));

        var status = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/model/status", Json);

        status.GetProperty("mode").GetString().Should().Be("offline");
        status.GetProperty("label").GetString().Should().Be("Offline (saved answers)");
        status.GetProperty("live").GetBoolean().Should().BeFalse();
        factory.Services.GetRequiredService<Mps.ModelGateway.Routing.ModelGateway>().Status.Mode.Should().Be(ModelRoute.MockReasoner);
    }

    [Fact]
    public void BannerTextForEachMode()
    {
        var timeout = TimeSpan.FromSeconds(5);
        ModelStatusResponse.From(new ModelModeSettings(ModelMode.Offline, null, null, null, null, null, null, timeout), null).Label.Should().Be("Offline (saved answers)");
        ModelStatusResponse.From(new ModelModeSettings(ModelMode.OpenAI, null, null, "k", "m", null, null, timeout), null).Label.Should().Be("OpenAI");
        ModelStatusResponse.From(new ModelModeSettings(ModelMode.Azure, "https://x", "d", null, null, null, null, timeout), null).Label.Should().Be("Azure OpenAI");
        ModelStatusResponse.From(new ModelModeSettings(ModelMode.Placeholder, null, null, null, null, null, null, timeout), null).Label.Should().Be("Placeholder");

        var failing = new ModelGatewayStatus(ModelRoute.AzureOpenAI, "mock", true, 2, true, DateTimeOffset.UnixEpoch, "The model service could not be reached.");
        var status = ModelStatusResponse.From(new ModelModeSettings(ModelMode.Azure, "https://x", "d", null, null, null, null, timeout), failing);
        status.FallingBack.Should().BeTrue();
        status.Detail.Should().Be("The model service could not be reached. Using saved answers until the model answers again.");
    }

    [Fact]
    public void DotEnvLinesParseWithoutOverridingBlanks()
    {
        var values = DotEnvFile.Parse([
            "# comment",
            "MODEL_MODE=openai",
            "export OPENAI_MODEL=\"m-1\"",
            "OPENAI_API_KEY=",
            "  AZURE_OPENAI_DEPLOYMENT = 'dep' ",
            "not a line",
        ]);

        values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["MODEL_MODE"] = "openai",
            ["OPENAI_MODEL"] = "m-1",
            ["AZURE_OPENAI_DEPLOYMENT"] = "dep",
        });
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mps:Model:TimeoutSeconds"] = "5" })
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}
