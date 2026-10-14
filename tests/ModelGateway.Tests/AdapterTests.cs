// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.ModelGateway.Adapters;

namespace Mps.ModelGateway.Tests;

public sealed class AdapterTests
{
    [Fact]
    public void AzureAdapterRejectsAMissingKeyWithoutCallingTheNetwork()
    {
        var act = () => AzureOpenAiChatClient.Create(new Uri("https://mps-demo.openai.azure.com"), "gpt-4o-mini", " ");

        act.Should().Throw<ArgumentException>().WithMessage("*API key*");
    }

    [Fact]
    public void AzureAdapterBuildsAClientWhenTheKeyIsPresent()
    {
        using var client = AzureOpenAiChatClient.Create(new Uri("https://mps-demo.openai.azure.com"), "gpt-4o-mini", "test-key");

        client.Should().NotBeNull();
    }

    [Fact]
    public void OpenAiAdapterRejectsAMissingKeyWithoutCallingTheNetwork()
    {
        var act = () => OpenAiChatClient.Create("gpt-4o-mini", string.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*API key*");
    }

    [Fact]
    public void OpenAiAdapterBuildsAClientWhenTheKeyIsPresent()
    {
        using var client = OpenAiChatClient.Create("gpt-4o-mini", "test-key");

        client.Should().NotBeNull();
    }
}
