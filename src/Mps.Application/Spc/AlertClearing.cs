// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Spc;

/// <summary>
/// When an alert clears on its own. The signal must stay inside +/- <see cref="ZoneSigma"/> sigma of the center
/// for <see cref="InControlReadings"/> readings in a row after the alert was raised.
/// </summary>
public static class AlertClearing
{
    public const int InControlReadings = 8;

    public const double ZoneSigma = 2d;
}
