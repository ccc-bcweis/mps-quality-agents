// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.Configuration;

namespace Mps.Api.Tests;

/// <summary>
/// Production keeps the per-query database log and the per-request HTTP client log off: at 10x they were most of the
/// container's log volume and CPU. The app's own events (model calls, tickets) stay at Information.
/// </summary>
public sealed class ProductionLoggingTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", "Warning")]
    [InlineData("Microsoft.EntityFrameworkCore", "Warning")]
    [InlineData("System.Net.Http.HttpClient", "Warning")]
    [InlineData("Default", "Information")]
    public void ProductionLogLevels(string category, string expected)
    {
        var api = Path.Combine(FindRepo(), "src", "Mps.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(api)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: false)
            .Build();

        Assert.Equal(expected, configuration[$"Logging:LogLevel:{category}"]);
    }

    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("MPS.sln was not found above the test output directory.");
    }
}
