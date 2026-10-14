// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;

namespace Mps.Api.IntegrationTests;

/// <summary>
/// Mock enterprise writes accept X-Mock-Fault of 500, timeout, or 409.
/// A repeated CAPA create is an idempotent conflict. Ground truth is not on this API.
/// Spec sections 2.5, 2.6, and 7.4.
/// </summary>
[Trait("Category", "PendingLane")]
public sealed class ToolFailureTests
{
    [Fact]
    public async Task CreateCapaWithInjectedServerErrorReturns500()
    {
        using var client = MockEnterpriseApi.CreateClient();
        var failed = await ApiClient.SendAsync(client, HttpMethod.Post, "/qms/capas", CapaBody(), fault: "500");
        _ = failed.Require(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateCapaWithInjectedConflictReturns409()
    {
        using var client = MockEnterpriseApi.CreateClient();
        var conflict = await ApiClient.SendAsync(client, HttpMethod.Post, "/qms/capas", CapaBody(), fault: "409");
        _ = conflict.Require(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RepeatingACapaCreateIsAnIdempotentConflict()
    {
        using var client = MockEnterpriseApi.CreateClient();
        var body = CapaBody();
        var created = (await ApiClient.SendAsync(client, HttpMethod.Post, "/qms/capas", body))
            .Require(HttpStatusCode.Created);
        created.RequiredString("externalId").Should().NotBeNullOrWhiteSpace();

        var repeat = await ApiClient.SendAsync(client, HttpMethod.Post, "/qms/capas", body);
        _ = repeat.Require(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCapaTimeoutDoesNotSucceed()
    {
        using var client = MockEnterpriseApi.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/qms/capas");
        request.Headers.TryAddWithoutValidation("X-Mock-Fault", "timeout");
        request.Content = JsonContent.Create(CapaBody(), options: ApiClient.Serializer);

        HttpResponseMessage? response = null;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeFalse("response body was {0}", body);
        }
    }

    [Fact]
    public async Task CreateCapaWithoutApprovalTokenIdIsRejected()
    {
        using var client = MockEnterpriseApi.CreateClient();
        var rejected = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/qms/capas",
            new
            {
                capaId = Guid.NewGuid(),
                incidentId = Guid.NewGuid(),
                summary = "Hold the lot.",
            });
        _ = rejected.Require(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpenTicketWithInjectedServerErrorReturns500()
    {
        using var client = MockEnterpriseApi.CreateClient();
        var failed = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/ticketing/tickets",
            new
            {
                incidentId = Guid.NewGuid(),
                toolId = "E-02",
                title = "ESC temperature oscillation",
            },
            fault: "500");
        _ = failed.Require(HttpStatusCode.InternalServerError);
    }

    [Theory]
    [InlineData("/groundtruth")]
    [InlineData("/ground-truth")]
    [InlineData("/api/groundtruth")]
    [InlineData("/api/ground-truth")]
    public async Task GroundTruthIsNotOnTheMockEnterpriseApi(string path)
    {
        using var client = MockEnterpriseApi.CreateClient();
        var response = await ApiClient.SendAsync(client, HttpMethod.Get, path);
        _ = response.Require(HttpStatusCode.NotFound);
    }

    private static object CapaBody()
    {
        return new
        {
            capaId = Guid.NewGuid(),
            incidentId = Guid.NewGuid(),
            approvalTokenId = Guid.NewGuid(),
            summary = "Hold the lot and confirm the ring revision.",
        };
    }
}
