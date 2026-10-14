// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Mps.Contract.Tests;

internal static class OpenApiDocs
{
    private static readonly Lazy<OpenApiDocument> ConsoleLazy = new(() => Load("openapi.yaml"));

    private static readonly Lazy<OpenApiDocument> MockLazy = new(() => Load("mock-enterprise.openapi.yaml"));

    public static OpenApiDocument Console => ConsoleLazy.Value;

    public static OpenApiDocument Mock => MockLazy.Value;

    public static OpenApiSchema Schema(this OpenApiDocument document, string name)
    {
        document.Components.Schemas.Should().ContainKey(name);
        return document.Components.Schemas[name];
    }

    public static OpenApiSchema Target(this OpenApiSchema schema, OpenApiDocument document)
    {
        if (schema.Reference?.Id is string id
            && document.Components.Schemas.TryGetValue(id, out var found))
        {
            return found;
        }

        return schema;
    }

    public static OpenApiResponse Response(this OpenApiDocument document, OpenApiOperation operation, string status)
    {
        operation.Responses.Should().ContainKey(status);
        var response = operation.Responses[status];
        if (response.Reference?.Id is string id
            && document.Components.Responses.TryGetValue(id, out var found))
        {
            return found;
        }

        return response;
    }

    public static OpenApiParameter Parameter(this OpenApiDocument document, OpenApiParameter parameter)
    {
        if (parameter.Reference?.Id is string id
            && document.Components.Parameters.TryGetValue(id, out var found))
        {
            return found;
        }

        return parameter;
    }

    public static string[] EnumNames(this OpenApiSchema schema)
    {
        schema.Enum.Should().NotBeEmpty();
        schema.Enum.Should().AllBeAssignableTo<OpenApiString>();
        return schema.Enum.Cast<OpenApiString>().Select(item => item.Value).ToArray();
    }

    public static OpenApiOperation Operation(this OpenApiDocument document, string path, OperationType method)
    {
        document.Paths.Should().ContainKey(path);
        document.Paths[path].Operations.Should().ContainKey(method);
        return document.Paths[path].Operations[method];
    }

    private static OpenApiDocument Load(string fileName)
    {
        var path = Path.Combine(RepoPaths.Contracts, fileName);
        using var stream = File.OpenRead(path);
        var document = new OpenApiStreamReader().Read(stream, out var diagnostic);
        diagnostic.Errors.Should().BeEmpty("because {0} is a frozen contract", fileName);
        document.Should().NotBeNull();
        return document!;
    }
}
