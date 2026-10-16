// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Reflection;

using Mps.Api.IntegrationTests;

namespace Mps.Contract.Tests;

/// <summary>
/// The integration suite runs in the default CI gate. This catalog keeps it there: no project filter,
/// no PendingLane trait, no skipped fact, and every fact listed in tests/PENDING.md.
/// </summary>
public sealed class PendingLaneCatalogTests
{
    [Fact]
    public void EveryIntegrationFactRunsInCiIsListedAndNotSkipped()
    {
        var assembly = typeof(ApprovalEnforcementTests).Assembly;
        var catalog = File.ReadAllText(Path.Combine(RepoPaths.Root, "tests", "PENDING.md"));
        var project = File.ReadAllText(Path.Combine(RepoPaths.Root, "tests", "Api.IntegrationTests", "Api.IntegrationTests.csproj"));
        project.Should().NotContain("VSTestTestCaseFilter", "the integration suite is part of the default CI run");

        var facts = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Fact: method.GetCustomAttribute<FactAttribute>(inherit: true)))
            .Where(item => item.Fact is not null)
            .ToArray();

        facts.Should().NotBeEmpty();
        foreach (var (method, fact) in facts)
        {
            var name = method.DeclaringType!.Name + "." + method.Name;
            fact!.Skip.Should().BeNull("{0} must run", name);
            HasPendingLane(method).Should().BeFalse("{0} must not be held out of CI", name);
            catalog.Should().Contain("`" + method.Name + "`", "{0} must be listed in tests/PENDING.md", name);
        }
    }

    private static bool HasPendingLane(MethodInfo method) =>
        HasPendingLane((MemberInfo)method) || HasPendingLane(method.DeclaringType!);

    private static bool HasPendingLane(MemberInfo member) =>
        member.GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(TraitAttribute))
            .Any(data => data.ConstructorArguments.Count == 2
                && data.ConstructorArguments[0].Value as string == "Category"
                && data.ConstructorArguments[1].Value as string == "PendingLane");
}
