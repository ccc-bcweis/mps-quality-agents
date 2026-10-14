// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// Keys written onto <c>ChatResponse.AdditionalProperties</c> for every gateway call.
/// </summary>
public static class GatewayMetadata
{
    public const string Route = "mps.route";

    public const string LiveRefused = "mps.liveRefused";

    public const string FellBack = "mps.fellBack";

    public const string PromptHash = "mps.promptHash";

    public const string CostUsd = "mps.costUsd";
}
