// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Nodes;

using Json.Schema;

using Mps.Domain.Events;

namespace Mps.Contract.Tests;

/// <summary>
/// Lifecycle events are JSON Schema draft 2020-12. The catalog and the rejection cases
/// come from the spec and ADR-001, including "approval events do not carry a signature"
/// and "an escalation names the gap and does not assert a root cause".
/// </summary>
public sealed class EventSchemaContractTests
{
    private static readonly EvaluationOptions Options = BuildOptions();
    private static readonly string[] EventTypes =
    [
        "alertRaised",
        "ticketOpened",
        "incidentOpened",
        "investigationStarted",
        "evidenceGathered",
        "hypothesisProposed",
        "investigationEscalated",
        "approvalGranted",
        "approvalRejected",
        "capaCreated",
        "capaAdvanced",
        "faultInjected",
    ];

    public static TheoryData<string> Events { get; } = new(EventTypes);

    [Fact]
    public void CatalogListsTheTwelveLifecycleEvents()
    {
        var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Events, "event.schema.json")))!;
        catalog["$schema"]!.GetValue<string>().Should().Be("https://json-schema.org/draft/2020-12/schema");
        var refs = catalog["oneOf"]!.AsArray()
            .Select(item => item!["$ref"]!.GetValue<string>())
            .ToArray();
        refs.Should().Equal(EventTypes.Select(type => FileName(type)));

        foreach (var type in EventTypes)
        {
            var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Events, FileName(type))))!;
            schema["$schema"]!.GetValue<string>().Should().Be("https://json-schema.org/draft/2020-12/schema");
            schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
            schema["properties"]!["eventType"]!["const"]!.GetValue<string>().Should().Be(type);
            schema["required"]!.AsArray().Select(item => item!.GetValue<string>()).Should().Equal(
                "eventId",
                "eventType",
                "occurredAt",
                "correlationId",
                "payload");
            PropertyNames(schema).Should().NotContain("signature");
        }
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void ValidSampleMatchesItsSchemaAndTheCatalog(string eventType)
    {
        var sample = EventSamples.Create(eventType);
        Evaluate(FileName(eventType), sample).Should().BeTrue();
        Evaluate("event.schema.json", sample).Should().BeTrue();
    }

    [Fact]
    public void UnknownEventTypeIsRejectedByTheCatalog()
    {
        var sample = EventSamples.Create("alertRaised");
        sample["eventType"] = "notAnEvent";
        Evaluate("event.schema.json", sample).Should().BeFalse();
    }

    [Fact]
    public void ApprovalEventsRejectASignatureAndEscalationDoesNotNameACategory()
    {
        var granted = EventSamples.Create("approvalGranted");
        granted["signature"] = "not-a-token-signature";
        Evaluate("approval-granted.schema.json", granted).Should().BeFalse();

        var rejected = EventSamples.Create("approvalRejected");
        rejected["payload"]!.AsObject()["signature"] = "not-a-token-signature";
        Evaluate("approval-rejected.schema.json", rejected).Should().BeFalse();

        var escalated = JsonNode.Parse(File.ReadAllText(
            Path.Combine(RepoPaths.Events, "investigation-escalated.schema.json")))!;
        var payload = escalated["properties"]!["payload"]!["properties"]!.AsObject();
        payload.Select(property => property.Key).Should().BeEquivalentTo(
            "investigationId",
            "incidentId",
            "reason");
        payload.Select(property => property.Key).Should().NotContain("category");
        payload.Select(property => property.Key).Should().NotContain("cause");

        var missingReason = EventSamples.Create("investigationEscalated");
        missingReason["payload"]!.AsObject().Remove("reason");
        Evaluate("investigation-escalated.schema.json", missingReason).Should().BeFalse();
    }

    [Fact]
    public void HypothesisRequiresEvidenceAndACapaStartsOpen()
    {
        var hypothesis = EventSamples.Create("hypothesisProposed");
        hypothesis["payload"]!["hypothesis"]!.AsObject()["evidenceIds"] = new JsonArray();
        Evaluate("hypothesis-proposed.schema.json", hypothesis).Should().BeFalse();

        var confidence = EventSamples.Create("hypothesisProposed");
        confidence["payload"]!["hypothesis"]!.AsObject()["confidence"] = 1.5;
        Evaluate("hypothesis-proposed.schema.json", confidence).Should().BeFalse();

        var created = EventSamples.Create("capaCreated");
        created["payload"]!.AsObject()["stage"] = "containment";
        Evaluate("capa-created.schema.json", created).Should().BeFalse();

        var createdWithoutToken = EventSamples.Create("capaCreated");
        createdWithoutToken["payload"]!.AsObject().Remove("approvalTokenId");
        Evaluate("capa-created.schema.json", createdWithoutToken).Should().BeFalse();

        var ticket = EventSamples.Create("ticketOpened");
        ticket["payload"]!.AsObject()["number"] = "INC-10";
        Evaluate("ticket-opened.schema.json", ticket).Should().BeFalse();
    }

    private static bool Evaluate(string fileName, JsonNode sample)
    {
        var schema = JsonSchema.FromFile(Path.Combine(RepoPaths.Events, fileName));
        var result = schema.Evaluate(sample, Options);
        return result.IsValid;
    }

    private static EvaluationOptions BuildOptions()
    {
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        };
        options.SchemaRegistry.Fetch = static uri =>
        {
            var path = Path.Combine(RepoPaths.Events, Path.GetFileName(uri.AbsolutePath));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Event schema " + uri + " is not in contracts/events. Ref resolution stays local.",
                    path);
            }

            return JsonSchema.FromFile(path);
        };

        foreach (var file in Directory.GetFiles(RepoPaths.Events, "*.schema.json"))
        {
            var id = JsonNode.Parse(File.ReadAllText(file))!["$id"]!.GetValue<string>();
            options.SchemaRegistry.Register(new Uri(id), JsonSchema.FromFile(file));
        }

        return options;
    }

    private static string FileName(string eventType)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in eventType)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                _ = builder.Append('-');
            }

            _ = builder.Append(char.ToLowerInvariant(character));
        }

        _ = builder.Append(".schema.json");
        return builder.ToString();
    }

    private static IEnumerable<string> PropertyNames(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                yield return property.Key;
                if (property.Value is not null)
                {
                    foreach (var nested in PropertyNames(property.Value))
                    {
                        yield return nested;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    foreach (var nested in PropertyNames(item))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }
}
