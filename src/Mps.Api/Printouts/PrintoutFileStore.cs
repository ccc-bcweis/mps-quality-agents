// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

namespace Mps.Api.Printouts;

/// <summary>Keeps uploaded printout files in memory so a confirmed reading can attach its sheet as evidence. Reset clears it.</summary>
public sealed class PrintoutFileStore
{
    public const int MaxFiles = 50;

    private readonly ConcurrentDictionary<Guid, StoredPrintoutFile> _files = new();

    public Guid Add(byte[] bytes, string? contentType, string? fileName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (_files.Count >= MaxFiles)
        {
            foreach (var oldest in _files.OrderBy(pair => pair.Value.At).Take(_files.Count - MaxFiles + 1))
            {
                _files.TryRemove(oldest.Key, out _);
            }
        }

        var id = Guid.NewGuid();
        _files[id] = new StoredPrintoutFile(bytes, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, Path.GetFileName(fileName ?? "printout"), DateTimeOffset.UtcNow);
        return id;
    }

    public StoredPrintoutFile? Find(Guid id) => _files.TryGetValue(id, out var file) ? file : null;

    public void Clear() => _files.Clear();
}

public sealed record StoredPrintoutFile(byte[] Bytes, string ContentType, string FileName, DateTimeOffset At);
