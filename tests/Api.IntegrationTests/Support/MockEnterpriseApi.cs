// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

using Mps.MockEnterprise;

namespace Mps.Api.IntegrationTests.Support;

internal static class MockEnterpriseApi
{
    public static HttpClient CreateClient()
    {
        if (typeof(MockHostMarker).Assembly.EntryPoint is null)
        {
            throw new InvalidOperationException(
                "Mps.MockEnterprise has no web entry point yet. GB-1 (D4) must host the mock enterprise API in this assembly.");
        }

        return new MockEnterpriseFactory().CreateClient();
    }
}

internal sealed class MockEnterpriseFactory : WebApplicationFactory<MockHostMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
    }
}
