// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Select;
using static E2E.Tests.Select.SelectKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>
/// Upstream's <c>describe('filterSelection')</c> (<c>tests/unit/test-filters.test.ts</c>). Every <c>it</c> is partial: it builds its collection
/// by hand where upstream collects <c>test()</c> declarations with <c>collectModule</c>, and its config by hand where upstream calls
/// <c>resolveConfig</c>.
/// </summary>
public sealed class FilterSelectionTests
{
    /// <summary>What <c>FilterSelection</c> is handed for the collection under the config, judging a ref git cannot measure from, so a lookup would throw; and the notices it wrote.</summary>
    private static (FilterSelectionOptions Options, List<string> Notices) OptionsOf(Collection col, ResolvedConfig cfg, SelectionFilters? selectionFilters = null, CancellationToken cancellationToken = default)
    {
        var notices = new List<string>();
        return (new FilterSelectionOptions
        {
            Config = cfg,
            Collection = col,
            SelectionFilters = selectionFilters ?? new SelectionFilters(),
            ChangedSince = "no-such-ref",
            CancellationToken = cancellationToken,
            Notice = notices.Add,
        }, notices);
    }

    [Fact]
    public async Task Partial_Keeps_the_selection_it_was_handed_when_the_config_has_no_filter_warning_unless_interrupted_and_asks_no_git()
    {
        var col = CollectionOf("tests/a.e2e.ts", new Decl("a"));
        var cfg = ConfigOf();
        var first = Selector.Select(col, cfg);
        var (options, notices) = OptionsOf(col, cfg);
        var kept = await Filters.FilterSelection(first, options);
        Assert.Empty(kept.Records);
        Assert.Null(kept.Changes);
        Assert.Same(first, kept.Selection);
        Assert.Equal(["warning: --changed-since: the config has no filters, so every test runs"], notices);

        using var interrupt = new CancellationTokenSource();
        await interrupt.CancelAsync();
        var (interrupted, interruptedNotices) = OptionsOf(col, cfg, cancellationToken: interrupt.Token);
        Assert.Same(first, (await Filters.FilterSelection(first, interrupted)).Selection);
        Assert.Empty(interruptedNotices);
    }

    [Fact]
    public async Task Partial_Keeps_the_selection_it_was_handed_when_the_gate_calls_no_filter_recording_why()
    {
        var col = CollectionOf("tests/a.e2e.ts", new Decl("a"));
        var calls = 0;
        var never = FilterOf.Of("areas", _ =>
        {
            calls++;
            return new TestFilterResult { Exclude = new Dictionary<string, string>() };
        });
        var cfg = ConfigOf([never]);
        var first = Selector.Select(col, cfg);
        var (options, notices) = OptionsOf(col, cfg, new SelectionFilters { LastFailed = new HashSet<string>() });
        var kept = await Filters.FilterSelection(first, options);
        Assert.Empty(kept.Records);
        Assert.Equal(new ReportChanges(Skipped: "--last-failed"), kept.Changes);
        Assert.Same(first, kept.Selection);
        Assert.Equal(["--last-failed: the tests the last run owes run without filters"], notices);
        Assert.Equal(0, calls);
    }
}
