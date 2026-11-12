// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.OpenSession;

/// <summary>.NET-only: <c>PublicOwnership</c> and <c>RedactOpening</c> (upstream's <c>run/public-ownership.ts</c> and <c>redactOpening</c>), which upstream's tests reach only through a runner.</summary>
public sealed class OpenHostTests
{
    [Fact]
    public async Task Holds_the_process_for_a_host_from_before_the_config_loads_until_it_closes()
    {
        var before = PublicOwnership.OpenHosts;
        var missing = Directory.CreateTempSubdirectory("e2e-open-host-").FullName;
        try
        {
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => E2EHost.OpenAsync(new OpenHostOptions { Cwd = missing, ConfigPath = "none.json" }));
            Assert.Equal("CONFIG_NOT_FOUND", error.Code);
            Assert.Equal(before, PublicOwnership.OpenHosts);

            var release = PublicOwnership.ReserveHostedRun();
            Assert.Equal(before + 1, PublicOwnership.OpenHosts);
            release();
            release();
            Assert.Equal(before, PublicOwnership.OpenHosts);
        }
        finally
        {
            Directory.Delete(missing, recursive: true);
        }
    }

    [Fact]
    public async Task Redacts_the_config_secrets_from_an_opening_failure_and_keeps_a_cancel_a_cancel()
    {
        const string value = "s3cr3t-token-value";
        global::E2E.OpenSession.RegisterStaticSecrets([Secret.Create("api", value)]);
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            global::E2E.OpenSession.RedactOpening<int>(() => throw new EngineException("ENGINE_FAILURE", "device said " + value, new InvalidOperationException(value))));
        Assert.DoesNotContain(value, failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        Assert.Equal("ENGINE_FAILURE", failure.Code);

        await Assert.ThrowsAsync<OperationCanceledException>(() => global::E2E.OpenSession.RedactOpening<int>(() => throw new OperationCanceledException()));
    }
}
