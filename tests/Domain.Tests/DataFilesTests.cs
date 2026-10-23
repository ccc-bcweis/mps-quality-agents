// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain.Tests;

public sealed class DataFilesTests
{
    [Fact]
    public void AMissingFileIsNotInvented()
    {
        using var scope = new DataDirectoryScope(null);

        DataFiles.Find("domain-tests/does-not-exist-413.txt").Should().BeNull();
    }

    [Fact]
    public void TheConfiguredDirectoryWinsWhenTheFileIsThere()
    {
        var root = NewRoot();
        var relative = Path.Combine("notes", "marker.txt");
        var expected = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllText(expected, "marker");

        try
        {
            using var scope = new DataDirectoryScope(root);

            DataFiles.Find(relative).Should().Be(expected);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AConfiguredDirectoryWithoutTheFileFallsThroughToTheRepoDataFolder()
    {
        var root = NewRoot();
        try
        {
            using var scope = new DataDirectoryScope(root);

            var found = DataFiles.Find("glossary.yaml");

            found.Should().NotBeNull();
            File.Exists(found).Should().BeTrue();
            found.Should().EndWith(Path.Combine("data", "glossary.yaml"));
            found.Should().NotStartWith(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WhitespaceConfigurationIsIgnored()
    {
        using var scope = new DataDirectoryScope("   ");

        DataFiles.Find("domain-tests/does-not-exist-413.txt").Should().BeNull();
        DataFiles.Find(Path.Combine("seed", "README.md")).Should().EndWith(Path.Combine("data", "seed", "README.md"));
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mps-domain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class DataDirectoryScope : IDisposable
    {
        private const string Variable = "Mps__DataDirectory";

        private readonly string? _previous;

        public DataDirectoryScope(string? value)
        {
            _previous = Environment.GetEnvironmentVariable(Variable);
            Environment.SetEnvironmentVariable(Variable, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(Variable, _previous);
        }
    }
}
