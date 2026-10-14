// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Decisions;

namespace Mps.Api.Hosting;

internal static class ApprovalPolicyLoader
{
    public static ApprovalPolicy Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var minutes = configuration.GetValue("Approval:LifetimeMinutes", 15);
        var lifetime = TimeSpan.FromMinutes(minutes <= 0 ? 15 : minutes);
        var configured = configuration["APPROVAL_SIGNING_KEY"];
        var key = string.IsNullOrWhiteSpace(configured)
            ? ApprovalPolicy.DemoSigningKey()
            : Convert.FromHexString(configured.Trim());
        return new ApprovalPolicy(key, lifetime);
    }
}
