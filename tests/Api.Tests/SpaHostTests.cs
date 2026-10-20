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
        File.WriteAllText(Path.Combine(_webRoot, "landing.html"), "<!doctype html><html data-mps-landing></html>");
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
        var members = await client.GetAsync(new Uri("/admin/members", UriKind.Relative));
        var asset = await client.GetAsync(new Uri("/assets/app.js", UriKind.Relative));
        var missingAsset = await client.GetAsync(new Uri("/assets/missing.js", UriKind.Relative));
        var missingApi = await client.GetAsync(new Uri("/api/not-a-route", UriKind.Relative));
        var health = await client.GetFromJsonAsync<JsonElement>(new Uri("/health", UriKind.Relative));

        (await root.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        root.StatusCode.Should().Be(HttpStatusCode.OK);
        root.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        screen.StatusCode.Should().Be(HttpStatusCode.OK);
        (await screen.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        members.StatusCode.Should().Be(HttpStatusCode.OK);
        (await members.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        asset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asset.Content.ReadAsStringAsync()).Should().Contain("mps-console");
        missingAsset.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingApi.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missingApi.Content.ReadAsStringAsync()).Should().NotContain("data-mps-spa");
        health.GetProperty("status").GetString().Should().Be("ok");
        health.GetProperty("service").GetString().Should().Be("Mps.Api");
    }

    [Fact]
    public async Task HostedSignedOutBrowsersLandOnTheLandingPageAndTheApiRejectsThem()
    {
        await using var factory = new SpaFactory(_webRoot, _databasePath, hosted: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var root = await client.GetAsync(new Uri("/", UriKind.Relative));
        var screen = await client.GetAsync(new Uri("/tools/E-02?tab=alerts", UriKind.Relative));
        var landing = await client.GetAsync(new Uri("/landing.html", UriKind.Relative));
        var asset = await client.GetAsync(new Uri("/assets/app.js", UriKind.Relative));
        var api = await client.GetAsync(new Uri("/api/tools", UriKind.Relative));
        var hub = await client.PostAsync(new Uri("/hubs/fleet/negotiate?negotiateVersion=1", UriKind.Relative), null);
        var session = await client.GetAsync(new Uri("/api/admin/session", UriKind.Relative));

        root.StatusCode.Should().Be(HttpStatusCode.Redirect);
        root.Headers.Location!.OriginalString.Should().Be("/landing.html");
        screen.StatusCode.Should().Be(HttpStatusCode.Redirect);
        screen.Headers.Location!.OriginalString.Should().Be("/landing.html?next=" + Uri.EscapeDataString("/tools/E-02?tab=alerts"));
        landing.StatusCode.Should().Be(HttpStatusCode.OK);
        asset.StatusCode.Should().Be(HttpStatusCode.OK);
        api.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        hub.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        session.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HostedSignedInUsersGetAnUncachedConsoleAndAccountsWithoutAccessGetTheLandingPage()
    {
        await using var factory = new SpaFactory(_webRoot, _databasePath, hosted: true);
        var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        member.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Principal("home-tenant", "11111111-1111-1111-1111-111111111111"));
        var outsider = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        outsider.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Principal("outsider", "22222222-2222-2222-2222-222222222222"));

        var console = await member.GetAsync(new Uri("/", UriKind.Relative));
        var denied = await outsider.GetAsync(new Uri("/tools/E-02", UriKind.Relative));
        var membersDenied = await outsider.GetAsync(new Uri("/admin/members", UriKind.Relative));
        var membersAllowed = await member.GetAsync(new Uri("/admin/members", UriKind.Relative));
        var join = await outsider.GetAsync(new Uri("/join/some-token", UriKind.Relative));
        var deniedApi = await outsider.GetAsync(new Uri("/api/tools", UriKind.Relative));

        console.StatusCode.Should().Be(HttpStatusCode.OK);
        (await console.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        console.Headers.CacheControl!.NoStore.Should().BeTrue("a signed-out browser must not reopen a cached console");
        denied.StatusCode.Should().Be(HttpStatusCode.Redirect);
        denied.Headers.Location!.OriginalString.Should().Be("/landing.html?noAccess=1");
        membersDenied.StatusCode.Should().Be(HttpStatusCode.Redirect);
        membersDenied.Headers.Location!.OriginalString.Should().Be("/landing.html?noAccess=1");
        membersAllowed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await membersAllowed.Content.ReadAsStringAsync()).Should().Contain("data-mps-spa=\"console\"");
        join.StatusCode.Should().Be(HttpStatusCode.OK, "a join link opens for a signed-in guest");
        deniedApi.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Principal(string name, string tenantId)
    {
        var json = "{\"auth_typ\":\"aad\",\"claims\":[{\"typ\":\"name\",\"val\":\"" + name + "\"},"
            + "{\"typ\":\"http://schemas.microsoft.com/identity/claims/tenantid\",\"val\":\"" + tenantId + "\"},"
            + "{\"typ\":\"http://schemas.microsoft.com/identity/claims/objectidentifier\",\"val\":\"" + Guid.NewGuid() + "\"},"
            + "{\"typ\":\"preferred_username\",\"val\":\"" + name + "@example.com\"}],\"role_typ\":\"roles\"}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
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
        private readonly bool _hosted;

        public SpaFactory(string webRoot, string databasePath, bool hosted = false)
        {
            _webRoot = webRoot;
            _databasePath = databasePath;
            _hosted = hosted;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Otel:Enabled", "false");
            builder.UseSetting("Mps:Simulation:RealTime", "false");
            builder.UseWebRoot(_webRoot);
            if (_hosted)
            {
                builder.UseSetting("Mps:Auth:Mode", "azure");
                builder.UseSetting("Mps:Auth:HomeTenantId", "11111111-1111-1111-1111-111111111111");
                builder.UseSetting("Mps:Auth:AdminEmails", "owner@example.com");
            }

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
