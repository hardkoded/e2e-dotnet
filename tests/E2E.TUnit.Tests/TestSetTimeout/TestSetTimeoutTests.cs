// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.TUnit.Tests.EngineContract;

namespace E2E.TUnit.Tests.TestSetTimeout;

/// <summary>
/// <c>describe('test.setTimeout')</c> of upstream's <c>tests/integration/test-set-timeout.test.ts</c>: the fixme <c>it</c>. The port's clock starts before
/// the engine launches, so the timeouts are longer than upstream's (10 s and 60 s for 300 ms and 30 s) and the body sleeps past them. TUnit's <c>Skipped</c>
/// stands for upstream's exit code 0, and the skip and its cause are read from the fixture's <c>report.json</c>.
/// </summary>
public sealed class TestSetTimeoutTests
{
    [After(Class)]
    public static void RemoveResults() => TestSetTimeoutResults.Clear();

    [Test]
    public async Task Skips_a_test_as_fixme_when_it_reaches_test_fixme_after_the_timeout_it_raised()
    {
        TestSetTimeoutResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(ShortTimeoutFixture));
        await Assert
            .That(results[nameof(ShortTimeoutFixture.SetTimeout_fixme_past_the_first_timeout)].Result)
            .IsEqualTo("Skipped");

        var report = JsonNode.Parse(File.ReadAllText(TestSetTimeoutResults.ReportOf(typeof(ShortTimeoutFixture))))!;
        var test = report["run"]!["results"]!
            .AsArray()
            .Single(found =>
                ((string?)found!["titlePath"]!.AsArray().Last())!.EndsWith(
                    "." + nameof(ShortTimeoutFixture.SetTimeout_fixme_past_the_first_timeout),
                    StringComparison.Ordinal
                )
            )!;
        await Assert.That((string?)test["status"]).IsEqualTo("skipped");
        await Assert.That((string?)test["skip"]!["cause"]).IsEqualTo("fixme");
        await Assert.That((string?)test["skip"]!["reason"]).IsEqualTo("fixme: not ready");
        var attempt = test["attempts"]![0]!;
        await Assert.That((string?)attempt["status"]).IsEqualTo("skipped");
        await Assert.That((string?)attempt["skip"]!["cause"]).IsEqualTo("fixme");
        await Assert.That((string?)attempt["skip"]!["reason"]).IsEqualTo("fixme: not ready");

        // The skip came after the 10 s the test started with.
        await Assert.That((long)attempt["durationMs"]!).IsGreaterThanOrEqualTo(12_000);
    }
}
