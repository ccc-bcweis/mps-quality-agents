// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain.Evidence;

/// <summary>
/// Plain tool and chamber names for operator screens. Record ids stay out of the sentence.
/// </summary>
public static class PlainEquipment
{
    public static string? ToolName(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        var id = toolId.Trim();
        var dash = id.IndexOf('-', StringComparison.Ordinal);
        if (dash != 1 || id.Length is < 3 or > 5)
        {
            return null;
        }

        var family = id[0] switch
        {
            'D' => "Deposition",
            'E' => "Etch",
            'S' => "Strip",
            'C' => "Clean",
            _ => null,
        };
        if (family is null)
        {
            return null;
        }

        var number = id[(dash + 1)..];
        if (number.Length is 0 or > 3 || !IsDigits(number))
        {
            return null;
        }

        return family + " tool " + id;
    }

    public static string? ChamberName(string? chamberId)
    {
        if (string.IsNullOrWhiteSpace(chamberId))
        {
            return null;
        }

        var id = chamberId.Trim();
        if (id.StartsWith("Chamber ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = id["Chamber ".Length..].Trim();
            if (rest.Length == 1 && rest[0] is >= 'A' and <= 'D')
            {
                return "Chamber " + char.ToUpperInvariant(rest[0]);
            }

            return null;
        }

        if (id.Length == 1 && id[0] is >= 'A' and <= 'D')
        {
            return "Chamber " + id;
        }

        if (id.Length >= 3 && id[^2] == '-' && id[^1] is >= 'A' and <= 'D')
        {
            return "Chamber " + id[^1];
        }

        if (id.Length == 2 && id[0] == 'C' && id[1] is >= '1' and <= '4')
        {
            return "Chamber " + (char)('A' + (id[1] - '1'));
        }

        return null;
    }

    private static bool IsDigits(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
