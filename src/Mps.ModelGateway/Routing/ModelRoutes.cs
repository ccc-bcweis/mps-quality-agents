// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// Route names stored on the cost ledger and on response metadata.
/// </summary>
public static class ModelRoutes
{
    public const string AzureOpenAI = "azure-openai";

    public const string OpenAI = "openai";

    public const string Replay = "replay";

    public const string Mock = "mock";
}
