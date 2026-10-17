// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Mps.Api.Hosting;

using Xunit;

namespace Mps.Api.Tests;

public sealed class ApprovalSigningKeyTests
{
    private static readonly string ValidHex = new('a', ApprovalPolicyLoader.MinimumKeyBytes * 2);

    [Fact]
    public void AConfiguredHexKeyIsUsedAsIs()
    {
        var key = ApprovalPolicyLoader.SigningKey(ValidHex, new Env(Environments.Production), NullLogger.Instance);

        key.Should().HaveCount(ApprovalPolicyLoader.MinimumKeyBytes).And.OnlyContain(value => value == 0xAA);
    }

    [Fact]
    public void ProductionWithoutAKeyRefusesToStart()
    {
        var act = () => ApprovalPolicyLoader.SigningKey(null, new Env(Environments.Production), NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Mps:Approval:SigningKey is not set*");
    }

    [Fact]
    public void DevelopmentWithoutAKeyUsesARandomKeyPerProcessNeverTheOldPublicBytes()
    {
        var first = ApprovalPolicyLoader.SigningKey(" ", new Env(Environments.Development), NullLogger.Instance);
        var second = ApprovalPolicyLoader.SigningKey(null, new Env(Environments.Development), NullLogger.Instance);

        first.Should().HaveCount(ApprovalPolicyLoader.MinimumKeyBytes);
        first.Should().NotEqual(second);
        first.Should().NotEqual(Enumerable.Range(1, 16).Select(value => (byte)value));
    }

    [Theory]
    [InlineData("not-hex-at-all")]
    [InlineData("0102030405060708090a0b0c0d0e0f10")]
    public void AMalformedOrShortKeyIsRejected(string configured)
    {
        var act = () => ApprovalPolicyLoader.SigningKey(configured, new Env(Environments.Development), NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LoadReadsTheOneSettingAndTheLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApprovalPolicyLoader.SigningKeySetting] = ValidHex,
                ["Approval:LifetimeMinutes"] = "7",
            })
            .Build();

        var policy = ApprovalPolicyLoader.Load(configuration, new Env(Environments.Production), NullLogger.Instance);

        policy.SigningKey.ToArray().Should().OnlyContain(value => value == 0xAA);
        policy.Lifetime.Should().Be(TimeSpan.FromMinutes(7));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Mps.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
