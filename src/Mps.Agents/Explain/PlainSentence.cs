// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Wording;

namespace Mps.Agents.Explain;

/// <summary>
/// One plain sentence a supervisor can read. The limits and the allowed terms come from
/// data/metadata/plain-sentence.yaml. A glossary term is allowed. Any other acronym is not.
/// </summary>
public static class PlainSentence
{
    public static int MaxLength => PlainWording.Shared.MaxLength;

    public static bool IsValid(string? text)
    {
        return TryProblem(text, out _);
    }

    public static bool TryProblem(string? text, out string problem)
    {
        var rule = PlainWording.Shared;
        problem = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "blank";
            return false;
        }

        var sentence = text.Trim();
        if (sentence.Length > rule.MaxLength)
        {
            problem = "too-long";
            return false;
        }

        if (rule.SentenceCase && !char.IsUpper(sentence[0]))
        {
            problem = "sentence-case";
            return false;
        }

        if (rule.OneSentence)
        {
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
        }

        if (rule.HasUnexplainedAcronym(sentence))
        {
            problem = "acronym";
            return false;
        }

        return true;
    }
}
