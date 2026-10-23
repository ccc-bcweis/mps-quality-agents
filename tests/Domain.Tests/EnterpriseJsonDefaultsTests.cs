// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Domain.Identity;
using Mps.Domain.Json;

namespace Mps.Domain.Tests;

public sealed class EnterpriseJsonDefaultsTests
{
    private sealed record Sample(ToolId ToolId, DateTimeOffset StartedAt, string? Note);

    [Fact]
    public void RecordsUseCamelCaseToolIdsAndUtcTimestamps()
    {
        var at = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.FromHours(-4));
        var options = EnterpriseJsonDefaults.Create();
        var json = JsonSerializer.Serialize(new Sample(new ToolId("E-02"), at, null), options);

        json.Should().Be("{\"toolId\":\"E-02\",\"startedAt\":\"2026-10-14T15:00:00Z\",\"note\":null}");
        json.Should().NotContain("ToolId");
        json.Should().NotContain("\\u003C");

        var tagged = JsonSerializer.Serialize(new Sample(new ToolId("E-02"), at, "<tag>"), options);
        tagged.Should().Contain("\"note\":\"<tag>\"");
        tagged.Should().NotContain("\\u003C");

        var indented = JsonSerializer.Serialize(new Sample(new ToolId("E-02"), at, "note"), EnterpriseJsonDefaults.Create(writeIndented: true));
        indented.Should().Contain("\n");
        indented.Should().Contain("\"toolId\": \"E-02\"");

        JsonSerializer.Serialize(new Sample(new ToolId("E-02"), at, "note"), EnterpriseJsonDefaults.Create()).Should().NotContain("\n");
    }

    [Fact]
    public void ReadsAreCaseInsensitiveAndToolIdsAreTrimmed()
    {
        var options = EnterpriseJsonDefaults.Create();
        var sample = JsonSerializer.Deserialize<Sample>(
            "{\"TOOLID\":\"  E-02  \",\"StartedAt\":\"2026-10-14T15:00:00Z\",\"note\":\"kept\"}",
            options);

        sample.Should().NotBeNull();
        sample!.ToolId.Value.Should().Be("E-02");
        sample.StartedAt.Should().Be(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero));
        sample.StartedAt.Offset.Should().Be(TimeSpan.Zero);
        sample.Note.Should().Be("kept");

        var zoned = JsonSerializer.Deserialize<Sample>(
            "{\"toolId\":\"E-02\",\"startedAt\":\"2026-10-14T11:00:00-04:00\"}",
            options);
        zoned!.StartedAt.UtcDateTime.Should().Be(new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ABlankToolIdOrTimestampIsRejected()
    {
        var options = EnterpriseJsonDefaults.Create();
        Rejects("{\"toolId\":\"  \",\"startedAt\":\"2026-10-14T15:00:00Z\"}", options);
        Rejects("{\"toolId\":null,\"startedAt\":\"2026-10-14T15:00:00Z\"}", options);
        Rejects("{\"toolId\":\"E-02\",\"startedAt\":\"\"}", options);
        Rejects("{\"toolId\":\"E-02\",\"startedAt\":\"   \"}", options);
        Rejects("{\"toolId\":\"E-02\",\"startedAt\":null}", options);

        var apply = () => EnterpriseJsonDefaults.Apply(null!);
        apply.Should().Throw<ArgumentNullException>();
    }

    private static void Rejects(string json, JsonSerializerOptions options)
    {
        var act = () => JsonSerializer.Deserialize<Sample>(json, options);
        act.Should().Throw<JsonException>();
    }
}
