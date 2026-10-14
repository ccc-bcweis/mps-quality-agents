// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.MockEnterprise;

public sealed class MockOptions
{
    public const string SectionName = "Mock";

    public string SeedPath { get; set; } = Path.Combine("data", "seed", "enterprise.json");

    public int TimeoutSeconds { get; set; } = 30;
}
