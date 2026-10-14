// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Impact;

/// <summary>
/// One tool, chamber, recipe, or lot touched by the incident window.
/// </summary>
public sealed class ImpactObservation
{
    public ImpactObservation(string toolId, string? chamberId, string? recipeId, string? lotId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool id is required.", nameof(toolId));
        }

        ToolId = toolId.Trim();
        ChamberId = BlankToNull(chamberId);
        RecipeId = BlankToNull(recipeId);
        LotId = BlankToNull(lotId);
    }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string? RecipeId { get; }

    public string? LotId { get; }

    private static string? BlankToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
