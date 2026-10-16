// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Application.Faults;
using Mps.Domain.Enterprise;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class RuledOutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RootCauseCategory.EngineeringChange)]
    [InlineData(RootCauseCategory.EquipmentCalibrationOrAlignment)]
    [InlineData(RootCauseCategory.SupplyChainMaterial)]
    [InlineData(RootCauseCategory.ProcessDrift)]
    public void EveryInjectedMisleadingRecordReadsAsRuledOut(RootCauseCategory category)
    {
        var records = ConfounderFactory.For(category, new ToolId("E-02"), "E-02-A", Guid.NewGuid(), Now);

        var reasons = records.Ecos.Select(RuledOut.Reason)
            .Concat(records.WorkOrders.Select(RuledOut.Reason))
            .Concat(records.Notices.Select(RuledOut.Reason))
            .ToArray();

        reasons.Should().NotBeEmpty().And.OnlyContain(reason => reason != null);
    }

    [Fact]
    public void ARealChangeOrAnOutOfSpecLotIsNotRuledOut()
    {
        RuledOut.Reason(new EcoRecord("ECO-4471", "Edge ring rev C", "E-02", null, Now, "Install edge ring rev C.")).Should().BeNull();
        RuledOut.Reason(new SupplierNotice("SN-1", "S", "GAS-CF4", "PL-2044", "Lot PL-2044 is out of spec.", Now)).Should().BeNull();
        RuledOut.Reason(new WorkOrder("WO-1", new ToolId("E-02"), null, "pm", Now, "Replaced the liner.")).Should().BeNull();
    }
}
