// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.MockEnterprise;

/// <summary>
/// Public instance type in the mock enterprise assembly. Integration tests pass it to
/// <c>WebApplicationFactory</c> once this project has a web entry point. <see cref="LayerAnchor"/>
/// is static and cannot be a type argument.
/// </summary>
public sealed class MockHostMarker
{
}
