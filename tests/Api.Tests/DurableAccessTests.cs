// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Access;

namespace Mps.Api.Tests;

public sealed class DurableAccessTests
{
    private const string Home = "01aa7360-027f-4de4-9d7e-33b17e9bb8fb";
    private const string Outside = "9188040d-6c67-4c5b-b112-36a304b66dad";

    [Fact]
    public async Task LinksAndMembersSurviveASqliteRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "mps-access-restart-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            string? token;
            await using (var first = new ApiFactory(path, deleteDatabase: false).WithWebHostBuilder(Hosted))
            {
                var admin = Client(first, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
                var guest = Client(first, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
                token = Token(await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 2 })).Content.ReadFromJsonAsync<JsonElement>());
                (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);
            }

            await using var second = new ApiFactory(path, deleteDatabase: false).WithWebHostBuilder(Hosted);
            var adminAgain = Client(second, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
            var guestAgain = Client(second, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
            var list = await adminAgain.GetFromJsonAsync<JsonElement>("/api/admin/join-links");
            list.GetProperty("links").EnumerateArray().Should().Contain(link => link.GetProperty("uses").GetInt32() == 1);
            list.GetProperty("members").EnumerateArray().Should().Contain(member => member.GetProperty("email").GetString() == "guest@outlook.com");
            (await guestAgain.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await guestAgain.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
    }

    [Fact]
    public async Task LinksAndMembersSurviveANewHostOverTheSameTableClient()
    {
        var links = new MemoryEntityTable();
        var members = new MemoryEntityTable();
        string? token;
        await using (var first = new ApiFactory().WithWebHostBuilder(builder =>
        {
            Hosted(builder);
            builder.ConfigureServices(services => UseTables(services, links, members));
        }))
        {
            var admin = Client(first, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
            var guest = Client(first, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
            token = Token(await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 2 })).Content.ReadFromJsonAsync<JsonElement>());
            (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using var second = new ApiFactory().WithWebHostBuilder(builder =>
        {
            Hosted(builder);
            builder.ConfigureServices(services => UseTables(services, links, members));
        });
        var adminAgain = Client(second, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
        var guestAgain = Client(second, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
        var list = await adminAgain.GetFromJsonAsync<JsonElement>("/api/admin/join-links");
        list.GetProperty("links").EnumerateArray().Should().Contain(link => link.GetProperty("uses").GetInt32() == 1);
        list.GetProperty("members").EnumerateArray().Should().Contain(member => member.GetProperty("status").GetString() == "active");
        (await guestAgain.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreAcceptsAUrlOrTokenAndRejectsMalformedOrActive()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(Hosted);
        var admin = Client(factory, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
        var guest = Client(factory, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
        var second = Client(factory, Principal("oid-guest-2", Outside, "two@outlook.com", "Guest Two"));

        foreach (var bad in new[] { "nope", "abcd", new string('g', 48), "/join/nope", "https://example.test/join/" + new string('a', 48) + "?token=" + new string('b', 48) })
        {
            var rejected = await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token = bad, hours = 1, maxUses = 1 });
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Title(rejected)).Should().Be("That is not a join link. Paste the link or the token.");
        }

        var created = await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).Content.ReadFromJsonAsync<JsonElement>();
        var active = Token(created);
        var conflict = await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token = active, hours = 2, maxUses = 2 });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Title(conflict)).Should().Be("That join link is already active.");

        (await admin.DeleteAsync("/api/admin/join-links/" + created.GetProperty("id").GetGuid())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var restored = await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token = "https://example.test/join/" + active, hours = 1, maxUses = 1 });
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.PostAsync("/api/join/" + active, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var spent = Token(await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).Content.ReadFromJsonAsync<JsonElement>());
        (await second.PostAsync("/api/join/" + spent, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var blocked = Client(factory, Principal("oid-guest-3", Outside, "three@outlook.com", "Guest Three"));
        (await blocked.PostAsync("/api/join/" + spent, null)).StatusCode.Should().Be(HttpStatusCode.Gone);
        (await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token = "/join/" + spent, hours = 1, maxUses = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await blocked.PostAsync("/api/join/" + spent, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var fresh = new string('c', 48);
        var query = await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token = "https://example.test/start?token=" + fresh.ToUpperInvariant(), hours = 1, maxUses = 1 });
        query.StatusCode.Should().Be(HttpStatusCode.OK);
        (await blocked.PostAsync("/api/join/" + fresh, null)).StatusCode.Should().Be(HttpStatusCode.OK, "an unknown token becomes a link, and a member redeeming again does not spend the new use");
    }

    [Fact]
    public async Task RevokedMemberIsBlockedUntilANewLink()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(Hosted);
        var admin = Client(factory, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
        var guest = Client(factory, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));

        var firstLink = await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).Content.ReadFromJsonAsync<JsonElement>();
        var firstToken = Token(firstLink);
        (await guest.PostAsync("/api/join/" + firstToken, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = await admin.GetFromJsonAsync<JsonElement>("/api/admin/join-links");
        var memberKey = listed.GetProperty("members").EnumerateArray().Single().GetProperty("memberKey").GetString();
        (await admin.PostAsJsonAsync("/api/admin/members/revoke", new { memberKey })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await guest.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await guest.PostAsync("/api/join/" + firstToken, null)).StatusCode.Should().Be(HttpStatusCode.Gone);

        var secondLink = await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).Content.ReadFromJsonAsync<JsonElement>();
        (await guest.PostAsync("/api/join/" + Token(secondLink), null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await admin.GetFromJsonAsync<JsonElement>("/api/admin/join-links");
        var member = after.GetProperty("members").EnumerateArray().Single(row => row.GetProperty("email").GetString() == "guest@outlook.com");
        member.GetProperty("status").GetString().Should().Be("active");
        member.GetProperty("linkLabel").GetString().Should().Be(secondLink.GetProperty("label").GetString());
    }

    [Fact]
    public async Task ReactivateDoesNotExtendAnExpiredLinkAndRestoreDoes()
    {
        var database = Path.Combine(Path.GetTempPath(), "mps-access-clock-" + Guid.NewGuid().ToString("N") + ".db");
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-15T17:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        var store = new JoinLinkStore(new SqliteAccessDirectory("Data Source=" + database + ";Pooling=False"), clock);
        try
        {
            await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            {
                Hosted(builder);
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IJoinLinkStore)).ToList())
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<IJoinLinkStore>(store);
                });
            });
            var admin = Client(factory, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
            var guest = Client(factory, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
            var created = await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).Content.ReadFromJsonAsync<JsonElement>();
            var token = Token(created);
            (await admin.DeleteAsync("/api/admin/join-links/" + created.GetProperty("id").GetGuid())).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.Gone);
            (await admin.PostAsync("/api/admin/join-links/" + created.GetProperty("id").GetGuid() + "/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);

            clock.UtcNow = clock.UtcNow.AddMinutes(61);
            (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK, "an active member redeeming again does not spend a use and is not blocked by expiry");
            var outsider = Client(factory, Principal("oid-guest-2", Outside, "two@outlook.com", "Guest Two"));
            var expired = await outsider.PostAsync("/api/join/" + token, null);
            expired.StatusCode.Should().Be(HttpStatusCode.Gone);
            (await Title(expired)).Should().Be("This join link has expired. Ask for a new one.");
            (await admin.PostAsync("/api/admin/join-links/" + created.GetProperty("id").GetGuid() + "/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await outsider.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.Gone);
            (await admin.PostAsJsonAsync("/api/admin/join-links/restore", new { token, hours = 1, maxUses = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await outsider.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            TryDelete(database);
            TryDelete(database + "-wal");
            TryDelete(database + "-shm");
        }
    }

    [Fact]
    public async Task NewEndpointsAreAdminOnly()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(Hosted);
        var home = Client(factory, Principal("oid-home", Home, "colleague@example.com", "Colleague"));
        var admin = Client(factory, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
        var missing = Guid.NewGuid();
        var garbage = new StringContent("not-json", Encoding.UTF8, "application/json");

        foreach (var response in new[]
        {
            await home.PostAsJsonAsync("/api/admin/join-links/restore", new { token = new string('a', 48), hours = 1, maxUses = 1 }),
            await home.PostAsync("/api/admin/join-links/" + missing + "/reactivate", null),
            await home.PostAsJsonAsync("/api/admin/members/revoke", new { memberKey = Outside + ":oid-guest" }),
            await home.GetAsync("/api/admin/join-links"),
            await home.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 }),
            await home.DeleteAsync("/api/admin/join-links/" + missing),
            await home.PostAsync("/api/admin/join-links/restore", garbage),
            await home.PostAsync("/api/admin/members/revoke", new StringContent("{", Encoding.UTF8, "application/json")),
        })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Title(response)).Should().Be("Only the admin can manage join links.");
        }

        var absent = await admin.PostAsync("/api/admin/join-links/" + missing + "/reactivate", null);
        absent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Title(absent)).Should().Be("That join link was not found.");
    }

    private static void Hosted(IWebHostBuilder builder)
    {
        builder.UseSetting("Mps:Auth:Mode", "azure");
        builder.UseSetting("Mps:Auth:HomeTenantId", Home);
        builder.UseSetting("Mps:Auth:AdminEmails", "owner@example.com");
    }

    private static void UseTables(IServiceCollection services, MemoryEntityTable links, MemoryEntityTable members)
    {
        foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IJoinLinkStore)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<IJoinLinkStore>(_ => new JoinLinkStore(new AzureTableAccessDirectory(links, members), TimeProvider.System));
    }

    private static HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string principal)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", principal);
        return client;
    }

    private static string Principal(string oid, string tid, string email, string name)
    {
        var json = "{\"auth_typ\":\"aad\",\"claims\":["
            + "{\"typ\":\"http://schemas.microsoft.com/identity/claims/objectidentifier\",\"val\":\"" + oid + "\"},"
            + "{\"typ\":\"http://schemas.microsoft.com/identity/claims/tenantid\",\"val\":\"" + tid + "\"},"
            + "{\"typ\":\"preferred_username\",\"val\":\"" + email + "\"},"
            + "{\"typ\":\"name\",\"val\":\"" + name + "\"}],\"role_typ\":\"roles\"}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string Token(JsonElement link)
    {
        var url = link.GetProperty("url").GetString()!;
        return url[(url.LastIndexOf('/') + 1)..];
    }

    private static async Task<string?> Title(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("title").GetString();
    }

    private static void TryDelete(string path)
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

    private sealed class ManualTimeProvider : TimeProvider
    {
        public ManualTimeProvider(DateTimeOffset start) => UtcNow = start;

        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
