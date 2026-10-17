// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Bus;

/// <summary>How many events one bus publish or store append carries. Shared by the seeders, the reset and the telemetry publisher.</summary>
internal static class EventBatch
{
    public const int Size = 2000;
}
