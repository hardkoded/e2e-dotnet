// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>
/// Upstream's <c>describe('traces')</c> (<c>tests/integration/traces.test.ts</c>), driven through a session over an engine that reports
/// its screens. The port has no runner, so a test's attempts are sessions with the attempt number set.
/// </summary>
public sealed class TracesTests : IDisposable
{
    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    // Partial: upstream also reads the page from the run's last event, which the port has no run to emit.
    [Fact]
    public async Task Keeps_each_steps_events_and_files_what_the_app_logged_under_the_step_it_happened_in_on_the_page()
    {
        var results = Results();
        const string title = "save > saves, then expects two buttons";
        await using (var session = await StartAsync(results, title, engine: new ReportingEngine(logs: true)))
        {
            // The suite opens the app in a beforeEach hook.
            session.Phase = StepPhase.BeforeEach;
            await session.App.OpenAsync("/");
            session.Phase = null;
            await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();
            var failure = await Assert.ThrowsAnyAsync<E2EException>(() => Expect.That(session.Screen.GetByRole("button")).ToHaveCountAsync(2, TimeSpan.FromMilliseconds(300)));

            var (open, tap, count) = (session.Steps[0], session.Steps[1], session.Steps[2]);
            Assert.Equal(("app.open", StepPhase.BeforeEach), (open.Api, open.Phase));
            Assert.Empty(open.Events);
            Assert.Null(tap.Phase);
            var acted = Assert.Single(tap.Events);
            Assert.Equal((StepEventKind.Engine, "tap", StepEventStatus.Passed, "tap button \"Submit\""), (acted.Kind, acted.Name, acted.Status, acted.Detail));

            // Launched before any step, the warning belongs to none; the failed request to the tap that sent it.
            Assert.Equal(
                [
                    (AppLogSource.Console, AppLogLevel.Warning, "booting", (int?)null),
                    (AppLogSource.Network, AppLogLevel.Error, "POST http://127.0.0.1:4100/api/save 500 Internal Server Error", tap.Index),
                ],
                session.AppLog.Select(entry => (entry.Source, entry.Level, entry.Text, entry.Step)));
            var polled = Assert.Single(count.Events);
            Assert.Equal((StepEventKind.Poll, "expect", StepEventStatus.Failed), (polled.Kind, polled.Name, polled.Status));
            Assert.Matches(@"^count 1 \(1 match\) x\d+$", polled.Detail!);
            Assert.True(polled.Count > 1);
            session.Complete(failure);
        }

        var page = ReadTrace(results, title)!;
        Assert.Contains("1. ✓ `app.open` `/` (", page, StringComparison.Ordinal);
        Assert.Contains("in beforeEach)", page, StringComparison.Ordinal);
        Assert.Contains("   - ✗ network error: `POST /api/save 500 Internal Server Error`", page, StringComparison.Ordinal);
        Assert.Contains("- before step 1 · ⚠ console warning: `booting`", page, StringComparison.Ordinal);
        Assert.Contains("   - tap button \"Submit\" (", page, StringComparison.Ordinal);
        Assert.Matches(@" {3}- expect gave up after \d+ reads in [\d.]+m?s: count 1 \(1 match\) x\d+", page);

        // The page quotes the last screen the test saw, as a file beside the attempt's others.
        Assert.Contains("3. ✗ `expect.toHaveCount` ", page, StringComparison.Ordinal);
        Assert.Contains("**ASSERTION_FAILED**", page, StringComparison.Ordinal);
        Assert.Contains("The screen as the agent reads it", page, StringComparison.Ordinal);
        Assert.Contains("button \"Submit\"", page[page.IndexOf("```text\n#", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("- [screen](attempt-1/screen-at-failure.txt) `", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_screen_each_step_left_and_what_the_engine_said_the_attempt_ran_on_in_the_report_and_on_the_page()
    {
        var results = Results();
        const string title = "traces > saves";
        await using (var session = await StartAsync(results, title, RecordingMode.On, engine: new ReportingEngine()))
        {
            await session.App.OpenAsync("/");
            await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();
            await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();

            Assert.Equal(new Dictionary<string, string> { ["device"] = "Pixel 9", ["os"] = "Android 16" }, session.Environment);
            var steps = session.Steps;
            var first = steps[1].Screen!;
            var second = steps[2].Screen!;
            Assert.Equal(2, first.Nodes);
            Assert.Empty(first.Changes);
            Assert.Equal(1, second.Since);
            Assert.Equal(["added button \"Undo\""], second.Changes);
            session.Complete();
        }

        var page = ReadTrace(results, title)!;
        Assert.Contains("Ran on: device `Pixel 9` · os `Android 16`", page, StringComparison.Ordinal);
        Assert.Contains("   - screen: 1 change since step 2 at `/`, 3 nodes\n     - `added button \"Undo\"`", page, StringComparison.Ordinal);

        // An attempt that keeps no trace asks the engine for no screens, and takes none from what the test observes.
        var untraced = new ReportingEngine();
        await using var off = await StartAsync(results, "traces > saves, untraced", RecordingMode.Off, engine: untraced);
        await off.App.OpenAsync("/");
        await off.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();

        Assert.False(untraced.AskedForScreens);
        Assert.NotEmpty(off.Steps);
        Assert.All(off.Steps, step => Assert.Null(step.Screen));
    }

    // Partial: upstream also reads the lead of the run's summary, which the port has no reporter for.
    [Fact]
    public async Task Names_the_step_whose_error_ended_the_test_on_the_page_not_an_earlier_failure_the_test_caught()
    {
        var results = Results();
        const string title = "banner > tolerates a missing banner, then expects two buttons";
        E2EException ended;
        StepRecord waitFor;
        StepRecord count;
        await using (var session = await StartAsync(results, title))
        {
            await session.App.OpenAsync("/");
            try
            {
                await session.Screen.GetByRole("button", new() { Name = "Banner" }).WaitForAsync(new() { Timeout = TimeSpan.FromMilliseconds(100) });
            }
            catch (E2EException)
            {
                // the banner is optional
            }

            await session.Screen.GetByRole("button", new() { Name = "Submit" }).TapAsync();
            ended = await Assert.ThrowsAnyAsync<E2EException>(() => Expect.That(session.Screen.GetByRole("button")).ToHaveCountAsync(2, TimeSpan.FromMilliseconds(300)));
            Assert.Equal("ASSERTION_FAILED", ended.Code);
            Assert.Equal(
                [("app.open", StepStatus.Passed), ("locator.waitFor", StepStatus.Failed), ("locator.tap", StepStatus.Passed), ("expect.toHaveCount", StepStatus.Failed)],
                session.Steps.Select(step => (step.Api, step.Status)));
            waitFor = session.Steps[1];
            count = session.Steps[3];
            session.Complete(ended);
        }

        // The line and the lead are the assertion's; the caught step is listed, not named.
        Assert.NotEqual(waitFor.Source!.Line, count.Source!.Line);
        var page = ReadTrace(results, title)!;
        Assert.Contains($"Look at: `{count.Source.File}:{count.Source.Line}`", page, StringComparison.Ordinal);
        Assert.Contains("2. ✗ `locator.waitFor`", page, StringComparison.Ordinal);
        Assert.Contains("4. ✗ `expect.toHaveCount`", page, StringComparison.Ordinal);
    }

    // Partial: upstream reads the lead of the run's summary too, which the port has no reporter for.
    [Fact]
    public async Task Names_the_step_of_a_call_split_across_lines_whose_error_unwinds_through_the_line_above_its_call_site()
    {
        var results = Results();
        await using var session = await StartAsync(results, "split > split call");
        await session.App.OpenAsync("/");
        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => Expect.That(session.Screen.GetByRole("button", new() { Name = "Submit" }))
            .ToHaveCountAsync(2, TimeSpan.FromMilliseconds(300)));

        var count = session.Steps[1];
        var error = ErrorSerializer.Serialize(failure, ProjectRoot());
        Assert.Equal(error.Source, count.Error?.Source);
        Assert.NotEqual(error.Source?.Line, count.Source?.Line);
        session.Complete(failure);
    }

    [Fact]
    public async Task Writes_no_page_for_a_passing_run_by_default()
    {
        var results = Results();
        await using (var session = await StartAsync(results, "save > saves"))
        {
            await session.App.OpenAsync("/");
            await Expect.That(session.Screen.GetByRole("button")).ToHaveCountAsync(1, TimeSpan.FromMilliseconds(300));
            session.Complete();
        }

        Assert.Null(ReadTrace(results, "save > saves"));
    }

    [Fact]
    public async Task Clears_the_page_an_earlier_run_left_when_the_first_attempt_keeps_none()
    {
        var results = Results();
        await using (var failing = await StartAsync(results, "stale > test"))
        {
            await failing.App.OpenAsync("/");
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        Assert.NotNull(ReadTrace(results, "stale > test"));

        await using (var passing = await StartAsync(results, "stale > test"))
        {
            await passing.App.OpenAsync("/");
            passing.Complete();
        }

        Assert.Null(ReadTrace(results, "stale > test"));

        await using (var failing = await StartAsync(results, "stale > test"))
        {
            await failing.App.OpenAsync("/");
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        await using (var off = await StartAsync(results, "stale > test", RecordingMode.Off))
        {
            await off.App.OpenAsync("/");
            off.Complete(new TestException("TEST_FAILED", "nope"));
        }

        Assert.Null(ReadTrace(results, "stale > test"));
    }

    // .NET-only: upstream's runner keeps a test's attempts in one record, so its page tells them all. Here each attempt is a session.
    [Fact]
    public async Task Tells_a_flaky_test_on_one_page_across_its_attempts()
    {
        var results = Results();
        await using (var failing = await StartAsync(results, "flaky > test"))
        {
            await failing.App.OpenAsync("/failed-attempt");
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        await using (var passing = await StartAsync(results, "flaky > test", attempt: 2))
        {
            await passing.App.OpenAsync("/");
            passing.Complete();
        }

        var page = ReadTrace(results, "flaky > test");
        Assert.NotNull(page);
        // The page tells the failed attempt, which only the first attempt opened.
        Assert.Contains("/failed-attempt", page, StringComparison.Ordinal);
        Assert.Contains("Passed after 1 failed attempt", page, StringComparison.Ordinal);
        Assert.Matches(@"\n\n`[^\n]+` · flaky · ", page);
    }

    [Fact]
    public async Task Tells_a_test_that_failed_every_attempt_as_failed_the_same_way()
    {
        var results = Results();
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await using var session = await StartAsync(results, "twice > test", attempt: attempt);
            await session.App.OpenAsync("/");
            session.Complete(new TestException("TEST_FAILED", "nope"));
        }

        Assert.Contains("Failed the same way on both attempts: **TEST_FAILED**", ReadTrace(results, "twice > test"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_page_of_an_earlier_attempt_when_the_retry_that_follows_keeps_none()
    {
        var results = Results();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var session = await StartAsync(results, "kept > test", RecordingMode.OnFirstRetry, attempt);
            await session.App.OpenAsync("/");
            session.Complete(attempt < 3 ? new TestException("TEST_FAILED", "nope") : null);
        }

        // Under on-first-retry only the second attempt keeps a trace; the third passes and keeps none, yet the page tells all three.
        Assert.Contains("Passed after 2 failed attempts", ReadTrace(results, "kept > test"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_traces_the_mode_asks_for_none_under_off_a_passing_test_under_on_a_retry_under_trace_on_first_retry()
    {
        Assert.Equal([], await TracedAsync(RecordingMode.Off, retries: 0));
        Assert.Equal(["fails", "passes"], await TracedAsync(RecordingMode.On, retries: 0));
        Assert.Equal(["fails"], await TracedAsync(RecordingMode.OnFirstRetry, retries: 1));
        Assert.Equal([], await TracedAsync(RecordingMode.OnFirstRetry, retries: 0));
    }

    /// <summary>The titles of the tests a run of 'passes' and 'fails' with <paramref name="retries"/> kept a trace page for under <paramref name="mode"/>.</summary>
    private async Task<List<string>> TracedAsync(RecordingMode mode, int retries)
    {
        var results = Results();
        foreach (var title in new[] { "passes", "fails" })
        {
            for (var attempt = 1; attempt <= retries + 1; attempt++)
            {
                await using var session = await StartAsync(results, title, mode, attempt);
                await session.App.OpenAsync("/");
                if (title == "passes")
                {
                    session.Complete();
                    break;
                }

                session.Complete(new TestException("TEST_FAILED", "nope"));
            }
        }

        return [.. new[] { "passes", "fails" }.Where(title => ReadTrace(results, title) is not null).Order(StringComparer.Ordinal)];
    }

    private string Results()
    {
        var results = Path.Combine(Path.GetTempPath(), "e2e-traces-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(results);
        _dirs.Add(results);
        return results;
    }

    private static string ProjectRoot([CallerFilePath] string testFile = "") => Path.GetDirectoryName(Path.GetDirectoryName(testFile))!;

    private static Task<E2ESession> StartAsync(string results, string title, RecordingMode mode = RecordingMode.RetainOnFailure, int attempt = 1, ReportingEngine? engine = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine ?? new ReportingEngine(),
            TestTitle = title,
            BaseUrl = "http://127.0.0.1:4100",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ProjectRoot = ProjectRoot(),
            ResultsDirectory = results,
            CacheEnabled = false,
            Trace = new ResolvedRecording(mode, RecordingSource.Target),
            Attempt = attempt,
        });

    /// <summary>The trace page of the test titled <paramref name="title"/>, <c>trace.md</c> in its directory under <paramref name="results"/>, if one was written.</summary>
    private static string? ReadTrace(string results, string title)
    {
        var page = Path.Combine(results, AttemptReport.ResultSegmentOf(null, title), "trace.md");
        return File.Exists(page) ? File.ReadAllText(page) : null;
    }

    private sealed class ReportingEngine(bool logs = false) : IEngine
    {
        public bool AskedForScreens { get; private set; }

        public string Platform => "document";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            AskedForScreens = options.Screen is not null;
            options.Environment?.Invoke(new Dictionary<string, string> { ["device"] = "Pixel 9", ["os"] = "Android 16" });
            if (logs)
            {
                options.AppLog?.Invoke(new AppLogEntry(AppLogSource.Console, AppLogLevel.Warning, "booting"));
            }

            return Task.FromResult<IEngineSession>(new Session(options, logs));
        }
    }

    private sealed class Session(EngineStartOptions options, bool logs) : IEngineSession
    {
        private int _taps;

        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(Screen(_taps >= 2 ? ["Submit", "Undo"] : ["Submit"], "observe"));

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            _taps++;
            if (logs)
            {
                options.AppLog?.Invoke(new AppLogEntry(AppLogSource.Network, AppLogLevel.Error, "POST http://127.0.0.1:4100/api/save 500 Internal Server Error"));
            }

            options.Screen?.Invoke(Screen(_taps == 1 ? ["Submit"] : ["Submit", "Undo"], $"t{_taps}"));
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Observation Screen(string[] names, string mint) => new()
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "root", Role = "document", Children = [.. names.Select((name, index) => new SemanticNode { Ref = $"{(name == "Submit" ? "submit" : mint + "-" + index)}", Role = "button", Name = name })] }],
        };
    }
}
