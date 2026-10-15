// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Printouts;

public interface IPrintoutTextExtractor
{
    /// <summary>Text from a PDF. Pictures and printouts with no text layer return empty.</summary>
    string Extract(ReadOnlyMemory<byte> bytes, string? mediaType, string? fileName);
}

public interface IPrintoutFieldParser
{
    bool TryParse(string text, out PrintoutReading reading);
}

/// <summary>
/// Reads a scan through the model gateway. Mock and replay modes return the sample parse.
/// </summary>
public interface IPrintoutModelReader
{
    Task<PrintoutReading?> ReadAsync(
        ReadOnlyMemory<byte> bytes,
        string? mediaType,
        string? fileName,
        CancellationToken cancellationToken);
}

/// <summary>
/// Applies a confirmed reading to the running simulation. The simulator tick itself belongs to the simulation lane.
/// </summary>
public interface ISimulationFaultPort
{
    bool TryApply(string toolId, string? chamberId, string parameter, string shape, double magnitude);
}

/// <summary>
/// Bench readings kept for the life of the demo so an investigation can cite them. Cleared on demo reset.
/// </summary>
public interface IManualMeasurementLog
{
    void Add(ManualMeasurement measurement);

    IReadOnlyList<ManualMeasurement> ForTool(string toolId);

    void Clear();
}
