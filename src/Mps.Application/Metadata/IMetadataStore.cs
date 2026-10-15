// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Metadata;

/// <summary>
/// Read-only plant metadata. Loaded once, cached, and never touched by reset.
/// </summary>
public interface IMetadataStore
{
    CompanyProfile Company { get; }

    IReadOnlyList<SiteProfile> Sites { get; }

    IReadOnlyList<CapabilityProfile> Capabilities { get; }

    IReadOnlyList<ProductionLine> Lines { get; }

    IReadOnlyList<ToolProfile> Tools { get; }

    IReadOnlyList<BusinessRule> Rules { get; }

    IReadOnlyList<ProcessMap> ProcessMaps { get; }

    TimeSpan LoadDuration { get; }

    int LoadCount { get; }

    ToolProfile? FindTool(string toolId);

    string? ParameterClass(string toolId, string parameterId);

    string? SiteIdForTool(string toolId);
}
