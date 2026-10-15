// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.AI;

using Mps.Agents.Reasoning;

namespace Mps.Agents.Tests;

public sealed class PrintoutMockReasonerTests
{
    [Fact]
    public void SampleScanReturnsTheSameReadingTwiceAndAnUnknownHashDoesNotCallTools()
    {
        var reasoner = new QualityMockReasoner();
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "data", "samples", "thermocouple-logger-e01.png"));
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var first = reasoner.Complete(Messages(hash), null);
        var second = reasoner.Complete(Messages(hash), null);
        var unknown = reasoner.Complete(Messages(new string('a', 64)), null);

        first.Text.Should().Contain("escTemperature");
        first.Text.Should().Contain("thermocoupleLogger");
        second.Text.Should().Be(first.Text);
        first.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Should().BeEmpty();
        unknown.Text.Should().Contain("unrecognized printout");
        unknown.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Should().BeEmpty();
        JsonDocument.Parse(first.Text!).RootElement.GetProperty("value").GetDouble().Should().Be(68d);
    }

    private static List<ChatMessage> Messages(string hash)
    {
        return
        [
            new(ChatRole.System, "You are a printout reader. Return JSON."),
            new(ChatRole.User, "file: thermocouple-logger-e01.png\nsha256: " + hash),
        ];
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("MPS.sln was not found above the test output directory.");
    }
}
