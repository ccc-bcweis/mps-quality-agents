// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Application.Intake;

/// <summary>
/// Channel defaults. A nonconformance is at least high. Critical always forces escalation before approval.
/// </summary>
public static class SeverityRules
{
    public static Severity Resolve(IncidentChannel channel, Severity? requested, Severity? signal)
    {
        if (requested == Severity.Critical || signal == Severity.Critical)
        {
            return Severity.Critical;
        }

        return channel switch
        {
            IncidentChannel.IotSpcAlert => signal ?? requested ?? Severity.High,
            IncidentChannel.OperatorReport => requested ?? Severity.Medium,
            IncidentChannel.QmsNonconformance => AtLeast(requested ?? signal, Severity.High),
            IncidentChannel.SupplierQualityNotice => requested ?? signal ?? Severity.High,
            IncidentChannel.FaultInjector => requested ?? signal ?? Severity.High,
            _ => requested ?? signal ?? Severity.Medium,
        };
    }

    public static bool ForcesEscalation(Severity severity) => severity == Severity.Critical;

    public static bool OpensTicketFromSignal(Severity severity) => severity >= Severity.High;

    private static Severity AtLeast(Severity? requested, Severity floor)
    {
        if (requested is null || requested.Value < floor)
        {
            return floor;
        }

        return requested.Value;
    }
}
