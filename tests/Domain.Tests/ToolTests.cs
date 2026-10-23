// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class ToolTests
{
    [Fact]
    public void AToolTrimsItsTextKeepsEveryChamberAndDropsABlankSite()
    {
        var chambers = new List<Chamber> { new("  C1  ", "  Chamber A  "), new("C2", "Chamber B") };
        var tool = new Mps.Domain.Equipment.Tool(
            new ToolId("E-02"),
            "  Etch 02  ",
            ToolFamily.Etch,
            "  MPS-E7  ",
            "  Bay B  ",
            chambers,
            "  austin  ");

        tool.Id.Value.Should().Be("E-02");
        tool.Name.Should().Be("Etch 02");
        tool.Family.Should().Be(ToolFamily.Etch);
        tool.Model.Should().Be("MPS-E7");
        tool.Bay.Should().Be("Bay B");
        tool.SiteId.Should().Be("austin");
        tool.Chambers.Should().HaveCount(2);
        tool.Chambers[0].Id.Should().Be("C1");
        tool.Chambers[0].Name.Should().Be("Chamber A");
        tool.Chambers[1].Name.Should().Be("Chamber B");

        chambers.Clear();
        tool.Chambers.Should().HaveCount(2);

        new Mps.Domain.Equipment.Tool(new ToolId("D-01"), "Dep", ToolFamily.Deposition, "MPS-D7", "Bay A", [new Chamber("A", "Chamber A")], null)
            .SiteId.Should().BeNull();
        new Mps.Domain.Equipment.Tool(new ToolId("S-02"), "Strip", ToolFamily.Strip, "MPS-S7", "Bay C", [new Chamber("A", "Chamber A")], "  ")
            .SiteId.Should().BeNull();
        new Mps.Domain.Equipment.Tool(new ToolId("C-01"), "Clean", ToolFamily.Clean, "MPS-C7", "Bay D", [new Chamber("A", "Chamber A")])
            .Family.Should().Be(ToolFamily.Clean);
    }

    [Fact]
    public void AToolRequiresAnIdANameAModelABayAndAChamber()
    {
        var chambers = new List<Chamber> { new("C1", "Chamber A") };
        Rejects(() => new Mps.Domain.Equipment.Tool(default, "Etch", ToolFamily.Etch, "MPS-E7", "Bay B", chambers), "Tool id is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), " ", ToolFamily.Etch, "MPS-E7", "Bay B", chambers), "Tool name is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), null!, ToolFamily.Etch, "MPS-E7", "Bay B", chambers), "Tool name is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, " ", "Bay B", chambers), "Tool model is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, null!, "Bay B", chambers), "Tool model is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, "MPS-E7", " ", chambers), "Bay is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, "MPS-E7", null!, chambers), "Bay is required.");
        Rejects(() => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, "MPS-E7", "Bay B", []), "A tool has at least one chamber.");

        var missing = () => new Mps.Domain.Equipment.Tool(new ToolId("E-02"), "Etch", ToolFamily.Etch, "MPS-E7", "Bay B", null!);
        missing.Should().Throw<ArgumentNullException>().WithParameterName("chambers");
    }

    [Fact]
    public void AChamberRequiresAnIdAndAName()
    {
        Rejects(() => new Chamber(" ", "Chamber A"), "Chamber id is required.");
        Rejects(() => new Chamber(null!, "Chamber A"), "Chamber id is required.");
        Rejects(() => new Chamber("C1", " "), "Chamber name is required.");
        Rejects(() => new Chamber("C1", null!), "Chamber name is required.");
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
