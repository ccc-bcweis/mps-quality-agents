// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.OpenApi.Models;

namespace Mps.Contract.Tests;

/// <summary>
/// Mock MES, QMS, PLM, ERP, CMMS, and ticketing HTTP contract.
/// Fault injection is the X-Mock-Fault header: 500, timeout, or 409.
/// </summary>
public sealed class MockEnterpriseContractTests
{
    [Fact]
    public void DocumentIsTheLocalMockAndHidesGroundTruth()
    {
        var yaml = File.ReadAllText(Path.Combine(RepoPaths.Contracts, "mock-enterprise.openapi.yaml"));
        yaml.Should().Contain("openapi: 3.0.3");
        yaml.Should().Contain("Ground truth is not on this API");
        yaml.Should().NotContain("AURA");

        var document = OpenApiDocs.Mock;
        document.Info.Title.Should().Contain("mock enterprise");
        document.Info.License.Name.Should().Be("MIT");
        document.Servers.Should().ContainSingle();
        document.Servers[0].Url.Should().Be("http://localhost:8081");
        document.Paths.Keys.Should().NotContain(path =>
            path.Contains("ground", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadModelsCoverTheSystemsTheInvestigationAgentMayCall()
    {
        var document = OpenApiDocs.Mock;
        Read(document, "/mes/runs", "getRuns");
        Read(document, "/mes/lots", "getLots");
        Read(document, "/qms/ncrs", "searchNcrs");
        Read(document, "/plm/ecos", "getEcos");
        Read(document, "/plm/config", "getConfig");
        Read(document, "/erp/lots", "getMaterialLots");
        Read(document, "/erp/notices", "getSupplierNotices");
        Read(document, "/cmms/work-orders", "getWorkOrders");
        Read(document, "/cmms/calibrations", "getCalibrations");

        document.Schema("ProcessRun").Required.Should().BeEquivalentTo(
            "runId", "toolId", "recipeId", "recipeRevision", "lotId", "startedAt");
        document.Schema("ConfigRevision").Required.Should().BeEquivalentTo(
            "configId", "toolId", "revision", "partName");
        document.Schema("MaterialLot").Required.Should().BeEquivalentTo(
            "lotId", "materialCode", "supplierName", "receivedAt");
        document.Schema("SupplierNotice").Required.Should().BeEquivalentTo(
            "noticeId", "supplierName", "materialCode", "lotId", "summary", "issuedAt");
        document.Schema("Calibration").Required.Should().BeEquivalentTo(
            "calibrationId", "toolId", "instrument", "dueAt", "isOverdue");
        document.Schema("Eco").Required.Should().BeEquivalentTo(
            "ecoId", "title", "effectiveAt", "changeSummary");
    }

    [Fact]
    public void CapaCreateRequiresAnApprovalTokenAndDeclares500TimeoutAnd409()
    {
        var document = OpenApiDocs.Mock;
        var create = document.Operation("/qms/capas", OperationType.Post);
        create.OperationId.Should().Be("createCapa");
        create.RequestBody.Required.Should().BeTrue();
        var body = create.RequestBody.Content["application/json"].Schema;
        body.AdditionalPropertiesAllowed.Should().BeFalse();
        body.Required.Should().BeEquivalentTo("capaId", "incidentId", "approvalTokenId", "summary");
        body.Properties.Keys.Should().NotContain("signature");

        create.Responses.Should().ContainKey("201");
        create.Responses["201"].Content["application/json"].Schema.Required.Should().Contain("externalId");
        create.Responses.Should().ContainKey("409");
        create.Responses["409"].Description.Should().Contain("Idempotent");
        create.Responses.Should().ContainKey("500");

        FaultHeader(document, create).Should().Equal("500", "timeout", "409");
    }

    [Fact]
    public void TicketsReturnTktNumbersAndAcceptTheSameFaultHeader()
    {
        var document = OpenApiDocs.Mock;
        var open = document.Operation("/ticketing/tickets", OperationType.Post);
        open.OperationId.Should().Be("openTicket");
        var body = open.RequestBody.Content["application/json"].Schema;
        body.AdditionalPropertiesAllowed.Should().BeFalse();
        body.Required.Should().BeEquivalentTo("incidentId", "toolId", "title");
        open.Responses.Should().ContainKey("201");
        open.Responses["201"].Content["application/json"].Schema.Properties["number"].Pattern.Should().Be("^TKT-.+");
        FaultHeader(document, open).Should().Equal("500", "timeout", "409");
    }

    private static void Read(OpenApiDocument document, string path, string operationId)
    {
        var operation = document.Operation(path, OperationType.Get);
        operation.OperationId.Should().Be(operationId);
        operation.Responses.Should().ContainKey("200");
    }

    private static string[] FaultHeader(OpenApiDocument document, OpenApiOperation operation)
    {
        var header = operation.Parameters
            .Select(document.Parameter)
            .Should().ContainSingle(parameter => parameter.Name == "X-Mock-Fault").Subject;
        header.In.Should().Be(ParameterLocation.Header);
        header.Required.Should().BeFalse();
        return header.Schema.EnumNames();
    }
}
