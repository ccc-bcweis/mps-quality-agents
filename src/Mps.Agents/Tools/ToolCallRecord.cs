// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Tools;

/// <summary>
/// One model-requested tool call. Disallowed names are recorded and not executed.
/// </summary>
public sealed class ToolCallRecord
{
    private ToolCallRecord(string name, bool executed, string? flag)
    {
        Name = name;
        Executed = executed;
        Flag = flag;
    }

    public string Name { get; }

    public bool Executed { get; }

    public string? Flag { get; }

    public static ToolCallRecord Ran(string name)
    {
        return new ToolCallRecord(name, true, null);
    }

    public static ToolCallRecord Disallowed(string name)
    {
        return new ToolCallRecord(name, false, "disallowed");
    }
}
