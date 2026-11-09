// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>The suite's base class: <see cref="E2ETest"/> against the benchmark app, which <see cref="BenchmarkSite"/> serves.</summary>
public abstract class BenchmarkTest : E2ETest
{
    protected override string? BaseUrl => BenchmarkSite.Url;

    /// <summary>The Login Form scenario's hardcoded account, which <c>Credentials.User("benchmark")</c> reads; upstream declares it in <c>e2e.config.ts</c>.</summary>
    [OneTimeSetUp]
    public void DeclareTheBenchmarkAccount()
    {
        // Set unconditionally: the page hardcodes this account, so an ambient value would make the valid sign-in fail.
        Environment.SetEnvironmentVariable("E2E_USER_BENCHMARK_USERNAME", "tester@tester.army");
        Environment.SetEnvironmentVariable("E2E_USER_BENCHMARK_PASSWORD", "benchmark123");
    }
}
