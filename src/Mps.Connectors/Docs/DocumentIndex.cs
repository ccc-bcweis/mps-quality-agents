// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Mps.Domain.Enterprise;

namespace Mps.Connectors.Docs;

public sealed class IndexedChunk
{
    public IndexedChunk(string id, string documentId, string title, string? toolFamily, string revision, string heading, string text, float[] embedding)
    {
        Id = id;
        DocumentId = documentId;
        Title = title;
        ToolFamily = string.IsNullOrWhiteSpace(toolFamily) ? null : toolFamily.Trim();
        Revision = revision;
        Heading = heading;
        Text = text;
        Embedding = embedding;
    }

    public string Id { get; }

    public string DocumentId { get; }

    public string Title { get; }

    public string? ToolFamily { get; }

    public string Revision { get; }

    public string Heading { get; }

    public string Text { get; }

    public float[] Embedding { get; }
}

/// <summary>
/// Hybrid ranker: BM25 over the chunk text plus cosine similarity on <see cref="HashEmbedding.Model"/>.
/// </summary>
public sealed class DocumentIndex
{
    private const int ExcerptLength = 480;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly Bm25 _bm25;

    private DocumentIndex(IndexedChunk[] chunks)
    {
        Chunks = chunks;
        _bm25 = Bm25.Build(chunks.Select(chunk => chunk.Text).ToArray());
    }

    public IndexedChunk[] Chunks { get; }

    public static DocumentIndex Build(IReadOnlyList<CorpusDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var chunks = new List<IndexedChunk>();
        foreach (var document in documents)
        {
            foreach (var chunk in DocumentChunker.Chunk(document))
            {
                chunks.Add(new IndexedChunk(
                    chunk.Id,
                    document.Id,
                    document.Title,
                    document.ToolFamily,
                    document.Revision,
                    chunk.Heading,
                    chunk.Text,
                    HashEmbedding.Embed(chunk.Text)));
            }
        }

        return new DocumentIndex(chunks.OrderBy(chunk => chunk.Id, StringComparer.Ordinal).ToArray());
    }

    public static DocumentIndex BuildFromDirectory(string corpusDirectory)
    {
        if (string.IsNullOrWhiteSpace(corpusDirectory) || !Directory.Exists(corpusDirectory))
        {
            throw new DirectoryNotFoundException($"Corpus directory was not found: {corpusDirectory}");
        }

        var documents = Directory.GetFiles(corpusDirectory, "*.md")
            .Order(StringComparer.Ordinal)
            .Select(CorpusDocument.ParseFile)
            .ToArray();
        return Build(documents);
    }

    public static DocumentIndex Load(string path)
    {
        var file = JsonSerializer.Deserialize<IndexFile>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Empty document index {path}.");
        if (!string.Equals(file.Model, HashEmbedding.Model, StringComparison.Ordinal) || file.Dimensions != HashEmbedding.Dimensions)
        {
            throw new InvalidDataException($"Document index {path} is not {HashEmbedding.Model}.");
        }

        var chunks = file.Chunks ?? [];
        foreach (var chunk in chunks)
        {
            if (chunk.Embedding.Length != HashEmbedding.Dimensions)
            {
                throw new InvalidDataException($"Chunk {chunk.Id} does not have {HashEmbedding.Dimensions} dimensions.");
            }
        }

        return new DocumentIndex(chunks);
    }

    public void Write(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new IndexFile { Model = HashEmbedding.Model, Dimensions = HashEmbedding.Dimensions, Chunks = Chunks }, Options)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        File.WriteAllText(path, json);
    }

    public DocumentHit[] Search(string query, string? toolFamily, int limit = 8)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var family = string.IsNullOrWhiteSpace(toolFamily) ? null : toolFamily.Trim();
        var queryVector = HashEmbedding.Embed(query);
        var scored = new List<Scored>();
        for (var index = 0; index < Chunks.Length; index++)
        {
            var chunk = Chunks[index];
            if (family is not null && !string.Equals(chunk.ToolFamily, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scored.Add(new Scored(index, _bm25.Score(query, index), HashEmbedding.Dot(queryVector, chunk.Embedding)));
        }

        if (scored.Count == 0)
        {
            return [];
        }

        var max = scored.Max(item => item.Lexical);
        var min = scored.Min(item => item.Lexical);
        return scored
            .Select(item => (Item: item, Score: Hybrid(item.Lexical, item.Semantic, min, max)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => Chunks[item.Item.Index].Id, StringComparer.Ordinal)
            .Take(limit)
            .Select(item => ToHit(Chunks[item.Item.Index]))
            .ToArray();
    }

    private static double Hybrid(double lexical, float semantic, double min, double max)
    {
        var lexicalNorm = max == min ? (lexical > 0 ? 1d : 0d) : (lexical - min) / (max - min);
        var semanticNorm = (semantic + 1d) / 2d;
        return (0.45d * lexicalNorm) + (0.55d * semanticNorm);
    }

    private static DocumentHit ToHit(IndexedChunk chunk)
    {
        var excerpt = chunk.Text.Length <= ExcerptLength ? chunk.Text : chunk.Text[..ExcerptLength] + "...";
        return new DocumentHit(chunk.Id, chunk.Title, excerpt, chunk.ToolFamily, chunk.Revision);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
    }

    private sealed class IndexFile
    {
        public string? Model { get; set; }

        public int Dimensions { get; set; }

        public IndexedChunk[]? Chunks { get; set; }
    }

    private readonly record struct Scored(int Index, double Lexical, float Semantic);
}

internal sealed class Bm25
{
    private const double K1 = 1.2;

    private const double B = 0.75;

    private readonly Dictionary<string, int>[] _counts;

    private readonly int[] _lengths;

    private readonly Dictionary<string, int> _documentFrequency;

    private readonly double _averageLength;

    private Bm25(Dictionary<string, int>[] counts, int[] lengths, Dictionary<string, int> documentFrequency, double averageLength)
    {
        _counts = counts;
        _lengths = lengths;
        _documentFrequency = documentFrequency;
        _averageLength = averageLength;
    }

    public static Bm25 Build(IReadOnlyList<string> documents)
    {
        var counts = new Dictionary<string, int>[documents.Count];
        var lengths = new int[documents.Count];
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;
        for (var index = 0; index < documents.Count; index++)
        {
            var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in TextTokens.Tokenize(documents[index]))
            {
                frequencies.TryGetValue(token, out var count);
                frequencies[token] = count + 1;
            }

            counts[index] = frequencies;
            lengths[index] = frequencies.Values.Sum();
            total += lengths[index];
            foreach (var token in frequencies.Keys)
            {
                documentFrequency.TryGetValue(token, out var docs);
                documentFrequency[token] = docs + 1;
            }
        }

        var average = documents.Count == 0 || total == 0 ? 1d : total / (double)documents.Count;
        return new Bm25(counts, lengths, documentFrequency, average);
    }

    public double Score(string query, int documentIndex)
    {
        var score = 0d;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in TextTokens.Tokenize(query))
        {
            if (!seen.Add(token) || !_documentFrequency.TryGetValue(token, out var documentsWithTerm))
            {
                continue;
            }

            if (!_counts[documentIndex].TryGetValue(token, out var termFrequency) || termFrequency == 0)
            {
                continue;
            }

            var idf = Math.Log(((_counts.Length - documentsWithTerm + 0.5d) / (documentsWithTerm + 0.5d)) + 1d);
            var lengthNorm = 1d - B + (B * _lengths[documentIndex] / _averageLength);
            score += idf * (termFrequency * (K1 + 1d)) / (termFrequency + (K1 * lengthNorm));
        }

        return score;
    }
}
