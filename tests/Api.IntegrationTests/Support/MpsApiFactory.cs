// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Mps.Api;

namespace Mps.Api.IntegrationTests.Support;

public sealed class MpsApiFactory : WebApplicationFactory<ApiHostMarker>
{
    /// <summary>Server warnings and errors go to the current test's output (see <see cref="TestOutputLoggerProvider"/>).</summary>
    public TestOutputLoggerProvider ServerLog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(ServerLog));
        builder.UseEnvironment(Environments.Development);
        // Each fixture gets its own SQLite folder so open hosts never share a database file.
        builder.UseSetting("Mps:DataDirectory", Path.Combine(Path.GetTempPath(), "mps-it-" + Guid.NewGuid().ToString("N")));
        builder.UseSetting("Mps:Simulation:RealTime", "false");
        builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        // These tests drive the approval lifecycle; the placeholder workflow always recommends, so the offline agents (which may escalate) stay out.
        builder.UseSetting("Mps:Model:Mode", "placeholder");
    }
}
