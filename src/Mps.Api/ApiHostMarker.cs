// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api;

/// <summary>
/// Public instance type in the API assembly. Contract and integration tests pass it to
/// <c>WebApplicationFactory</c>. <see cref="LayerAnchor"/> is static and cannot be a type argument.
/// The host entry point stays the generated Program class.
/// </summary>
public sealed class ApiHostMarker
{
}
