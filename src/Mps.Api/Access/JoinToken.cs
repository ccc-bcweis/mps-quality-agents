// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Security.Cryptography;

namespace Mps.Api.Access;

/// <summary>
/// Join tokens are 192 bits, stored as 48 hex characters. A pasted value may be that token or a join link
/// whose path is <c>/join/{token}</c> or whose query carries <c>token</c>.
/// </summary>
public static class JoinToken
{
    public const int Length = 48;

    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public static bool IsToken(string? value)
    {
        if (value is null || value.Length != Length)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(string? pasted, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(pasted))
        {
            return false;
        }

        var text = pasted.Trim();
        if (IsToken(text))
        {
            token = text.ToLowerInvariant();
            return true;
        }

        if (!TryUri(text, out var uri) || uri is null)
        {
            return false;
        }

        var fromPath = TokenFromPath(uri);
        if (fromPath is null && HasJoinSegment(uri))
        {
            return false;
        }

        var fromQuery = TokenFromQuery(uri);
        if (fromPath is not null && fromQuery is not null && !string.Equals(fromPath, fromQuery, StringComparison.Ordinal))
        {
            return false;
        }

        token = fromPath ?? fromQuery ?? string.Empty;
        return token.Length > 0;
    }

    private static bool TryUri(string text, out Uri? uri)
    {
        var candidate = text;
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            if (!candidate.StartsWith('/'))
            {
                uri = null;
                return false;
            }

            candidate = "https://join.invalid" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            uri = null;
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool HasJoinSegment(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TokenFromPath(Uri uri)
    {
        string? found = null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!segments[index].Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(segments[index + 1]);
            if (!IsToken(value))
            {
                return null;
            }

            var normalized = value.ToLowerInvariant();
            if (found is not null && !string.Equals(found, normalized, StringComparison.Ordinal))
            {
                return null;
            }

            found = normalized;
        }

        return found;
    }

    private static string? TokenFromQuery(Uri uri)
    {
        if (uri.Query.Length <= 1)
        {
            return null;
        }

        string? found = null;
        foreach (var pair in uri.Query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            if (!key.Equals("token", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (eq < 0)
            {
                return null;
            }

            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (!IsToken(value))
            {
                return null;
            }

            var normalized = value.ToLowerInvariant();
            if (found is not null && !string.Equals(found, normalized, StringComparison.Ordinal))
            {
                return null;
            }

            found = normalized;
        }

        return found;
    }
}
