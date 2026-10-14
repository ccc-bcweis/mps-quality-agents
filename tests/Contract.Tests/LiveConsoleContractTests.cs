// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

using Mps.Api;

namespace Mps.Contract.Tests;

public sealed class MpsApiFactory : WebApplicationFactory<ApiHostMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
    }
}

/// <summary>
/// Checks the running console host against the parts of the contract that are already required
/// of every build: liveness, no anonymous CAPA create, and no ground-truth route.
/// </summary>
public sealed class LiveConsoleContractTests : IClassFixture<MpsApiFactory>
{
    private readonly MpsApiFactory _factory;

    public LiveConsoleContractTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthReturnsTheContractShape()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).Should().Equal("status", "service");
        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("service").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CapaCollectionDoesNotAcceptAClientCreate()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/capas", UriKind.Relative),
            new
            {
                incidentId = "8d0a1c4e-1b7a-4e5a-9c1d-0a6b2e3f4a5b",
                summary = "Create a CAPA without a human approval.",
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed },
            "response body was {0}",
            body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Created);
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/groundtruth")]
    [InlineData("/ground-truth")]
    [InlineData("/api/groundtruth")]
    [InlineData("/api/ground-truth")]
    public async Task ConsoleApiDoesNotExposeGroundTruth(string path)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
