// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Tests.FailureEvidence;

/// <summary>
/// Upstream's <c>describe('failure evidence')</c> (<c>tests/integration/failure-evidence.test.ts</c>), driven through a session over an
/// engine that masks its screenshots, with no runner: a test's attempt is a session, and what upstream reads from the report the page
/// and the session's evidence tell. "looks at the screen before teardown runs, so an afterEach that navigates away cannot
/// replace the evidence" needs a test framework's teardown, so it is in <c>E2E.NUnit.Tests/FailureEvidence</c>. "keeps the evidence of two tests whose ids sanitize to the same directory name apart,
/// each report digest matching its file" is ported without the digest check: the port's report artifacts carry no sha256.
/// </summary>
public sealed class FailureEvidenceTests : IDisposable
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Lists_the_closest_nodes_for_a_waitFor_that_timed_out_a_word_one_edit_away_included_and_a_short_word_never_stretched()
    {
        var engine = new EvidenceEngine("Submit", "Themes", "Cancel");

        var attempt = await FailAsync(engine, "waits for a button named a little differently", async screen =>
            await screen.GetByRole("button", "Submits the order").WaitForAsync(new LocatorWaitForOptions { Timeout = Short }));

        Assert.Equal("TIMEOUT", attempt.Error.Code);
        Assert.Equal("getByRole(\"button\", \"Submits the order\")", attempt.Error.Details?.Locator);
        Assert.Equal("button", attempt.Error.Details?.Role);
        Assert.Equal("Submits the order", attempt.Error.Details?.Name);
        Assert.InRange(attempt.Error.Details?.WaitedMs ?? -1, 300, 3_000);
        var candidate = Assert.Single(attempt.Session.Evidence!.Candidates!);
        Assert.Contains("button \"Submit\"", candidate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_screen_a_screenshot_the_location_and_the_closest_nodes_when_a_locator_matched_nothing_and_the_locator_facts_on_the_error()
    {
        var engine = new EvidenceEngine("Submit");

        var attempt = await FailAsync(engine, "taps a button that is not there", async screen => await screen.GetByRole("button", "Submit now").TapAsync());

        Assert.Equal("NOT_FOUND", attempt.Error.Code);
        Assert.Equal("getByRole(\"button\", \"Submit now\")", attempt.Error.Details?.Locator);
        Assert.Equal("button", attempt.Error.Details?.Role);
        Assert.Equal("Submit now", attempt.Error.Details?.Name);

        // What the locator actually waited: the action timeout, give or take a poll.
        Assert.InRange(attempt.Error.Details?.WaitedMs ?? -1, 300, 3_000);

        // The error and the failing step both resolve to the line in the test file that made the call.
        const string file = "FailureEvidence/FailureEvidenceTests.cs";
        Assert.Equal(file, ErrorSerializer.Serialize(attempt.Error, ProjectRoot()).Source?.File);
        var failed = Assert.Single(attempt.Session.Steps, step => step.Status != StepStatus.Passed);
        Assert.Equal("locator.tap", failed.Api);
        Assert.Equal(file, failed.Source?.File);
        Assert.Equal(file, attempt.Session.Steps[0].Source?.File);

        var evidence = attempt.Session.Evidence!;
        Assert.Contains("127.0.0.1:4599", evidence.Url, StringComparison.Ordinal);

        // The Submit button is the button on screen; the locator asked for one named differently.
        Assert.Contains("button \"Submit\"", Assert.Single(evidence.Candidates!), StringComparison.Ordinal);
        var text = File.ReadAllText(Path.Combine(attempt.Dir, "screen-at-failure.txt"));
        Assert.Matches(new Regex(@"^#\S+ ", RegexOptions.None, TimeSpan.FromSeconds(2)), text);
        Assert.Contains("button \"Submit\"", text, StringComparison.Ordinal);
        Assert.Equal(new StepViewport(40, 20, 2), evidence.Viewport);
        Assert.Equal(text.Trim().Split('\n').Length, evidence.Nodes);
        Assert.True(File.Exists(Path.Combine(attempt.Dir, "screenshots", "failure.png")));
        Assert.Equal(1, engine.Count("screenshot"));

        // The page tells it all.
        var page = File.ReadAllText(Path.Combine(attempt.Dir, "..", "trace.md"));
        Assert.Contains("Closest to what the locator asked for:", page, StringComparison.Ordinal);
        Assert.Contains("screenshot at failure", page, StringComparison.Ordinal);
        Assert.Contains("Asked for:", page, StringComparison.Ordinal);
        Assert.Contains("Waited:", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Looks_once_when_the_look_sees_nothing_instead_of_spending_the_evidence_budget_again_before_the_session_closes()
    {
        var engine = new EvidenceEngine("Submit") { CapturesPixels = false };

        var attempt = await FailAsync(
            engine,
            "taps a button that is not there",
            async screen => await screen.GetByRole("button", "Submit now").TapAsync(),
            () => engine.OnObserve = _ => throw new InvalidOperationException("the page stopped answering"));

        Assert.Equal("NOT_FOUND", attempt.Error.Code);
        Assert.Null(attempt.Session.Evidence);
        Assert.Equal(1, engine.Operations.Skip(attempt.ObservedBefore).Count(operation => operation == "observe"));
    }

    [Fact]
    public async Task Records_what_an_assertion_expected_and_observed_as_details_and_always_captures_the_screen_and_a_screenshot()
    {
        var engine = new EvidenceEngine("Submit");

        var attempt = await FailAsync(engine, "expects the wrong count", async screen => await Expect.That(screen.GetByRole("button")).ToHaveCountAsync(2, TimeSpan.FromMilliseconds(200)));

        Assert.Equal("ASSERTION_FAILED", attempt.Error.Code);
        Assert.Equal("getByRole(\"button\")", attempt.Error.Details?.Locator);
        Assert.Equal("count 2", attempt.Error.Details?.Expected);
        Assert.Equal(1, attempt.Error.Details?.Matches);
        Assert.NotNull(attempt.Error.Details?.Observed);
        var evidence = attempt.Session.Evidence!;
        Assert.NotNull(evidence.Screen);
        Assert.NotNull(evidence.Screenshot);
        Assert.Null(evidence.Candidates);
        Assert.Equal(1, engine.Count("screenshot"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Looks_once_and_gives_up_at_the_evidence_budget_when_the_screen_never_answers_and_the_engine_honors_or_ignores_its_cancellation_keeping_the_original_failure(bool honorsCancellation)
    {
        var engine = new EvidenceEngine("Submit");
        var watch = Stopwatch.StartNew();

        var attempt = await FailAsync(
            engine,
            "fails, then the screen hangs",
            async screen => await screen.GetByRole("button", "Submit now").TapAsync(),
            () => engine.OnObserve = honorsCancellation
                ? token => Task.Delay(Timeout.Infinite, token)
                : _ => new TaskCompletionSource().Task);

        Assert.Equal("NOT_FOUND", attempt.Error.Code);
        Assert.Null(attempt.Session.Evidence);
        Assert.Equal(1, engine.Operations.Skip(attempt.ObservedBefore).Count(operation => operation == "observe"));

        // The 5 s budget once, not once per capture.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), watch.Elapsed.ToString());
    }

    [Fact]
    public async Task Keeps_the_evidence_of_two_tests_whose_titles_slug_alike_apart()
    {
        var results = Results();
        var first = await FailAsync(new EvidenceEngine("Alpha"), "artifact a", async screen => await screen.GetByRole("button", "Missing").TapAsync(), results: results);
        var second = await FailAsync(new EvidenceEngine("Omega"), "artifact-a", async screen => await screen.GetByRole("button", "Missing").TapAsync(), results: results);

        Assert.NotEqual(first.Dir, second.Dir);
        foreach (var (attempt, name) in new[] { (first, "Alpha"), (second, "Omega") })
        {
            Assert.Contains("button \"" + name + "\"", File.ReadAllText(Path.Combine(attempt.Dir, "screen-at-failure.txt")), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(attempt.Dir, "screenshots", "failure.png")));
        }
    }

    private static string ProjectRoot([CallerFilePath] string testFile = "") => Path.GetDirectoryName(Path.GetDirectoryName(testFile))!;

    private string Results()
    {
        var results = Path.Combine(Path.GetTempPath(), "e2e-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(results);
        _dirs.Add(results);
        return results;
    }

    /// <summary>Runs <paramref name="body"/> as one attempt that must fail, then releases the session, which is when the evidence is taken. <paramref name="afterFailure"/> runs in between.</summary>
    private async Task<Attempt> FailAsync(EvidenceEngine engine, string title, Func<Screen, Task> body, Action? afterFailure = null, string? results = null)
    {
        results ??= Results();
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = title,
            BaseUrl = "http://127.0.0.1:4599",
            ActionTimeout = Short,
            ProjectRoot = ProjectRoot(),
            ResultsDirectory = results,
            CacheEnabled = false,
            Trace = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target),
        });
        E2EException error;
        try
        {
            await session.App.OpenAsync("/");
            error = await Assert.ThrowsAnyAsync<E2EException>(() => body(session.Screen));
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }

        var observedBefore = engine.Operations.Count;
        afterFailure?.Invoke();
        session.Complete(error);
        await session.DisposeAsync();
        return new Attempt(session, error, Path.Combine(results, AttemptReport.ResultSegmentOf(title), "attempt-1"), observedBefore);
    }

    private sealed record Attempt(E2ESession Session, E2EException Error, string Dir, int ObservedBefore);
}
