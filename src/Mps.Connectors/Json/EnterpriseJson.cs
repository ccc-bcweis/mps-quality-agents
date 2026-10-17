// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Domain.Json;

namespace Mps.Connectors;

internal static class EnterpriseJson
{
    public static readonly JsonSerializerOptions Options = EnterpriseJsonDefaults.Create();
}
