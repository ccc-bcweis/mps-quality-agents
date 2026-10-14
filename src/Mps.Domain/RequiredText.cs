// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain;

internal static class RequiredText
{
    public static string Value(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(message);
        }

        return value.Trim();
    }
}
