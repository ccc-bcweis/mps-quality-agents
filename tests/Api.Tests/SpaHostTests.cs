// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Persistence;

namespace Mps.Api.Tests;

public sealed class SpaHostTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "mps-wwwroot-" + Guid.NewGuid().ToString("N"));
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), "mps-spa-" + Guid.NewGuid().ToString("N") + ".db");

    public SpaHostTests()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html data-mps-spa=\"console\"></html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "/* mps-console */");
    }

    [Fact]
    public async Task AMissingWebRootLeavesTheApiOnly()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var root = await client.GetAsync(new Uri("/", UriKind.Relative));
        var health = await client.GetAsync(new Uri("/health", UriKind.Relative));

        root.StatusCode.Should().Be(HttpStatusCode.NotFound);
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebRootServesTheConsoleAndLeavesApiRoutesAlone()
    {
        await using var factory = new SpaFactory(_webRoot, _databasePath);
        var client = factory.CreateClient();

        var root = await client.GetAsync(new Uri("/", UriKind.Relative));
        var screen = await client.GetAsync(new Uri("/fleet", UriKind.Relative));
        var asset = await client.GetAsync(new Uri("/assets/app.js", UriKind.Relative));
        var missingAsset = await client.GetAsync(new Uri("/assets/missing.js", UriKind.Relative));
        var missingApi = await client.GetAsync(new Uri("/api/not-a-route", UriKind.Relative));
        var health = await client.GetFromJsonAsync<JsonElement>(new Uri("/health", UriKind.Relative));

        (await root.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        root.StatusCode.Should().Be(HttpStatusCode.OK);
        root.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        screen.StatusCode.Should().Be(HttpStatusCode.OK);
        (await screen.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        asset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asset.Content.ReadAsStringAsync()).Should().Contain("mps-console");
        missingAsset.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingApi.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missingApi.Content.ReadAsStringAsync()).Should().NotContain("data-mps-spa");
        health.GetProperty("status").GetString().Should().Be("ok");
        health.GetProperty("service").GetString().Should().Be("Mps.Api");
    }

    public void Dispose()
    {
        TryDeleteFile(_databasePath);
        TryDeleteFile(_databasePath + "-wal");
        TryDeleteFile(_databasePath + "-shm");
        try
        {
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // The host may still be releasing a file. The temp name will not be reused.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The host may still be releasing the file. The temp name will not be reused.
        }
    }

    private sealed class SpaFactory : WebApplicationFactory<Program>
    {
        private readonly string _webRoot;
        private readonly string _databasePath;

        public SpaFactory(string webRoot, string databasePath)
        {
            _webRoot = webRoot;
            _databasePath = databasePath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Otel:Enabled", "false");
            builder.UseWebRoot(_webRoot);
            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(service =>
                        service.ServiceType == typeof(DbContextOptions<QualityDbContext>)
                        || service.ServiceType == typeof(QualityDbContext))
                    .ToList();
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<QualityDbContext>(options => options.UseSqlite("Data Source=" + _databasePath));
            });
        }
    }
}
