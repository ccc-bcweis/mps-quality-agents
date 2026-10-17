// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.AspNetCore.Http.Json;

using Mps.Domain.Json;

namespace Mps.MockEnterprise;

public static class EnterpriseJson
{
    public static readonly JsonSerializerOptions Options = EnterpriseJsonDefaults.Create();

    public static void Configure(JsonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnterpriseJsonDefaults.Apply(options.SerializerOptions);
    }
}
