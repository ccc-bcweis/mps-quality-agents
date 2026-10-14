// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// Stable SHA-256 of a chat request. The same messages and tools always hash the same way.
/// </summary>
public static class PromptHasher
{
    public static string Hash(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var builder = new StringBuilder();
        builder.Append("mps-replay-v1\n");
        builder.Append(options?.ModelId ?? string.Empty).Append('\n');

        foreach (var message in messages)
        {
            ArgumentNullException.ThrowIfNull(message);
            builder.Append(message.Role.Value).Append('\n');
            builder.Append(message.Text ?? string.Empty).Append('\n');
            foreach (var content in message.Contents)
            {
                AppendContent(builder, content);
            }

            builder.Append('\u001e');
        }

        if (options?.Tools is not null)
        {
            var names = options.Tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal);
            foreach (var name in names)
            {
                builder.Append("tool:").Append(name).Append('\n');
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static void AppendContent(StringBuilder builder, AIContent content)
    {
        switch (content)
        {
            case FunctionCallContent call:
                builder.Append("fn:").Append(call.CallId).Append(':').Append(call.Name).Append(':');
                if (call.Arguments is not null)
                {
                    foreach (var pair in call.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        builder.Append(pair.Key).Append('=').Append(Format(pair.Value)).Append(';');
                    }
                }

                builder.Append('\n');
                break;
            case FunctionResultContent result:
                builder.Append("result:").Append(result.CallId).Append(':').Append(Format(result.Result)).Append('\n');
                break;
        }
    }

    private static string Format(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
