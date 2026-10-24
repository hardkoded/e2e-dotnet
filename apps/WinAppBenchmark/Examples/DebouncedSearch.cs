// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>Debounced input plus async results: the results render 400 ms after the last keystroke and 800 ms of "fetch".</summary>
public sealed class DebouncedSearch : Component
{
    private const int DebounceMilliseconds = 400;
    private const int FetchMilliseconds = 800;
    private const string Target = "Benchmark Target";

    private static readonly string[] Dataset =
    [
        "Alpha Report",
        "Benchmark Target",
        "Benchmark Baseline",
        "Beta Rollout",
        "Bench Press Guide",
        "Charlie Checklist",
        "Delta Dashboard",
        "Echo Environment",
    ];

    /// <summary>Filters the deterministic dataset with a case-insensitive substring match, mimicking a backend search endpoint.</summary>
    private static IReadOnlyList<string> SearchDataset(string query)
    {
        var normalized = query.Trim();
        return normalized.Length == 0
            ? []
            : [.. Dataset.Where(item => item.Contains(normalized, StringComparison.OrdinalIgnoreCase))];
    }

    public override Element Render()
    {
        var (query, setQuery) = UseState("");
        var (results, setResults) = UseState<IReadOnlyList<string>>([]);
        var (searching, setSearching) = UseState(false);
        var (selected, setSelected) = UseState<string?>(null);

        UseEffect(() =>
        {
            var cts = new CancellationTokenSource();
            After(DebounceMilliseconds, cts.Token, () =>
            {
                if (query.Trim().Length == 0)
                {
                    setResults([]);
                    setSearching(false);
                    return;
                }

                setSearching(true);
                After(FetchMilliseconds, cts.Token, () =>
                {
                    setResults(SearchDataset(query));
                    setSearching(false);
                });
            });
            return () => cts.Cancel();
        }, query);

        if (selected == Target)
        {
            return VStack(Success($"Selected {Target}")).Padding(24);
        }

        Element list = searching
            ? ProgressRing().AutomationId("search-loading").Padding(12)
            : ScrollView(VStack(4, [
                .. results.Select(item => Button(TextBlock(item), () => setSelected(item))
                    .AutomationId($"result-{item}")
                    .HAlign(HorizontalAlignment.Stretch)
                    .WithKey(item)),
                When(results.Count == 0 && query.Trim().Length > 0, () => Hint("No results")),
            ])).AutomationName("Results").AutomationId("search-results");

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star()],
            VStack(12,
                Hint($"Search for and select \"{Target}\""),
                TextBox(query, setQuery, placeholderText: "Search…").AutomationName("Search").AutomationId("search-input"))
                .Padding(24, 16),
            list.Padding(24, 0).Grid(row: 1));
    }
}
