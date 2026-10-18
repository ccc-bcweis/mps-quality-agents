// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Mps.Api.Access;

using Xunit;

namespace Mps.Api.Tests;

public sealed class AuthStartupTests
{
    [Fact]
    public void EntraModeWithNoAdminRefusesToStart()
    {
        var act = () => AuthStartup.Validate(Config(("Mps:Auth:Mode", "azure")), new TestEnv(Environments.Production), NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no admin is configured*ADMIN_EMAILS*");
    }

    [Fact]
    public void EntraAliasIsTheSameModeAndAlsoNeedsAnAdmin()
    {
        var act = () => AuthStartup.Validate(Config(("Mps:Auth:Mode", "entra")), new TestEnv(Environments.Production), NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Mps:Auth:AdminEmails")]
    [InlineData("ADMIN_EMAILS")]
    public void EntraModeStartsWithAdminsFromEitherSetting(string key)
    {
        var configuration = Config(("Mps:Auth:Mode", "azure"), (key, "owner@example.com; second@example.com"));

        AuthStartup.Validate(configuration, new TestEnv(Environments.Production), NullLogger.Instance);

        var options = AccessOptions.From(configuration);
        options.AdminEmails.Should().Equal("owner@example.com", "second@example.com");
        options.IsAdminEmail("OWNER@example.com").Should().BeTrue();
    }

    [Fact]
    public void NoAdminIsBuiltIn()
    {
        AccessOptions.From(Config()).AdminEmails.Should().BeEmpty();
        typeof(AccessOptions).GetFields().Select(field => field.GetRawConstantValue()?.ToString() ?? string.Empty)
            .Should().NotContain(value => value.Contains('@', StringComparison.Ordinal));
    }

    [Fact]
    public void LocalModeNeedsNoAdmin()
    {
        var act = () => AuthStartup.Validate(Config(), new TestEnv(Environments.Development), NullLogger.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TheHostRefusesToStartInEntraModeWithoutAnAdmin()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Auth:Mode", "azure"));

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*no admin is configured*");
    }

    [Theory]
    [InlineData("http://localhost:8080", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("http://[::1]:8080", true)]
    [InlineData("http://+:8080", false)]
    [InlineData("http://*:8080", false)]
    [InlineData("http://0.0.0.0:8080", false)]
    [InlineData("http://[::]:8080", false)]
    [InlineData("http://192.168.1.20:8080", false)]
    public void LoopbackDetection(string address, bool loopback)
    {
        AuthStartup.IsLoopback(address).Should().Be(loopback);
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();

    private sealed class TestEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Mps.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
