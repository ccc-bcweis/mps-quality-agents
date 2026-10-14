// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;

namespace Mps.Connectors.Docs;

internal static class TextTokens
{
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
                continue;
            }

            Flush(current, tokens);
        }

        Flush(current, tokens);
        return tokens;
    }

    private static void Flush(StringBuilder current, List<string> tokens)
    {
        if (current.Length >= 2)
        {
            tokens.Add(current.ToString());
        }

        current.Clear();
    }
}
