// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Reflection;

using Mps.Application.Tools;
using Mps.Application.Workflow;
using Mps.Application.Writes;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Contract.Tests;

/// <summary>
/// The in-process contract (domain enums and tool ports) uses the same names as the HTTP contract.
/// JSON names are camelCase. C# names stay PascalCase.
/// </summary>
public sealed class ContractAlignmentTests
{
    [Fact]
    public void DomainEnumsMatchTheConsoleSchemaNames()
    {
        Names<ToolFamily>().Should().Equal(OpenApiDocs.Console.Schema("ToolFamily").EnumNames());
        Names<Severity>().Should().Equal(OpenApiDocs.Console.Schema("Severity").EnumNames());
        Names<IncidentChannel>().Should().Equal(OpenApiDocs.Console.Schema("IncidentChannel").EnumNames());
        Names<IncidentStage>().Should().Equal(OpenApiDocs.Console.Schema("IncidentStage").EnumNames());
        Names<EvidenceSource>().Should().Equal(OpenApiDocs.Console.Schema("EvidenceSource").EnumNames());
        Names<RootCauseCategory>().Should().Equal(OpenApiDocs.Console.Schema("RootCauseCategory").EnumNames());
        Names<InvestigationStage>().Should().Equal(OpenApiDocs.Console.Schema("InvestigationStage").EnumNames());
        Names<HumanDecisionKind>().Should().Equal(OpenApiDocs.Console.Schema("HumanDecisionKind").EnumNames());
        Names<CapaStage>().Should().Equal(OpenApiDocs.Console.Schema("CapaStage").EnumNames());
    }

    [Fact]
    public void InvestigationToolsAreReadOnlyAndMatchTheSpec()
    {
        var tools = typeof(ITelemetryTool).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(ITelemetryTool).Namespace)
            .Select(type => type.Name)
            .ToArray();
        tools.Should().BeEquivalentTo(
            nameof(ITelemetryTool),
            nameof(ISpcTool),
            nameof(IMesTool),
            nameof(IQmsQueryTool),
            nameof(IPlmTool),
            nameof(IErpTool),
            nameof(ICmmsTool),
            nameof(IDocumentSearchTool),
            nameof(IImpactScopeTool));

        Methods(typeof(ITelemetryTool)).Should().Equal("GetWindow");
        Methods(typeof(ISpcTool)).Should().Equal("GetViolations");
        Methods(typeof(IMesTool)).Should().Equal("GetRuns", "GetLots");
        Methods(typeof(IQmsQueryTool)).Should().Equal("SearchNcrs");
        Methods(typeof(IPlmTool)).Should().Equal("GetEcos", "GetConfig");
        Methods(typeof(IErpTool)).Should().Equal("GetMaterialLots", "GetSupplierNotices");
        Methods(typeof(ICmmsTool)).Should().Equal("GetWorkOrders", "GetCalibrations");
        Methods(typeof(IDocumentSearchTool)).Should().Equal("Search");
        Methods(typeof(IImpactScopeTool)).Should().Equal("GetScope");

        var readMethods = tools
            .Select(name => typeof(ITelemetryTool).Assembly.GetType("Mps.Application.Tools." + name)!)
            .SelectMany(Methods);
        readMethods.Should().NotContain(name =>
            name.Contains("Create", StringComparison.Ordinal)
            || name.Contains("Approve", StringComparison.Ordinal)
            || name.Contains("Advance", StringComparison.Ordinal)
            || name.Contains("Write", StringComparison.Ordinal));
    }

    [Fact]
    public void WritePortsAndTheWorkflowBoundaryMatchTheSpec()
    {
        Methods(typeof(IQmsCapaWriter)).Should().Equal("Create");
        Methods(typeof(ITicketingPort)).Should().Equal("Open");
        Methods(typeof(IInvestigationWorkflow)).Should().Equal(
            "Start",
            "ResumeWithHumanInput",
            "GetState");

        typeof(IQmsCapaWriter).Namespace.Should().Be("Mps.Application.Writes");
        typeof(ITelemetryTool).Namespace.Should().Be("Mps.Application.Tools");
        typeof(ITelemetryTool).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(IQmsCapaWriter).Namespace)
            .Select(type => type.Name)
            .Should().BeEquivalentTo(nameof(IQmsCapaWriter), nameof(ITicketingPort));
    }

    private static string[] Names<TEnum>()
        where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>().Select(ToCamelCase).ToArray();
    }

    private static string[] Methods(Type contract)
    {
        return contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name.EndsWith("Async", StringComparison.Ordinal)
                ? method.Name[..^"Async".Length]
                : method.Name)
            .ToArray();
    }

    private static string ToCamelCase(string name)
    {
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
