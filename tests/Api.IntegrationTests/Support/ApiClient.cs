// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.IntegrationTests.Support;

internal readonly record struct ApiCall(HttpStatusCode Status, string Body)
{
    public JsonElement Json()
    {
        using var document = JsonDocument.Parse(Body);
        return document.RootElement.Clone();
    }

    public JsonElement Require(HttpStatusCode expected)
    {
        Status.Should().Be(expected, "response body was {0}", Body);
        // Error and not-found responses can carry no body.
        return string.IsNullOrWhiteSpace(Body) ? default : Json();
    }
}

internal static class ApiClient
{
    public static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web);

    public static async Task<ApiCall> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body = null,
        string? fault = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Serializer);
        }

        if (fault is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Mock-Fault", fault);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return new ApiCall(response.StatusCode, text);
    }

    public static string RequiredString(this JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetString();
        value.Should().NotBeNullOrWhiteSpace();
        return value!;
    }

    public static string Unique(string name)
    {
        return name + "-" + Guid.NewGuid().ToString("N")[..8];
    }
}
