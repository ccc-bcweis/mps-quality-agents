// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;

namespace Mps.Simulator.Tests;

public sealed class FleetSchemaTests
{
    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void FleetHasFourFamiliesAndEightToolsAcrossTwoBays()
    {
        Catalog.Tools.Should().HaveCount(8);
        Catalog.Tools.Select(tool => tool.Family).Distinct().Should().BeEquivalentTo(
            [ToolFamily.Deposition, ToolFamily.Etch, ToolFamily.Strip, ToolFamily.Clean]);
        Catalog.Tools.GroupBy(tool => tool.Family).Should().AllSatisfy(group => group.Should().HaveCount(2));
        Catalog.Tools.Select(tool => tool.Bay).Distinct().Should().BeEquivalentTo(["Bay-1", "Bay-2"]);
        Catalog.Tools.Where(tool => tool.Bay == "Bay-1").Select(tool => tool.Family).Should().BeEquivalentTo(
            [ToolFamily.Deposition, ToolFamily.Etch, ToolFamily.Strip, ToolFamily.Clean]);
    }

    [Fact]
    public void EveryStepWindowSitsInsideTheFamilyEnvelope()
    {
        var errors = FleetSchema.Evaluate(Catalog);

        errors.Should().BeEmpty();
        Catalog.Tools.Should().AllSatisfy(tool =>
        {
            tool.Model.Should().Be(FamilyParameterCatalog.ModelFor(tool.Family));
            tool.Recipes.Should().NotBeEmpty();
            tool.Recipes.Should().AllSatisfy(recipe =>
                recipe.Steps.Should().AllSatisfy(step => step.Windows.Should().NotBeEmpty()));
        });
    }

    [Fact]
    public void EtchEscAndDepositionPressureMatchTheSpecTable()
    {
        var etch = Catalog.GetRequired("E-02");
        etch.Family.Should().Be(ToolFamily.Etch);
        etch.Model.Should().Be("MPS-E7");
        etch.Bay.Should().Be("Bay-2");
        var esc = etch.Recipes.Single(recipe => recipe.Id == "OXIDE-ETCH").Step("main-etch")!.WindowFor("escTemperature");
        esc.Should().NotBeNull();
        esc!.Unit.Should().Be("C");
        esc.Minimum.Should().BeGreaterThanOrEqualTo(40);
        esc.Maximum.Should().BeLessThanOrEqualTo(60);

        var pressure = etch.Recipes.Single(recipe => recipe.Id == "OXIDE-ETCH").Step("main-etch")!.WindowFor("chamberPressure");
        pressure!.Unit.Should().Be("mTorr");
        pressure.Minimum.Should().BeGreaterThanOrEqualTo(20);
        pressure.Maximum.Should().BeLessThanOrEqualTo(80);

        var deposition = Catalog.GetRequired("D-01");
        var depPressure = deposition.Recipes.Single(recipe => recipe.Id == "PECVD-OXIDE").Step("deposit")!.WindowFor("chamberPressure");
        depPressure!.Unit.Should().Be("Torr");
        depPressure.Minimum.Should().BeGreaterThanOrEqualTo(2);
        depPressure.Maximum.Should().BeLessThanOrEqualTo(6);
    }

    [Fact]
    public void LoadedToolsMapOntoDomainTools()
    {
        var domain = Catalog.ToDomainTools();

        domain.Should().HaveCount(8);
        domain.Select(tool => tool.Id.Value).Should().BeEquivalentTo("D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02");
        domain.Should().AllSatisfy(tool => tool.Chambers.Should().NotBeEmpty());
    }

    [Fact]
    public void WindowOutsideTheFamilyEnvelopeIsRejected()
    {
        var window = new OperatingWindow("chamberPressure", "Torr", 0.5, 1.2, 0.9);

        var errors = FleetSchema.EvaluateWindow(ToolFamily.Deposition, window, "fixture");

        errors.Should().Contain(error => error.Contains("outside the family envelope", StringComparison.Ordinal));
    }

    [Fact]
    public void WrongUnitAndUnknownParameterAreRejected()
    {
        var wrongUnit = new OperatingWindow("chamberPressure", "mTorr", 3, 4, 3.5);
        var unknown = new OperatingWindow("biasVoltage", "V", 0, 1, 0.5);

        FleetSchema.EvaluateWindow(ToolFamily.Deposition, wrongUnit, "fixture")
            .Should().Contain(error => error.Contains("unit", StringComparison.Ordinal));
        FleetSchema.EvaluateWindow(ToolFamily.Etch, unknown, "fixture")
            .Should().Contain(error => error.Contains("not a", StringComparison.Ordinal));
    }

    [Fact]
    public void NominalOutsideTheWindowIsRejected()
    {
        var act = () => new OperatingWindow("escTemperature", "C", 48, 56, 60);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Nominal*");
    }

    [Fact]
    public void MissingFleetDirectoryIsRejected()
    {
        var act = () => FleetCatalogLoader.Load(Path.Combine(Path.GetTempPath(), "mps-fleet-missing-" + Guid.NewGuid().ToString("N")));

        act.Should().Throw<FleetSpecException>().WithMessage("*does not exist*");
    }
}
