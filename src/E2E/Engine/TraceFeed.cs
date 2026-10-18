// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// What the web engine tells the harness for an attempt's trace, apart from the app log: the screen an action left
/// (<see cref="EngineStartOptions.Screen"/>), and the browser and user agent the attempt runs on
/// (<see cref="EngineStartOptions.Environment"/>). One feed per attempt; it never fails what the test does. Upstream's
/// <c>TraceFeed</c> (<c>web/src/trace-feed.ts</c>).
/// </summary>
internal sealed class TraceFeed
{
    /// <summary>How long the screen an action left may take for the trace before the step goes without one.</summary>
    internal static readonly TimeSpan TraceScreenTimeout = TimeSpan.FromSeconds(1);

    /// <summary>How long the page may take to report its user agent before the trace names the browser alone.</summary>
    internal static readonly TimeSpan EnvironmentTimeout = TimeSpan.FromSeconds(1);

    private readonly Action<Observation>? _screen;
    private readonly Action<IReadOnlyDictionary<string, string>>? _environment;
    private readonly Func<IPage?> _page;
    private readonly Func<OperationBudget, Task<Observation>> _capture;
    private readonly TimeProvider _clock;

    // Whether the feed told the harness what the attempt runs on: 1 once it did.
    private int _environmentTold;

    /// <param name="screen">The harness's sink for the screen an action left; null when the attempt keeps no trace.</param>
    /// <param name="environment">The harness's sink for what the attempt runs on.</param>
    /// <param name="page">Reads the attempt's open page without touching the error latch.</param>
    /// <param name="capture">
    /// Captures the page like an observation, but never publishes it, so every ref a test or model holds stays valid.
    /// Upstream's <c>captureObservation</c> against the ref registry, which it then lets go.
    /// </param>
    /// <param name="clock">The clock the budgets run on.</param>
    public TraceFeed(
        Action<Observation>? screen,
        Action<IReadOnlyDictionary<string, string>>? environment,
        Func<IPage?> page,
        Func<OperationBudget, Task<Observation>> capture,
        TimeProvider clock)
    {
        _screen = screen;
        _environment = environment;
        _page = page;
        _capture = capture;
        _clock = clock;
    }

    /// <summary>
    /// Tells the harness, once an attempt, the browser and the user agent its page reports. Best effort and not awaited: nothing the
    /// test does waits on it.
    /// </summary>
    public void TellEnvironment()
    {
        if (_environment is not { } sink || Volatile.Read(ref _environmentTold) == 1)
        {
            return;
        }

        _ = TellAsync(sink);
    }

    private async Task TellAsync(Action<IReadOnlyDictionary<string, string>> sink)
    {
        var facts = await EnvironmentFactsAsync().ConfigureAwait(false);
        try
        {
            if (facts is not null)
            {
                sink(facts);
            }
        }
#pragma warning disable CA1031 // The harness's sink never fails the attempt.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    // The browser and the user agent the page reports, within a second; null without a page or a browser to ask.
    private async Task<IReadOnlyDictionary<string, string>?> EnvironmentFactsAsync()
    {
#pragma warning disable CA1031 // The trace is a reader: a page that cannot answer leaves it without facts, and never fails the test.
        try
        {
            var page = _page();
            if (page is null || Interlocked.Exchange(ref _environmentTold, 1) == 1)
            {
                return null;
            }

            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (page.Context.Browser is { } browser)
            {
                facts["browser"] = $"{browser.BrowserType.Name} {browser.Version}";
            }

            try
            {
                var budget = new OperationBudget(EnvironmentTimeout, "user agent", CancellationToken.None, _clock);
                facts["user agent"] = await budget.WithinAsync(page.EvaluateAsync<string>("navigator.userAgent")).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The browser alone.
            }

            return facts;
        }
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Hands the harness the screen an action left. The read is on the action's path, as the <see cref="EngineStartOptions.Screen"/>
    /// contract allows, and only when the attempt keeps a trace (the harness hands no sink otherwise). The screen is captured like an
    /// observation but never published, so every ref a test or model holds stays valid; a page that cannot answer within a second
    /// leaves the step without one.
    /// </summary>
    public async Task ScreenAfterActionAsync(CancellationToken cancellationToken)
    {
        if (_screen is not { } sink || cancellationToken.IsCancellationRequested || _page() is null)
        {
            return;
        }

#pragma warning disable CA1031 // A screen the page could not give in time is no evidence the trace claims.
        try
        {
            var budget = new OperationBudget(TraceScreenTimeout, "trace screen", cancellationToken, _clock);
            sink(await _capture(budget).ConfigureAwait(false));
        }
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }
}
