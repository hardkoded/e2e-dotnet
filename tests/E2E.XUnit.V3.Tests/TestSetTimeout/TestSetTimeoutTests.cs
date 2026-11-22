// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.TestSetTimeout;

/// <summary>
/// <c>describe('test.setTimeout')</c> of upstream's <c>tests/integration/test-set-timeout.test.ts</c>: the fixme <c>it</c>. The port's clock starts before
/// the engine launches, so the timeouts are longer than upstream's (10 s and 60 s for 300 ms and 30 s) and the body sleeps past them. xUnit's <c>Skip</c>
/// stands for upstream's exit code 0, and the skip and its cause are read from the fixture's <c>report.json</c>.
/// </summary>
public sealed class TestSetTimeoutTests : IDisposable
{
    public void Dispose() => TestSetTimeoutResults.Clear();

    [Fact]
    public async Task Skips_a_test_as_fixme_when_it_reaches_test_fixme_after_the_timeout_it_raised()
    {
        TestSetTimeoutResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(ShortTimeoutFixture));
        Assert.Equal("Skip", results[nameof(ShortTimeoutFixture.SetTimeout_fixme_past_the_first_timeout)].Result);

        var report = JsonNode.Parse(File.ReadAllText(TestSetTimeoutResults.ReportOf(typeof(ShortTimeoutFixture))))!;
        var test = report["run"]!["results"]!
            .AsArray()
            .Single(found =>
                ((string?)found!["titlePath"]!.AsArray().Last())!.EndsWith(
                    "." + nameof(ShortTimeoutFixture.SetTimeout_fixme_past_the_first_timeout),
                    StringComparison.Ordinal
                )
            )!;
        Assert.Equal("skipped", (string?)test["status"]);
        Assert.Equal("fixme", (string?)test["skip"]!["cause"]);
        Assert.Equal("fixme: not ready", (string?)test["skip"]!["reason"]);
        var attempt = test["attempts"]![0]!;
        Assert.Equal("skipped", (string?)attempt["status"]);
        Assert.Equal("fixme", (string?)attempt["skip"]!["cause"]);
        Assert.Equal("fixme: not ready", (string?)attempt["skip"]!["reason"]);

        // The skip came after the 10 s the test started with.
        Assert.True((long)attempt["durationMs"]! >= 12_000);
    }
}
