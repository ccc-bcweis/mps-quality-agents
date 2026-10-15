// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Scenarios;

/// <summary>
/// Presenter copy for one scripted scenario. The harness shows it when the operator picks that scenario.
/// </summary>
public sealed record ScenarioCard(
    string Id,
    string WhatsHappening,
    string WhatWeSimulate,
    string WhatAiShouldFind,
    string ExpectedCause);

/// <summary>Loads <c>data/scenarios/catalog.yaml</c>.</summary>
public sealed class ScenarioCardCatalog
{
    private readonly Dictionary<string, ScenarioCard> _byId;

    public ScenarioCardCatalog(IReadOnlyList<ScenarioCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Cards = cards.ToArray();
        _byId = new Dictionary<string, ScenarioCard>(Cards.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var card in Cards)
        {
            if (!_byId.TryAdd(card.Id, card))
            {
                throw new InvalidOperationException("Duplicate scenario card " + card.Id + ".");
            }
        }
    }

    public IReadOnlyList<ScenarioCard> Cards { get; }

    public bool TryGet(string id, out ScenarioCard card) => _byId.TryGetValue(id, out card!);

    public static ScenarioCardCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Scenario catalog was not found.", path);
        }

        var dto = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ScenarioCardFile>(File.ReadAllText(path));
        if (dto?.Scenarios is null || dto.Scenarios.Count == 0)
        {
            throw new InvalidDataException("Scenario catalog " + path + " has no scenarios.");
        }

        var cards = new List<ScenarioCard>();
        foreach (var row in dto.Scenarios)
        {
            cards.Add(new ScenarioCard(
                Required(row.Id, "id"),
                Required(row.WhatsHappening, "whatsHappening"),
                Required(row.WhatWeSimulate, "whatWeSimulate"),
                Required(row.WhatAiShouldFind, "whatAiShouldFind"),
                Required(row.ExpectedCause, "expectedCause")));
        }

        return new ScenarioCardCatalog(cards);
    }

    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Scenario card is missing " + name + ".");
        }

        return value.Trim();
    }

    private sealed class ScenarioCardFile
    {
        public List<ScenarioCardRow>? Scenarios { get; set; }
    }

    private sealed class ScenarioCardRow
    {
        public string? Id { get; set; }

        public string? WhatsHappening { get; set; }

        public string? WhatWeSimulate { get; set; }

        public string? WhatAiShouldFind { get; set; }

        public string? ExpectedCause { get; set; }
    }
}
