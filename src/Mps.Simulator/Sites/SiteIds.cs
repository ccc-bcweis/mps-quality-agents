// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;

namespace Mps.Simulator.Sites;

/// <summary>
/// Plant ids and the prefix rule. Enterprise rows use this so a tool id always names one plant.
/// </summary>
public static class SiteIds
{
    public const int DefaultSeed = 4242;

    public const string Northwater = "northwater";

    public const string Ironwood = "ironwood";

    public const string Lumen = "lumen";

    public const string FrontEndRole = "Front-end wafer fab";

    public const string PilotRole = "Strip, clean, and pilot line";

    public static string ForTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool id is required.", nameof(toolId));
        }

        if (toolId.StartsWith("I-", StringComparison.Ordinal))
        {
            return Ironwood;
        }

        if (toolId.StartsWith("P-", StringComparison.Ordinal))
        {
            return Lumen;
        }

        return Northwater;
    }

    public static string NameFor(string siteId)
    {
        return siteId switch
        {
            Northwater => "Northwater Fab",
            Ironwood => "Ironwood Fab",
            Lumen => "Lumen Pilot Works",
            _ => siteId,
        };
    }

    public static ToolFamily FamilyOf(string toolId)
    {
        var core = toolId;
        if ((toolId.StartsWith("I-", StringComparison.Ordinal) || toolId.StartsWith("P-", StringComparison.Ordinal)) && toolId.Length > 2)
        {
            core = toolId[2..];
        }

        if (core.Length == 0)
        {
            throw new InvalidOperationException($"Tool id {toolId} has no family letter.");
        }

        return core[0] switch
        {
            'D' => ToolFamily.Deposition,
            'E' => ToolFamily.Etch,
            'S' => ToolFamily.Strip,
            'C' => ToolFamily.Clean,
            _ => throw new InvalidOperationException($"Tool id {toolId} has no known family letter."),
        };
    }
}
