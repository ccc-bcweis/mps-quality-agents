// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway;

/// <summary>
/// Marks the model gateway. It routes <see cref="IChatClient"/> and holds no business rules.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.ModelGateway";

    public static IChatClient RequireClient(IChatClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client;
    }
}
