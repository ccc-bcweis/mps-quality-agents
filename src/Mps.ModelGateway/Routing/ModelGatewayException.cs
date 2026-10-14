// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// The gateway could not complete a call with the configured route.
/// </summary>
public sealed class ModelGatewayException : Exception
{
    public ModelGatewayException(string message)
        : base(message)
    {
    }

    public ModelGatewayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
