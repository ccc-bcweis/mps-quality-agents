// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Mps.Agents.Evaluation;
using Mps.Agents.Prompts;
using Mps.Agents.Resolution;
using Mps.Api.Live;
using Mps.Domain.Hypotheses;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>
/// On-demand eval against the real model. The replay eval scores the deterministic ranker; live SC-8 (Oct 18) showed the
/// model can still trip on a record the pack itself rules out. This sends every eval case that carries a "Ruled out:"
/// decoy through the real resolution prompt and checks the model picks the expected class and never cites the decoy.
/// Run it with MPS_LIVE_EVAL=1 plus the usual model settings (see docs/TESTING.md); without them it is skipped, so CI
/// never needs a key.
/// </summary>
public sealed class LiveModelEvalTests
{
    private const string RuledOut = "Ruled out:";

    [LiveModelFact]
    public async Task TheLiveModelPicksTheExpectedClassAndNeverCitesARuledOutDecoy()
    {
        var settings = ModelModeSettings.Read(LiveModelFactAttribute.Configuration());
        var chat = ModelClientFactory.Create(settings, NullLogger.Instance)!;
        var agent = new ResolutionAgent(chat, PromptCatalog.Load());
        var cases = EvalSuite.Load(Path.Combine(LiveModelFactAttribute.RepoRoot(), "eval", "cases"))
            .Where(item => item.Expected.Behavior == "recommend" && item.Expected.Category is not null && item.Request.Notes.Any(note => note.Title.StartsWith(RuledOut, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        cases.Should().Contain(item => item.Id == "SC8-01");

        var misses = new List<string>();
        foreach (var item in cases)
        {
            var decoys = item.Request.Notes.Where(note => note.Title.StartsWith(RuledOut, StringComparison.OrdinalIgnoreCase)).Select(note => note.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = await agent.ResolveAsync(item.Request, CancellationToken.None);
            if (result.Escalated || result.Claim is null)
            {
                misses.Add($"{item.Id}: escalated ({result.Reason})");
                continue;
            }

            var claim = result.Claim;
            var expected = Enum.Parse<RootCauseCategory>(item.Expected.Category!, ignoreCase: true);
            if (claim.Category != expected)
            {
                misses.Add($"{item.Id}: category {claim.Category}, expected {expected}");
            }

            if (claim.EvidenceIds.Any(id => decoys.Contains(id.ToString())))
            {
                misses.Add($"{item.Id}: cited a ruled-out record as support");
            }

            foreach (var forbidden in item.Expected.ForbiddenInCause.Where(term => claim.Cause?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            {
                misses.Add($"{item.Id}: cause names {forbidden}");
            }
        }

        misses.Should().BeEmpty("cases: " + string.Join(", ", cases.Select(item => item.Id)));
    }
}

/// <summary>Runs only when MPS_LIVE_EVAL=1 and a live model mode (openai or azure) is configured; skipped otherwise.</summary>
public sealed class LiveModelFactAttribute : FactAttribute
{
    public LiveModelFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("MPS_LIVE_EVAL"), "1", StringComparison.Ordinal))
        {
            Skip = "Live-model eval runs on demand: set MPS_LIVE_EVAL=1 and the model settings.";
            return;
        }

        var mode = ModelModeSettings.Read(Configuration()).Mode;
        if (mode is not (ModelMode.OpenAI or ModelMode.Azure))
        {
            Skip = "MPS_LIVE_EVAL=1 but no live model mode is configured (openai or azure).";
        }
    }

    /// <summary>The API's shipped appsettings (timeout) overlaid with the environment (mode, endpoint, key).</summary>
    public static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(RepoRoot(), "src", "Mps.Api", "appsettings.json"), optional: false)
        .AddEnvironmentVariables()
        .Build();

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
