// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.RegularExpressions;

using Mps.Simulator.Fleet;

namespace Mps.Simulator.Docs;

/// <summary>
/// Fills document templates from the fleet catalog. A window token becomes the operating range loaded from YAML.
/// </summary>
public static partial class DocumentCorpusGenerator
{
    public static IReadOnlyList<string> Write(FleetCatalog fleet, string templateDirectory, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        if (string.IsNullOrWhiteSpace(templateDirectory) || !Directory.Exists(templateDirectory))
        {
            throw new DirectoryNotFoundException($"Template directory was not found: {templateDirectory}");
        }

        Directory.CreateDirectory(outputDirectory);
        foreach (var existing in Directory.GetFiles(outputDirectory, "*.md"))
        {
            File.Delete(existing);
        }

        var written = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var template in Directory.GetFiles(templateDirectory, "*.md").Order(StringComparer.Ordinal))
        {
            var rendered = Render(fleet, File.ReadAllText(template));
            var id = ReadId(rendered);
            if (!seen.Add(id))
            {
                throw new InvalidOperationException($"Duplicate document id {id}.");
            }

            var path = Path.Combine(outputDirectory, id + ".md");
            var text = rendered.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
            if (!text.EndsWith('\n'))
            {
                text += "\n";
            }

            File.WriteAllText(path, text);
            written.Add(path);
        }

        if (written.Count != 12)
        {
            throw new InvalidOperationException($"The corpus expects 12 documents and rendered {written.Count}.");
        }

        return written;
    }

    public static string FormatWindow(OperatingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return string.Create(CultureInfo.InvariantCulture, $"{window.Minimum:0.###}-{window.Maximum:0.###} {window.Unit}");
    }

    private static string Render(FleetCatalog fleet, string template)
    {
        return WindowToken().Replace(template, match =>
        {
            var parts = match.Groups[1].Value.Split(':');
            if (parts.Length != 4)
            {
                throw new InvalidOperationException($"Window token '{match.Value}' needs tool, recipe, step, and parameter.");
            }

            var tool = fleet.GetRequired(parts[0]);
            var recipe = tool.Recipes.FirstOrDefault(item => string.Equals(item.Id, parts[1], StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Recipe {parts[1]} is not on {parts[0]}.");
            var step = recipe.Step(parts[2]) ?? throw new InvalidOperationException($"Step {parts[2]} is not on {parts[1]}.");
            var window = step.WindowFor(parts[3]) ?? throw new InvalidOperationException($"Parameter {parts[3]} is not on {parts[1]} {parts[2]}.");
            return FormatWindow(window);
        });
    }

    private static string ReadId(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("id:", StringComparison.Ordinal))
            {
                var id = trimmed[3..].Trim();
                if (id.Length == 0)
                {
                    break;
                }

                return id;
            }
        }

        throw new InvalidOperationException("A document template is missing id.");
    }

    [GeneratedRegex(@"\{\{window:([^}]+)\}\}")]
    private static partial Regex WindowToken();
}
