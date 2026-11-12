// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCache;

/// <summary>
/// <c>describe('e2e cache configuration')</c> of upstream <c>tests/unit/cli-cache.test.ts</c>. Not ported: "refuses a custom store it cannot
/// enumerate". The port's config file cannot name a <c>cache.store</c> (code sets it with <c>WithCacheStore</c>), so the CLI never sees one.
/// </summary>
public sealed class E2eCacheConfigurationTests : IDisposable
{
    private readonly CacheCli _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public void Fails_with_the_config_error_when_the_config_is_missing()
    {
        File.Delete(Path.Combine(_cli.Root, "e2e.config.json"));
        _cli.Invoke("ls");
        Assert.Contains("config file not found", _cli.Stderr, StringComparison.Ordinal);
        Assert.Equal(2, _cli.ExitCode);
    }
}
