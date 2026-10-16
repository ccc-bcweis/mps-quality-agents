// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

using Mps.Api;

namespace Mps.Api.IntegrationTests.Support;

public sealed class MpsApiFactory : WebApplicationFactory<ApiHostMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        // Each fixture gets its own SQLite folder so open hosts never share a database file.
        builder.UseSetting("Mps:DataDirectory", Path.Combine(Path.GetTempPath(), "mps-it-" + Guid.NewGuid().ToString("N")));
        builder.UseSetting("Mps:Simulation:RealTime", "false");
    }
}
