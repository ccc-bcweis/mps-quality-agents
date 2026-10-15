// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Mps.Contract.Tests;

/// <summary>
/// The console HTTP contract, read from contracts/openapi.yaml and checked against the spec.
/// These assertions name the routes, enums, and approval rules the spec requires.
/// </summary>
public sealed class ConsoleApiContractTests
{
    private static readonly string[] LifecyclePaths =
    [
        "/health",
        "/api/sites",
        "/api/sites/compare",
        "/api/sites/{siteId}",
        "/api/tools",
        "/api/tools/{toolId}",
        "/api/alerts",
        "/api/tickets",
        "/api/incidents",
        "/api/incidents/{incidentId}",
        "/api/incidents/{incidentId}/investigation",
        "/api/incidents/{incidentId}/investigations",
        "/api/investigations",
        "/api/investigations/{investigationId}",
        "/api/investigations/{investigationId}/decisions",
        "/api/capas",
        "/api/capas/{capaId}",
        "/api/capas/{capaId}/advance",
        "/api/faults",
        "/api/admin/session",
        "/api/admin/reset",
        "/api/injector/scenarios",
        "/api/injector/scenarios/{scenarioId}",
        "/api/metrics",
    ];

    [Fact]
    public void DocumentIsTheLocalMitConsoleContract()
    {
        var yaml = File.ReadAllText(Path.Combine(RepoPaths.Contracts, "openapi.yaml"));
        yaml.Should().Contain("openapi: 3.0.3");

        var document = OpenApiDocs.Console;
        document.Info.Title.Should().Contain("Meridian Precision Systems");
        document.Info.License.Name.Should().Be("MIT");
        document.Servers.Should().ContainSingle();
        document.Servers[0].Url.Should().Be("http://localhost:8080");
        document.Info.Description.Should().Contain("/hubs/fleet");
        document.Info.Description.Should().Contain("server issues the approval token");
        yaml.Should().NotContain("AURA");
    }

