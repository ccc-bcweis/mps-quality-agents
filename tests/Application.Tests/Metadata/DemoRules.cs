// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;

namespace Mps.Application.Tests.Metadata;

internal static class DemoRules
{
    private static readonly Lazy<YamlMetadataStore> Store = new(Load);

    private static readonly Lazy<IBusinessRuleEngine> EngineCache = new(() => new BusinessRuleEngine(Store.Value));

    public static YamlMetadataStore Metadata => Store.Value;

    public static IBusinessRuleEngine Engine => EngineCache.Value;

    public static string MetadataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "metadata", "company.yaml");
            if (File.Exists(candidate))
            {
                return Path.Combine(dir.FullName, "data", "metadata");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("data/metadata was not found from " + AppContext.BaseDirectory);
    }

    private static YamlMetadataStore Load()
    {
        return new YamlMetadataStore(MetadataDirectory());
    }
}
