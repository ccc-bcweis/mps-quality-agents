// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Nodes;

namespace Mps.Contract.Tests;

internal static class EventSamples
{
    private const string IncidentId = "8d0a1c4e-1b7a-4e5a-9c1d-0a6b2e3f4a5b";

    private const string InvestigationId = "2c6e1a40-7b2d-4f1e-8a33-91d0c5b6e7f8";

    private const string EvidenceId = "11111111-1111-4111-8111-111111111111";

    private const string When = "2026-10-14T15:00:00Z";

    public static JsonObject Create(string eventType)
    {
        return new JsonObject
        {
            ["eventId"] = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            ["eventType"] = eventType,
            ["occurredAt"] = When,
            ["correlationId"] = "corr-contract",
            ["payload"] = Payload(eventType),
        };
    }

    private static JsonObject Payload(string eventType)
    {
        return eventType switch
        {
            "alertRaised" => new JsonObject
            {
                ["id"] = "33333333-3333-4333-8333-333333333333",
                ["toolId"] = "E-02",
                ["chamberId"] = "C1",
                ["parameter"] = "escTemperature",
                ["severity"] = "high",
                ["rule"] = "westernElectric1",
                ["raisedAt"] = "2026-10-14T14:59:00Z",
            },
            "ticketOpened" => new JsonObject
            {
                ["id"] = "44444444-4444-4444-8444-444444444444",
                ["number"] = "TKT-1001",
                ["incidentId"] = IncidentId,
                ["toolId"] = "E-02",
                ["title"] = "ESC temperature oscillation",
                ["openedAt"] = When,
            },
            "incidentOpened" => new JsonObject
            {
                ["id"] = IncidentId,
                ["toolId"] = "E-02",
                ["chamberId"] = "C1",
                ["parameter"] = "escTemperature",
                ["channel"] = "iotSpcAlert",
                ["severity"] = "high",
                ["summary"] = "ESC temperature oscillation after a recipe revision.",
                ["stage"] = "ticketOpened",
                ["openedAt"] = When,
            },
            "investigationStarted" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["incidentId"] = IncidentId,
                ["stage"] = "started",
            },
            "evidenceGathered" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["incidentId"] = IncidentId,
                ["evidence"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = EvidenceId,
                        ["source"] = "plm",
                        ["title"] = "ECO-4471 edge ring rev C",
                        ["excerpt"] = "Edge ring revision C became effective before the oscillation.",
                        ["observedAt"] = "2026-10-12T08:00:00Z",
                        ["toolId"] = "E-02",
                        ["externalRef"] = "ECO-4471",
                    },
                },
            },
            "hypothesisProposed" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["hypothesis"] = new JsonObject
                {
                    ["id"] = "22222222-2222-4222-8222-222222222222",
                    ["category"] = "engineeringChange",
                    ["cause"] = "Edge ring revision C changed the thermal path.",
                    ["confidence"] = 0.74,
                    ["evidenceIds"] = new JsonArray(EvidenceId),
                    ["counterEvidence"] = new JsonArray("A preventive maintenance was performed the same week."),
                    ["actions"] = new JsonArray("Hold the affected lots and confirm the installed ring revision."),
                    ["ruledOut"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = "33333333-3333-4333-8333-333333333333",
                            ["reason"] = "The notice matches the purchase specification.",
                            ["source"] = "record",
                        }),
                },
            },
            "investigationEscalated" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["incidentId"] = IncidentId,
                ["reason"] = "PLM showerhead rev C contradicts the MES run header rev B.",
                ["evidenceIds"] = new JsonArray(EvidenceId),
                ["ruledOut"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "33333333-3333-4333-8333-333333333333",
                        ["reason"] = "The notice matches the purchase specification.",
                        ["source"] = "record",
                    }),
            },
            "approvalGranted" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["approverId"] = "eng-17",
                ["approvalTokenId"] = "55555555-5555-4555-8555-555555555555",
                ["actionHash"] = "abc123",
                ["capaId"] = "66666666-6666-4666-8666-666666666666",
            },
            "approvalRejected" => new JsonObject
            {
                ["investigationId"] = InvestigationId,
                ["comment"] = "Need the calibration record.",
            },
            "capaCreated" => new JsonObject
            {
                ["id"] = "66666666-6666-4666-8666-666666666666",
                ["incidentId"] = IncidentId,
                ["summary"] = "Hold lot NF-3317 and replace the edge ring.",
                ["approvalTokenId"] = "55555555-5555-4555-8555-555555555555",
                ["actionHash"] = "abc123",
                ["stage"] = "open",
                ["openedAt"] = When,
            },
            "capaAdvanced" => new JsonObject
            {
                ["capaId"] = "66666666-6666-4666-8666-666666666666",
                ["fromStage"] = "open",
                ["toStage"] = "containment",
            },
            "faultInjected" => new JsonObject
            {
                ["faultId"] = "77777777-7777-4777-8777-777777777777",
                ["toolId"] = "E-02",
                ["chamberId"] = null,
                ["parameter"] = "escTemperature",
                ["shape"] = "oscillation",
                ["magnitude"] = 3.5,
                ["rootCauseCategory"] = "engineeringChange",
                ["confounders"] = true,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "No sample for this event."),
        };
    }
}
