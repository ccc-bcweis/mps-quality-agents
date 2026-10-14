// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Domain.Identity;

namespace Mps.Api.Fleet;

/// <summary>
/// Eight fictional tools, two per family, across two bays. Model names are synthetic.
/// </summary>
public static class DemoFleet
{
    public static IReadOnlyList<Tool> Create() =>
    [
        Tool("D-01", "Deposition 01", ToolFamily.Deposition, "MPS-D7", "Bay-1"),
        Tool("D-02", "Deposition 02", ToolFamily.Deposition, "MPS-D7", "Bay-2"),
        Tool("E-01", "Etch 01", ToolFamily.Etch, "MPS-E7", "Bay-1"),
        Tool("E-02", "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2"),
        Tool("S-01", "Strip 01", ToolFamily.Strip, "MPS-S7", "Bay-1"),
        Tool("S-02", "Strip 02", ToolFamily.Strip, "MPS-S7", "Bay-2"),
        Tool("C-01", "Clean 01", ToolFamily.Clean, "MPS-C7", "Bay-1"),
        Tool("C-02", "Clean 02", ToolFamily.Clean, "MPS-C7", "Bay-2"),
    ];

    private static Tool Tool(string id, string name, ToolFamily family, string model, string bay) =>
        new(
            new ToolId(id),
            name,
            family,
            model,
            bay,
            [new Chamber("A", "Chamber A"), new Chamber("B", "Chamber B")]);
}
