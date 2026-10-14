// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Microsoft.OpenApi.Models;

namespace Mps.Contract.Tests;

public sealed class MswHandlerContractTests
{
    [Fact]
    public void MswHandlersCoverTheConsoleOperationsAndNoOthers()
    {
        var text = File.ReadAllText(Path.Combine(RepoPaths.Contracts, "msw", "handlers.ts"));
        var handlers = Regex.Matches(text, "http\\.(get|post)\\(\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => (
                Method: match.Groups[1].Value.Equals("get", StringComparison.Ordinal) ? OperationType.Get : OperationType.Post,
                Path: Regex.Replace(match.Groups[2].Value, ":([A-Za-z]+)", "{$1}")))
            .ToArray();

        var operations = OpenApiDocs.Console.Paths
            .SelectMany(path => path.Value.Operations.Select(operation => (operation.Key, Path: path.Key)))
            .ToArray();

        handlers.Should().BeEquivalentTo(operations);
    }
}