    [Fact]
    public void LifecycleRoutesMatchTheSpec()
    {
        OpenApiDocs.Console.Paths.Keys.Should().BeEquivalentTo(LifecyclePaths);

        Expect(OperationType.Get, "/health", "getHealth", "200");
        Expect(OperationType.Get, "/api/sites", "listSites", "200");
        Expect(OperationType.Get, "/api/sites/compare", "compareSites", "200");
        Expect(OperationType.Get, "/api/sites/{siteId}", "getSite", "200");
        Expect(OperationType.Get, "/api/tools", "listTools", "200");
        Expect(OperationType.Get, "/api/tools/{toolId}", "getTool", "200");
        Expect(OperationType.Get, "/api/alerts", "listAlerts", "200");
        Expect(OperationType.Get, "/api/tickets", "listTickets", "200");
        Expect(OperationType.Get, "/api/incidents", "listIncidents", "200");
        Expect(OperationType.Post, "/api/incidents", "openIncident", "201");
        Expect(OperationType.Get, "/api/incidents/{incidentId}", "getIncident", "200");
        Expect(OperationType.Get, "/api/incidents/{incidentId}/investigation", "getLatestInvestigation", "200");
        Expect(OperationType.Post, "/api/incidents/{incidentId}/investigations", "startInvestigation", "201");
        Expect(OperationType.Get, "/api/investigations", "listInvestigations", "200");
        Expect(OperationType.Get, "/api/investigations/{investigationId}", "getInvestigation", "200");
        Expect(OperationType.Post, "/api/investigations/{investigationId}/decisions", "submitDecision", "200");
        Expect(OperationType.Get, "/api/capas", "listCapas", "200");
        Expect(OperationType.Get, "/api/capas/{capaId}", "getCapa", "200");
        Expect(OperationType.Post, "/api/capas/{capaId}/advance", "advanceCapa", "200");
        Expect(OperationType.Post, "/api/faults", "injectFault", "202");
        Expect(OperationType.Get, "/api/admin/session", "getAdminSession", "200");
        Expect(OperationType.Post, "/api/admin/reset", "resetDemo", "200");
        Expect(OperationType.Get, "/api/injector/scenarios", "listScenarios", "200");
        Expect(OperationType.Post, "/api/injector/scenarios/{scenarioId}", "runScenario", "202");
        Expect(OperationType.Get, "/api/metrics", "getMetrics", "200");

        OpenApiDocs.Console.Paths["/api/capas"].Operations.Should().NotContainKey(OperationType.Post);
        OpenApiDocs.Console.Paths.Keys.Should().NotContain(path =>
            path.Contains("groundtruth", StringComparison.OrdinalIgnoreCase)
            || path.Contains("ground-truth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HistoryListsAcceptAnOptionalSiteFilter()
    {
        var document = OpenApiDocs.Console;
        foreach (var path in new[] { "/api/alerts", "/api/tickets", "/api/investigations", "/api/capas" })
        {
            var operation = document.Operation(path, OperationType.Get);
            var site = operation.Parameters
                .Select(document.Parameter)
                .Should()
                .ContainSingle(parameter => parameter.Name == "siteId")
                .Subject;
            site.In.Should().Be(ParameterLocation.Query);
            site.Required.Should().BeFalse();
        }

        document.Operation("/api/alerts", OperationType.Get)
            .Parameters.Select(document.Parameter)
            .Select(parameter => parameter.Name)
            .Should()
            .Equal("toolId", "siteId");
    }

    [Fact]
    public void ProblemResponsesCoverValidationNotFoundAndConflict()
    {
        var document = OpenApiDocs.Console;
        Problem(document, document.Operation("/api/incidents", OperationType.Post), "400");
        Problem(document, document.Operation("/api/incidents/{incidentId}", OperationType.Get), "404");
        Problem(document, document.Operation("/api/incidents/{incidentId}/investigation", OperationType.Get), "404");
        Problem(document, document.Operation("/api/incidents/{incidentId}/investigations", OperationType.Post), "409");
        Problem(document, document.Operation("/api/capas/{capaId}/advance", OperationType.Post), "409");

        var decision = document.Operation("/api/investigations/{investigationId}/decisions", OperationType.Post);
        var forbidden = document.Response(decision, "403");
        forbidden.Content.Should().ContainKey("application/problem+json");
        forbidden.Description.Should().Contain("token");
    }

    [Fact]
    public void IntakeChannelsSeveritiesAndStagesMatchTheSpec()
    {
        var document = OpenApiDocs.Console;
        document.Schema("IncidentChannel").EnumNames().Should().Equal(
            "iotSpcAlert",
            "operatorReport",
            "qmsNonconformance",
            "supplierQualityNotice",
            "faultInjector");
        document.Schema("Severity").EnumNames().Should().Equal("low", "medium", "high", "critical");
        document.Schema("IncidentStage").EnumNames().Should().Equal(
            "ticketOpened",
            "investigating",
            "recommendationReady",
            "escalated",
            "awaitingApproval",
            "approved",
            "rejected",
            "closed");
        document.Schema("ToolFamily").EnumNames().Should().Equal("deposition", "etch", "strip", "clean");

        var incident = document.Schema("Incident");
        incident.AdditionalPropertiesAllowed.Should().BeFalse();
        incident.Required.Should().BeEquivalentTo(
            "id",
            "toolId",
            "parameter",
            "channel",
            "severity",
            "summary",
            "correlationId",
            "openedAt",
            "stage");

        var open = document.Schema("OpenIncidentRequest");
        open.AdditionalPropertiesAllowed.Should().BeFalse();
        open.Required.Should().BeEquivalentTo("toolId", "parameter", "summary");
        open.Properties["summary"].MaxLength.Should().Be(4000);
    }

    [Fact]
    public void ApprovalIsServerIssuedAndAClientCannotSendASignature()
    {
        var document = OpenApiDocs.Console;
        var decision = document.Operation("/api/investigations/{investigationId}/decisions", OperationType.Post);
        decision.Description.Should().Contain("approverId");
        decision.Description.Should().Contain("signature");
        decision.Description.Should().Contain("single-use");

        var request = document.Schema("HumanDecisionRequest");
        request.AdditionalPropertiesAllowed.Should().BeFalse();
        request.Required.Should().Equal("kind");
        request.Properties.Keys.Should().BeEquivalentTo("kind", "comment", "approverId");
        request.Properties.Keys.Should().NotContain("signature");
        request.Properties.Keys.Should().NotContain("approvalToken");
        request.Properties["comment"].MaxLength.Should().Be(4000);
        document.Schema("HumanDecisionKind").EnumNames().Should().Equal("approve", "reject", "requestInfo", "override");

        var result = document.Schema("DecisionResult");
        result.AdditionalPropertiesAllowed.Should().BeFalse();
        result.Required.Should().Equal("investigation");
        result.Properties["capa"].Nullable.Should().BeTrue();
        result.Properties["capa"].AllOf.Should().ContainSingle().Which.Reference.Id.Should().Be("Capa");
        result.Required.Should().NotContain("capa");

        var capa = document.Schema("Capa");
        capa.AdditionalPropertiesAllowed.Should().BeFalse();
        capa.Required.Should().BeEquivalentTo(
            "id",
            "incidentId",
            "summary",
            "approvalTokenId",
            "actionHash",
            "stage",
            "openedAt");
        capa.Properties.Keys.Should().NotContain("signature");
    }

    [Fact]
    public void CapaStagesAreTheSpecSequenceAndTicketsUseTktNumbers()
    {
        var document = OpenApiDocs.Console;
        document.Schema("CapaStage").EnumNames().Should().Equal(
            "open",
            "containment",
            "rootCauseConfirmed",
            "correctiveAction",
            "verification",
            "closed");

        var advance = document.Schema("AdvanceCapaRequest");
        advance.AdditionalPropertiesAllowed.Should().BeFalse();
        advance.Required.Should().Equal("stage");

        document.Schema("Ticket").Properties["number"].Pattern.Should().Be("^TKT-.+");
    }

    [Fact]
    public void EvidenceHypothesesAndCategoriesMatchTheSpec()
    {
        var document = OpenApiDocs.Console;
        document.Schema("EvidenceSource").EnumNames().Should().Equal(
            "telemetry",
            "spc",
            "mes",
            "qms",
            "plm",
            "erp",
            "cmms",
            "document",
            "ticketing");

        var evidence = document.Schema("EvidenceItem");
        evidence.AdditionalPropertiesAllowed.Should().BeFalse();
        evidence.Required.Should().BeEquivalentTo("id", "source", "title", "excerpt", "observedAt");

        document.Schema("RootCauseCategory").EnumNames().Should().Equal(
            "engineeringChange",
            "equipmentCalibrationOrAlignment",
            "supplyChainMaterial",
            "processDrift");

        var hypothesis = document.Schema("Hypothesis");
        hypothesis.AdditionalPropertiesAllowed.Should().BeFalse();
        hypothesis.Required.Should().BeEquivalentTo(
            "id",
            "category",
            "cause",
            "confidence",
            "evidenceIds",
            "counterEvidence",
            "actions");
        hypothesis.Properties["confidence"].Minimum.Should().Be(0);
        hypothesis.Properties["confidence"].Maximum.Should().Be(1);
        hypothesis.Properties["evidenceIds"].MinItems.Should().Be(1);

        document.Schema("InvestigationStage").EnumNames().Should().Equal(
            "started",
            "evidenceGathered",
            "recommendationReady",
            "escalated",
            "awaitingApproval",
            "approved",
            "rejected",
            "failed");

        var investigation = document.Schema("Investigation");
        investigation.AdditionalPropertiesAllowed.Should().BeFalse();
        investigation.Required.Should().BeEquivalentTo(
            "id",
            "incidentId",
            "stage",
            "evidence",
            "hypotheses",
            "correlationId");
    }

    [Fact]
    public void FleetFaultsAndMetricsMatchTheSpec()
    {
        var document = OpenApiDocs.Console;
        var tool = document.Schema("Tool");
        tool.AdditionalPropertiesAllowed.Should().BeFalse();
        tool.Required.Should().BeEquivalentTo("id", "name", "family", "model", "bay", "siteId", "chambers");
        tool.Properties["model"].Description.Should().Contain("MPS-E7");

        var health = document.Schema("Health");
        health.AdditionalPropertiesAllowed.Should().BeFalse();
        health.Required.Should().BeEquivalentTo("status", "service");
        health.Properties["status"].EnumNames().Should().Equal("ok");

        document.Schema("FaultShape").EnumNames().Should().Equal("step", "ramp", "oscillation", "intermittent");
        var fault = document.Schema("FaultInjectionRequest");
        fault.AdditionalPropertiesAllowed.Should().BeFalse();
        fault.Required.Should().BeEquivalentTo("toolId", "parameter", "shape", "magnitude", "rootCauseCategory");
        fault.Properties["confounders"].Default.Should().BeOfType<OpenApiBoolean>();
        ((OpenApiBoolean)fault.Properties["confounders"].Default).Value.Should().BeFalse();

        var accepted = document.Schema("FaultInjectionAccepted");
        accepted.Required.Should().BeEquivalentTo("faultId", "correlationId");

        var metrics = document.Schema("MetricsSummary");
        metrics.AdditionalPropertiesAllowed.Should().BeFalse();
        metrics.Required.Should().BeEquivalentTo("mode", "spendTodayUsd", "spendCapUsd");
        metrics.Properties["mode"].EnumNames().Should().Equal("replay", "live");
        metrics.Properties["completionRate"].Nullable.Should().BeTrue();
        metrics.Properties["groundingScore"].Nullable.Should().BeTrue();
        metrics.Properties["categoryAccuracy"].Nullable.Should().BeTrue();
        metrics.Properties["latencyP95Ms"].Nullable.Should().BeTrue();
        metrics.Properties["costPerInvestigationUsd"].Nullable.Should().BeTrue();
        metrics.Properties["toolFailureCount"].Type.Should().Be("integer");
    }

    private static void Expect(OperationType method, string path, string operationId, string success)
    {
        var operation = OpenApiDocs.Console.Operation(path, method);
        operation.OperationId.Should().Be(operationId);
        operation.Responses.Should().ContainKey(success);
    }

    private static void Problem(OpenApiDocument document, OpenApiOperation operation, string status)
    {
        var response = document.Response(operation, status);
        response.Content.Should().ContainKey("application/problem+json");
        response.Content["application/problem+json"].Schema.Target(document).Properties.Keys.Should().Contain("status");
    }
}
