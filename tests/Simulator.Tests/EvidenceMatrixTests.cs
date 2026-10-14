// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Evidence;
using Mps.Simulator.Faults;

namespace Mps.Simulator.Tests;

public sealed class EvidenceMatrixTests
{
    [Fact]
    public void MatrixCoversFourClassesWithDisjointSignals()
    {
        var matrix = EvidenceMatrix.Load(RepoPaths.EvidenceMatrixFile());

        matrix.Classes.Select(item => item.Category).Should().BeEquivalentTo(FaultCategories.All);
        matrix.Signals.Should().HaveCountGreaterThanOrEqualTo(8);
        foreach (var signal in matrix.Signals)
        {
            signal.Supports.Intersect(signal.RulesOut).Should().BeEmpty();
            signal.Plain.Should().NotContain("\n");
            signal.WhyItMatters.Length.Should().BeInRange(1, 220);
            foreach (var token in FaultPlainLanguage.HiddenLabels)
            {
                signal.Plain.Should().NotContain(token);
                signal.WhyItMatters.Should().NotContain(token);
            }
        }

        foreach (var category in FaultCategories.All)
        {
            matrix.Supporting(category).Should().HaveCountGreaterThanOrEqualTo(2);
            matrix.RulingOut(category).Should().NotBeEmpty();
            matrix.Classes.Should().Contain(item => item.Category == category && item.WhyItMatters.Length > 0);
        }
    }

    [Fact]
    public void ASignalThatSupportsAndRulesOutTheSameClassIsRejected()
    {
        var temp = Directory.CreateTempSubdirectory("mps-matrix");
        try
        {
            File.WriteAllText(
                Path.Combine(temp.FullName, "matrix.yaml"),
                """
                classes:
                  - { category: engineeringChange, whyItMatters: A released part change can move the wafer. }
                  - { category: equipmentCalibrationOrAlignment, whyItMatters: An overdue instrument can lie. }
                  - { category: supplyChainMaterial, whyItMatters: A bad lot can move every tool that used it. }
                  - { category: processDrift, whyItMatters: Slow wear can move the result after a clean. }
                signals:
                  - id: overlap
                    plain: This row both supports and rules out the same class.
                    whyItMatters: The row is invalid.
                    supports: [engineeringChange]
                    rulesOut: [engineeringChange]
                """);

            var act = () => EvidenceMatrix.Load(Path.Combine(temp.FullName, "matrix.yaml"));
            act.Should().Throw<EvidenceMatrixException>().WithMessage("*supports and rules out*");
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
