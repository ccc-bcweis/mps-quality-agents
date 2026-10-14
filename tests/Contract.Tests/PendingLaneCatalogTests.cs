// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Reflection;

using Mps.Api.IntegrationTests;

namespace Mps.Contract.Tests;

/// <summary>
/// The integration suite is excluded from the default CI run. This catalog keeps that exclusion
/// honest: every fact stays pending, none is skipped, and each name is listed for the owning lane.
/// </summary>
public sealed class PendingLaneCatalogTests
{
    [Fact]
    public void EveryIntegrationFactIsPendingListedAndNotSkipped()
    {
        var assembly = typeof(ApprovalEnforcementTests).Assembly;
        var pending = File.ReadAllText(Path.Combine(RepoPaths.Root, "tests", "PENDING.md"));
        var project = File.ReadAllText(Path.Combine(
            RepoPaths.Root,
            "tests",
            "Api.IntegrationTests",
            "Api.IntegrationTests.csproj"));
        project.Should().Contain(
            "Category!=PendingLane",
            "the default dotnet test command has no filter of its own, so the project supplies one");

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
            fact!.Skip.Should().BeNull("{0} stays discoverable when the pending filter is selected", name);
            HasPendingLane(method).Should().BeTrue(
                "{0} needs Category=PendingLane. A test without that trait can disappear under the not-equal filter.",
                name);
            pending.Should().Contain("`" + method.Name + "`", "{0} must be listed in tests/PENDING.md", name);
        }
    }

    private static bool HasPendingLane(MethodInfo method)
    {
        return HasPendingLane((MemberInfo)method) || HasPendingLane(method.DeclaringType!);
    }

    private static bool HasPendingLane(MemberInfo member)
    {
        // TraitAttribute keeps its pair in the constructor. It has no Name or Value property.
        return member.GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(TraitAttribute))
            .Any(data =>
                data.ConstructorArguments.Count == 2
                && data.ConstructorArguments[0].Value as string == "Category"
                && data.ConstructorArguments[1].Value as string == "PendingLane");
    }
}
