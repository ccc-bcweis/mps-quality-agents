// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

namespace Mps.Api.Tests;

public sealed class JoinLinkTests
{
    private const string Home = "01aa7360-027f-4de4-9d7e-33b17e9bb8fb";
    private const string Outside = "9188040d-6c67-4c5b-b112-36a304b66dad";

    [Fact]
    public async Task OutsiderIsBlockedUntilRedeemingALinkAndTheLinkStopsAtMaxUses()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Auth:Mode", "azure");
            builder.UseSetting("Mps:Auth:HomeTenantId", Home);
            builder.UseSetting("Mps:Auth:AdminEmails", "owner@example.com");
        });
        var admin = Client(factory, Principal("oid-admin", Home, "owner@example.com", "Demo Owner"));
        var guest = Client(factory, Principal("oid-guest", Outside, "guest@outlook.com", "Guest One"));
        var second = Client(factory, Principal("oid-guest-2", Outside, "two@outlook.com", "Guest Two"));
        var homeUser = Client(factory, Principal("oid-home", Home, "colleague@example.com", "Colleague"));

        (await guest.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await homeUser.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await homeUser.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var created = await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 1 });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await created.Content.ReadFromJsonAsync<JsonElement>();
        var url = link.GetProperty("url").GetString()!;
        var token = url[(url.LastIndexOf('/') + 1)..];

        (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.OK, "a member redeeming again does not spend a use");
        (await second.PostAsync("/api/join/" + token, null)).StatusCode.Should().Be(HttpStatusCode.Gone, "the link allowed one use");
        (await second.GetAsync("/api/tools")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/admin/join-links");
        list.GetProperty("links")[0].GetProperty("uses").GetInt32().Should().Be(1);
        list.GetProperty("members").EnumerateArray().Should().Contain(member => member.GetProperty("email").GetString() == "guest@outlook.com");

        var another = await (await admin.PostAsJsonAsync("/api/admin/join-links", new { hours = 1, maxUses = 3 })).Content.ReadFromJsonAsync<JsonElement>();
        (await admin.DeleteAsync("/api/admin/join-links/" + another.GetProperty("id").GetGuid())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var revokedUrl = another.GetProperty("url").GetString()!;
        (await second.PostAsync("/api/join/" + revokedUrl[(revokedUrl.LastIndexOf('/') + 1)..], null)).StatusCode.Should().Be(HttpStatusCode.Gone);
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
}
