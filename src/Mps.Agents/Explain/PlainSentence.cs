// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;

namespace Mps.Agents.Explain;

/// <summary>
/// One plain sentence a supervisor can read: at most 240 characters, and no unexplained acronym.
/// </summary>
public static class PlainSentence
{
    public const int MaxLength = 240;

    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "spc", "capa", "pm", "oee", "mtbf", "mttr", "ec", "ecn", "eco", "ucl", "lcl", "cpk", "fdc", "ncr", "copq",
        "rf", "sccm", "mtorr", "mes", "qms", "plm", "erp", "cmms", "esc", "mfc", "nf3", "pecvd", "pvd", "diw",
        "sc1", "sc2", "sih4", "n2o", "cf4", "chf3",
    };

    public static bool IsValid(string? text)
    {
        return TryProblem(text, out _);
    }

    public static bool TryProblem(string? text, out string problem)
    {
        problem = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "blank";
            return false;
        }

        var sentence = text.Trim();
        if (sentence.Length > MaxLength)
        {
            problem = "too-long";
            return false;
        }

        if (!char.IsUpper(sentence[0]))
        {
            problem = "sentence-case";
            return false;
        }

        if (sentence[^1] is not ('.' or '!' or '?'))
        {
            problem = "not-a-sentence";
            return false;
        }

        for (var i = 0; i < sentence.Length - 1; i++)
        {
            if (sentence[i] is '.' or '!' or '?')
            {
                problem = "multiple-sentences";
                return false;
            }
        }

        if (HasUnexplainedAcronym(sentence))
        {
            problem = "acronym";
            return false;
        }

        return true;
    }

    private static bool HasUnexplainedAcronym(string sentence)
    {
        var token = new StringBuilder();
        for (var i = 0; i <= sentence.Length; i++)
        {
            var letter = i < sentence.Length && char.IsLetter(sentence[i]);
            if (letter)
            {
                token.Append(sentence[i]);
                continue;
            }

            if (token.Length == 0)
            {
                continue;
            }

            if (IsAcronym(token.ToString()))
            {
                return true;
            }

            token.Clear();
        }

        return false;
    }

    private static bool IsAcronym(string token)
    {
        if (Acronyms.Contains(token))
        {
            return true;
        }

        var letters = 0;
        var allUpper = true;
        foreach (var character in token)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            letters++;
            if (!char.IsUpper(character))
            {
                allUpper = false;
            }
        }

        return letters >= 2 && allUpper;
    }
}
