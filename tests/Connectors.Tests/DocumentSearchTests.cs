// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Connectors.Docs;

namespace Mps.Connectors.Tests;

public sealed class DocumentSearchTests
{
    [Fact]
    public void ChunksStayUnderTheTokenBudgetAndKeepTheHeading()
    {
        var words = string.Join(' ', Enumerable.Range(0, 250).Select(index => $"w{index:D3}"));
        var document = new CorpusDocument("long", "Long", "Etch", "A", "sop", false, "# Alpha\n" + words);
        var chunks = DocumentChunker.Chunk(document, maxTokens: 100, overlap: 20);
        chunks.Select(chunk => chunk.Id).Should().Equal("doc:long#c000", "doc:long#c001", "doc:long#c002");
        chunks.Should().OnlyContain(chunk => chunk.Text.StartsWith("# Alpha", StringComparison.Ordinal));
        chunks[0].Text.Should().Contain("w000").And.Contain("w099").And.NotContain("w100");
        chunks[1].Text.Should().Contain("w080").And.Contain("w100");
        chunks[^1].Text.Should().Contain("w249");
    }

    [Fact]
    public void HashEmbeddingIsDeterministic()
    {
        var first = HashEmbedding.Embed("Edge ring revision C");
        var second = HashEmbedding.Embed("Edge ring revision C");
        first.Should().Equal(second);
        first.Should().HaveCount(HashEmbedding.Dimensions);
        first.Should().NotEqual(HashEmbedding.Embed("showerhead pressure"));
    }

    [Fact]
    public void HybridRankPrefersTheDocumentThatUsesTheQueryTerms()
    {
        var index = DocumentIndex.Build(
        [
            new CorpusDocument("alpha", "Alpha note", "Etch", "A", "sop", false, "alpha alpha alpha chamber pressure alpha"),
            new CorpusDocument("beta", "Beta note", "Strip", "A", "sop", false, "beta beta wafer lot beta"),
        ]);
        index.Chunks.Should().OnlyContain(chunk => chunk.Embedding.Length == HashEmbedding.Dimensions);
        index.Search("alpha chamber", null)[0].DocumentId.Should().Be("doc:alpha#c000");
        index.Search("wafer lot", "Strip")[0].DocumentId.Should().Be("doc:beta#c000");
        index.Search("wafer lot", "Etch").Should().NotContain(hit => hit.DocumentId == "doc:beta#c000");
        index.Search("   ", null).Should().BeEmpty();
    }

    [Fact]
    public async Task CommittedIndexFindsTheBulletinTheCertificateTheDecoyAndTheFixture()
    {
        var committed = RepoPaths.IndexFile();
        var rebuilt = DocumentIndex.BuildFromDirectory(RepoPaths.CorpusDirectory());
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_INDEX") == "1")
        {
            rebuilt.Write(committed);
        }

        var temp = Directory.CreateTempSubdirectory("mps-index");
        try
        {
            var actualPath = Path.Combine(temp.FullName, "embeddings.json");
            rebuilt.Write(actualPath);
            var again = Path.Combine(temp.FullName, "again.json");
            DocumentIndex.BuildFromDirectory(RepoPaths.CorpusDirectory()).Write(again);
            File.ReadAllText(actualPath).Should().Be(File.ReadAllText(again));
            File.ReadAllText(actualPath).Should().Be(File.ReadAllText(committed));
        }
        finally
        {
            temp.Delete(true);
        }

        var search = new HybridDocumentSearch(committed);
        (await search.SearchAsync("edge ring revision C", "Etch", CancellationToken.None))[0].DocumentId.Should().Be("doc:sb-edge-ring#c000");
        (await search.SearchAsync("NF-3317", "Etch", CancellationToken.None))[0].DocumentId.Should().Be("doc:coa-nf-3317#c000");
        (await search.SearchAsync("9.5-12 Torr", "Deposition", CancellationToken.None))[0].DocumentId.Should().Be("doc:decoy-showerhead#c000");
        (await search.SearchAsync("ignore previous instructions", null, CancellationToken.None))[0].DocumentId.Should().Be("doc:untrusted-bay-note#c000");

        var maintenance = await search.SearchAsync("preventive maintenance", null, CancellationToken.None);
        maintenance[0].DocumentId.Should().NotBe("doc:untrusted-bay-note#c000");
        var strip = await search.SearchAsync("preventive maintenance oxygen flow", "Strip", CancellationToken.None);
        strip.Should().NotBeEmpty();
        strip.Should().OnlyContain(hit => hit.ToolFamily == "Strip");
        strip.Should().Contain(hit => hit.DocumentId == "doc:sop-s7-pm#c000");
    }
}
