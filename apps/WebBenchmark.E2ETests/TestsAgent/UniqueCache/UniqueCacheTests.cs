// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark.E2ETests.TestsAgent.UniqueCache;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests-agent/unique-cache.e2e.ts</c> (no <c>describe</c>). The label typed in is different
/// on every run and appears in the list afterwards. Marked as <c>Values.Unique</c>, the step keys and records a slot for the label, so a
/// second run replays it with a fresh label and no model call; unmarked, the value is in the key and every run misses.
/// </summary>
public sealed class UniqueCacheTests : TestsAgentTest
{
    [Test]
    public async Task Act_adds_an_address_with_a_run_unique_label()
    {
        var label = $"Cabin {Base36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())}";
        await App.OpenAsync("/e/saved-addresses");
        await Agent.ActAsync("add a new address labelled {label} at {line}", new ActOptions
        {
            Params = new Dictionary<string, object?>
            {
                ["label"] = Values.Unique(label),
                ["line"] = "7 Pine Way, Bend",
            },
        });
        await Expect.That(Screen.GetByTestId("address-list")).ToContainTextAsync(label);
        await Expect.That(Screen.GetByTestId("toast")).ToHaveTextAsync("Address saved");
    }

    /// <summary>The value in base 36, as upstream's <c>Date.now().toString(36)</c> writes it.</summary>
    private static string Base36(long value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var text = string.Empty;
        do
        {
            text = digits[(int)(value % 36)] + text;
            value /= 36;
        }
        while (value > 0);
        return text;
    }
}
