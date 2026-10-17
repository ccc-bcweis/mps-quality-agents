// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Decisions;
using Mps.Application.Metadata;
using Mps.Application.Spc;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

/// <summary>The labels, limits and rule sentences the console shows, all read from data/ at startup. The console keeps no copy.</summary>
public sealed record ConsoleCatalogResponse(
    CatalogLimitsResponse Limits,
    IReadOnlyList<CatalogRuleResponse> Rules,
    IReadOnlyList<CatalogParameterResponse> Parameters,
    IReadOnlyList<CatalogSiteResponse> Sites);

/// <summary>Rule numbers from spc-rules.yaml and decision.yaml. ZoneSigmas are the chart zones, inner to outer (rules 3, 2 and 1).</summary>
public sealed record CatalogLimitsResponse(
    double ControlLimitSigma,
    double WatchSigma,
    double CriticalSigma,
    int LimitCheckReadings,
    int ClearReadings,
    double ClearSigma,
    int OverrideMinChars,
    int MaxEvidenceRounds,
    int MaxEvidenceChars,
    IReadOnlyList<double> ZoneSigmas);

/// <summary>One rule id with its sentences, built from the rule parameters. Label is the console sentence, Brief the short form.</summary>
public sealed record CatalogRuleResponse(string Id, string Label, string Plain, string Brief);

/// <summary>One telemetry parameter id and its display name, from parameters.yaml.</summary>
public sealed record CatalogParameterResponse(string Id, string Label);

/// <summary>One plant and the prefix its tool ids carry, from data/fleet/sites.yaml. An empty prefix is the reference plant.</summary>
public sealed record CatalogSiteResponse(string Id, string Name, string ToolPrefix);

public static class ConsoleCatalog
{
    public static ConsoleCatalogResponse Build(SpcRuleSettings spc, DecisionSettings decision, EvidenceRoundSettings rounds, SiteDirectory sites)
    {
        ArgumentNullException.ThrowIfNull(spc);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(rounds);
        ArgumentNullException.ThrowIfNull(sites);
        return new ConsoleCatalogResponse(
            new CatalogLimitsResponse(
                spc.ControlLimitSigma,
                spc.WatchSigma,
                spc.CriticalSigma,
                spc.LimitCheck.RecentReadings,
                spc.Clearing.Readings,
                spc.Clearing.Sigma,
                decision.OverrideMinChars,
                rounds.MaxRounds,
                Mps.Domain.Investigations.InvestigationRounds.MaxEvidenceLength,
                [spc.WesternElectric.Rule3.Sigma, spc.WesternElectric.Rule2.Sigma, spc.WesternElectric.Rule1.Sigma]),
            SpcRuleText.RuleIds.Select(id => new CatalogRuleResponse(id, SpcRuleText.Label(id, spc), SpcRuleText.Plain(id, spc), SpcRuleText.Brief(id, spc))).ToArray(),
            ParameterNames.All.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new CatalogParameterResponse(pair.Key, pair.Value)).ToArray(),
            sites.Sites.Select(site => new CatalogSiteResponse(site.Id, site.Name, site.Prefix)).ToArray());
    }
}
