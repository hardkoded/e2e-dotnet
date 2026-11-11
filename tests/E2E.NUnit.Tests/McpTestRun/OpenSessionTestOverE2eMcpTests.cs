// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.McpTestRun;

/// <summary>
/// <c>describe('open_session {test}')</c> &gt; <c>describe('over e2e mcp')</c> of <c>tests/integration/mcp-test-run.test.ts</c> (tester-army/e2e#1315).
/// All eight ported <c>it</c>s are <c>Partial_</c>: seven run <see cref="NUnitSessionTestRunner"/>, the seam <c>e2e mcp</c> uses, and the eighth loads the assembly through <c>SessionTests.Load</c>; none runs the MCP server
/// (the server path was checked by hand; <c>McpSessionTest/SessionTestsTests</c> covers the loading and the refusals, and
/// <c>McpTestOutcome/TestOutcomeTests</c> the opening text). The first leaves out <c>afterEach</c> at <c>close_session</c> (hooks run before the
/// hand-over). The second checks the session holds the failing step's line and the evidence the text prints, not the text itself. The third
/// has no <c>file:line</c>, no target, no serial group, no several agents, and refuses a saved session before the run, where upstream also refuses the
/// setup test. The fourth ("compares a screenshot against the baseline an earlier session wrote") runs a fixture twice through the runner and
/// checks the first outcome fails with <c>no stored screenshot</c> and the second passes; it does not check the opening text or name the test
/// as <c>file:line</c>. Three more run the same seam and leave out the text and the server too (#1410): "reports a beforeAll failure as the outcome, once"
/// checks the outcome (no session, <c>HOOK_FAILED</c>, phase <c>beforeAll</c>, nothing to end), and not <c>0 steps ran</c> or <c>No screen to show</c>;
/// "fails a test on a promise it left rejected, and keeps serving" checks the failure, the close's report, and the operator's warning of the
/// seam's <see cref="StrayRejections"/>, and not the opening text; "keeps a body its timeout cut short from driving the session" runs a fixture with a
/// 5 s timeout (upstream's is 1 s, and the port's clock starts before the engine launches, so the first page must open inside it). It checks the
/// <c>TEST_TIMEOUT</c> failure, the step count, and the screen the session observes after its body timed out, but not the opening text; NUnit awaits the body, so the outcome
/// lands after it, and not at the timeout. "runs the test files as they are now when the session opens again" is <c>Partial_</c>: it loads the test assembly twice through
/// <c>SessionTests.Load</c> and checks the second load is a new one, not the server's open twice (an assembly loads once per server; hardkoded/e2e-dotnet#1411).
/// Not ported, with the issue that extracts what each needs:
/// "records a session's agent steps for the next session and for e2e run" (describe 'the replay cache'; ported in part as `TheReplayCacheTests`, which lacks the `e2e run` half until the run seam of #1336),
/// the two <c>test.fail</c> <c>it</c>s ("tells a test that
/// failed as test.fail expected, and one that passed unexpectedly", "does not cover a beforeAll failure with test.fail") are ported in part, as
/// <c>Partial_</c> tests: the mark is NUnit's <c>[Property("E2E.Fail", reason)]</c> (or set in the body), and the run's own verdict is not inverted (see COMPATIBILITY.md).
/// They check the outcome and not the opening text. The unit <c>it</c> in <c>mcp-test-lines.test.ts</c> ("drafts the lines after
/// a test the session ran as the ones that continue it") is <c>McpTestLines/SessionTestLinesTests</c> in E2E.Cli.Tests.
/// </summary>
public sealed class OpenSessionTestOverE2eMcpTests
{
    [Test]
    public async Task Partial_Leaves_a_passing_test_on_its_final_screen_and_runs_its_afterEach_at_close_session()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Opens_the_browser_page), CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        var session = outcome!.Session!;
        Assert.That(outcome.Failure, Is.Null);
        await Expect.That(session.Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Partial_Opens_a_failing_test_with_its_code_line_code_frame_and_nearest_nodes_on_the_screen_it_failed_on()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Fails_on_the_browser_page), CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        var session = outcome!.Session!;
        Assert.That(outcome.Failure, Is.Not.Null);
        Assert.That(outcome.Failure!.Message, Does.Contain("the count"));

        // What the opening text prints: the line of the failing step, and the screen the failure landed on.
        Assert.That(session.StepRecords.Last(step => step.Status != StepStatus.Passed).Source?.File, Does.EndWith("OpenSessionTestOverE2eMcpTests.cs"));
        Assert.That(session.Evidence?.Url, Does.Contain("/browser"));
        await Expect.That(session.Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    [Test]
    public void Partial_Refuses_a_name_that_selects_no_test_several_or_one_a_session_cannot_run_alone_before_anything_boots()
    {
        var runner = new NUnitSessionTestRunner();
        var assembly = typeof(SessionFixture).Assembly;

        Assert.That(runner.Select(assembly, "No.Such.Test"), Is.Empty);
        var several = runner.Select(assembly, typeof(SessionFixture).FullName!);
        Assert.That(several.Select(test => test.FullName), Is.EquivalentTo([typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Opens_the_browser_page), typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Fails_on_the_browser_page), typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Starts_from_a_saved_session)]));
        var saved = runner.Select(assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Starts_from_a_saved_session));
        Assert.That(saved, Has.Count.EqualTo(1));
        Assert.That(saved[0].Unsupported, Is.EqualTo("test \"" + typeof(SessionFixture).FullName + ".Starts_from_a_saved_session\" starts from session \"member\", which a setup test saves first; a session does not run setup tests yet, so run it with dotnet test"));
        Assert.That(runner.Select(assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Opens_the_browser_page))[0].Unsupported, Is.Null);
    }

    [Test]
    public async Task Partial_Tells_a_test_that_failed_as_test_fail_expected_and_one_that_passed_unexpectedly()
    {
        var runner = new NUnitSessionTestRunner();
        var assembly = typeof(ExpectedFailureFixture).Assembly;

        var expected = await runner.RunAsync(assembly, typeof(ExpectedFailureFixture).FullName + "." + nameof(ExpectedFailureFixture.Counter_gap), CancellationToken.None);
        Assert.That(expected, Is.Not.Null);
        await using var closingExpected = expected!;
        Assert.That(expected!.ExpectedFailure, Is.EqualTo("the counter starts at one"));
        Assert.That(expected.Failure, Is.Not.Null);
        Assert.That(expected.Failure!.Message, Does.Not.Contain("UNEXPECTED_PASS"));

        var fixedGap = await runner.RunAsync(assembly, typeof(ExpectedFailureFixture).FullName + "." + nameof(ExpectedFailureFixture.Fixed_gap), CancellationToken.None);
        Assert.That(fixedGap, Is.Not.Null);
        await using var closingFixed = fixedGap!;
        Assert.That(fixedGap!.ExpectedFailure, Is.EqualTo("the home page is broken"));
        Assert.That(fixedGap.Failure?.Message, Is.EqualTo("test is marked test.fail (\"the home page is broken\") but passed"));
    }

    [Test]
    public async Task Partial_Does_not_cover_a_beforeAll_failure_with_test_fail()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(MarkedHooksFixture).Assembly, typeof(MarkedHooksFixture).FullName + "." + nameof(MarkedHooksFixture.Needs_the_database), CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        Assert.That(outcome!.Session, Is.Null);
        Assert.That(outcome.Phase, Is.EqualTo(RunPhase.BeforeAll));
        Assert.That(outcome.Failure!.Message, Does.StartWith("beforeAll failed: "));
        Assert.That(outcome.ExpectedFailure, Is.EqualTo("the database gap"));
    }

    [Test]
    [NonParallelizable]
    public async Task Partial_Compares_a_screenshot_against_the_baseline_an_earlier_session_wrote()
    {
        var name = typeof(ScreenshotSessionFixture).FullName + "." + nameof(ScreenshotSessionFixture.Matches_its_screenshot);
        var snapshots = ScreenshotSessionFixture.SnapshotsDirectory();
        var ci = Environment.GetEnvironmentVariable("CI");
        var update = Environment.GetEnvironmentVariable("E2E_UPDATE_SNAPSHOTS");

        // The directory belongs to this fixture alone: nothing else stores screenshots beside this file.
        if (Directory.Exists(snapshots))
        {
            Directory.Delete(snapshots, recursive: true);
        }

        // As upstream's server runs with CI empty: a CI run writes no baseline, and an update run writes it and passes.
        Environment.SetEnvironmentVariable("CI", "0");
        Environment.SetEnvironmentVariable("E2E_UPDATE_SNAPSHOTS", null);
        try
        {
            var first = await RunAsync(name);
            Assert.That(first.Failure, Is.Not.Null);
            Assert.That(first.Failure!.Message, Does.Contain("no stored screenshot"));

            // Upstream's store of written screenshots is per store, so the second session starts with none; the port's is per process.
            ScreenshotContext.ForgetWrittenUnder(snapshots);
            var second = await RunAsync(name);
            Assert.That(second.Failure, Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", ci);
            Environment.SetEnvironmentVariable("E2E_UPDATE_SNAPSHOTS", update);
            if (Directory.Exists(snapshots))
            {
                Directory.Delete(snapshots, recursive: true);
            }
        }
    }

    [Test]
    public async Task Partial_Reports_a_beforeAll_failure_as_the_outcome_once()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, typeof(HooksSessionFixture).FullName + "." + nameof(HooksSessionFixture.Needs_the_database), CancellationToken.None);

        // The test never opened the app, and the outcome is the hook's: no session to show, and nothing for the close to repeat.
        Assert.That(outcome, Is.Not.Null);
        Assert.That(outcome!.Session, Is.Null);
        Assert.That(outcome.Phase, Is.EqualTo(RunPhase.BeforeAll));
        Assert.That(outcome.Failure, Is.TypeOf<TestException>());
        Assert.That(((TestException)outcome.Failure!).Code, Is.EqualTo("HOOK_FAILED"));
        Assert.That(outcome.Failure!.Message, Is.EqualTo("beforeAll failed: database is down"));
        Assert.That(outcome.Run, Is.Null);
    }

    [Test]
    [NonParallelizable]
    public async Task Partial_Fails_a_test_on_a_promise_it_left_rejected_and_keeps_serving()
    {
        var runner = new NUnitSessionTestRunner();
        var assembly = typeof(SessionFixture).Assembly;
        var warnings = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var warn = StrayRejections.Warn;
        StrayRejections.Warn = warnings.Enqueue;
        try
        {
            var forgot = await runner.RunAsync(assembly, typeof(SloppySessionFixture).FullName + "." + nameof(SloppySessionFixture.Forgets_an_await), CancellationToken.None);
            Assert.That(forgot, Is.Not.Null);
            await using (forgot!.Session)
            {
                Assert.That(forgot.Failure?.Message, Is.EqualTo("seed endpoint returned 500"));
                Assert.That(forgot.Run!.End(), Is.Empty);
            }

            // One that rejects after the body ended is the test's too, reported at close.
            var left = await runner.RunAsync(assembly, typeof(SloppySessionFixture).FullName + "." + nameof(SloppySessionFixture.Leaves_a_promise_behind), CancellationToken.None);
            Assert.That(left, Is.Not.Null);
            Assert.That(left!.Failure, Is.Null);
            SloppySessionFixture.LeftRelease.SetResult();
            await Task.Delay(300);
            await using (left.Session)
            {
                var closed = left.Run!.End();
                Assert.That(closed.Select(cause => (cause as TestException)?.Code + ": " + cause.Message), Is.EqualTo(["UNHANDLED_REJECTION: a promise rejected after the test's body ended, most likely left by \"" + left.TestName + "\": late failure"]));
            }

            // One that rejects once its session closed is a warning for the operator, not a crash.
            var early = await runner.RunAsync(assembly, typeof(SloppySessionFixture).FullName + "." + nameof(SloppySessionFixture.Leaves_a_promise_for_after_the_close), CancellationToken.None);
            Assert.That(early, Is.Not.Null);
            Assert.That(early!.Failure, Is.Null);
            await using (early.Session)
            {
                Assert.That(early.Run!.End(), Is.Empty);
            }

            SloppySessionFixture.EarlyRelease.SetResult();

            // .NET reports the faulted task when it is collected: look until it lands, as the host does for a few seconds after a close.
            for (var look = 0; look < 40 && warnings.IsEmpty; look++)
            {
                await Task.Delay(250);
                StrayRejections.Collect();
            }

            Assert.That(warnings, Does.Contain("a promise rejected after its session closed, most likely left by test \"" + early.TestName + "\": later failure"));

            var after = await runner.RunAsync(assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Opens_the_browser_page), CancellationToken.None);
            Assert.That(after, Is.Not.Null);
            await using (after!.Session)
            {
                Assert.That(after.Failure, Is.Null);
            }
        }
        finally
        {
            StrayRejections.Warn = warn;
        }
    }

    /// <summary>Runs the named test as a session and disposes its session; what is left is the outcome's name and failure.</summary>
    private static async Task<(string TestName, Exception? Failure)> RunAsync(string name)
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, name, CancellationToken.None);
        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        var session = outcome!.Session!;
        return (outcome.TestName, outcome.Failure);
    }

    [Test]
    public void Partial_Runs_the_test_files_as_they_are_now_when_the_session_opens_again()
    {
        // The test assembly is collected again on every open, from the bytes on disk now: a rebuilt test replaces the file even while a session holds the old one.
        var directory = Path.Combine(Path.GetTempPath(), "e2e-reload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.GetDirectoryName(typeof(SessionFixture).Assembly.Location)!;
            foreach (var file in Directory.EnumerateFiles(source, "E2E.NUnit.Tests.*"))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            var path = Path.Combine(directory, "E2E.NUnit.Tests.dll");
            using var first = E2E.Cli.Mcp.SessionTests.Load(path);
            File.Delete(path);
            File.Copy(Path.Combine(source, "E2E.NUnit.Tests.dll"), path);
            using var second = E2E.Cli.Mcp.SessionTests.Load(path);

            Assert.That(second.Tests, Is.Not.SameAs(first.Tests));
            Assert.That(second.Tests.GetType(typeof(SessionFixture).FullName!), Is.Not.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Selects_a_test_by_file_and_line_and_refuses_a_line_inside_its_body()
    {
        using var loaded = E2E.Cli.Mcp.SessionTests.Load(typeof(SessionFixture).Assembly.Location);
        var lines = File.ReadAllLines(SourcePath());
        int LineOf(string fragment) => Array.FindIndex(lines, line => line.Contains(fragment, StringComparison.Ordinal)) + 1;
        var opens = LineOf("public async Task Opens_the_" + "browser_page() =>");
        var fails = LineOf("public async Task Fails_on_" + "the_browser_page()");
        var expected = typeof(SessionFixture).FullName + ".";

        // The attribute line and the signature line both name the test.
        Assert.That(E2E.Cli.Mcp.SessionTests.SelectOne(loaded.Runner, loaded.Tests, "McpTestRun/OpenSessionTestOverE2eMcpTests.cs:" + opens, "tests.dll"), Is.EqualTo(expected + "Opens_the_browser_page"));
        Assert.That(E2E.Cli.Mcp.SessionTests.SelectOne(loaded.Runner, loaded.Tests, "OpenSessionTestOverE2eMcpTests.cs:" + (opens - 1), "tests.dll"), Is.EqualTo(expected + "Opens_the_browser_page"));
        Assert.That(E2E.Cli.Mcp.SessionTests.SelectOne(loaded.Runner, loaded.Tests, "OpenSessionTestOverE2eMcpTests.cs:" + fails, "tests.dll"), Is.EqualTo(expected + "Fails_on_the_browser_page"));

        var inside = Assert.Throws<E2E.ConfigurationException>(() => E2E.Cli.Mcp.SessionTests.SelectOne(loaded.Runner, loaded.Tests, "OpenSessionTestOverE2eMcpTests.cs:" + (fails + 2), "tests.dll"));
        Assert.That(inside!.Code, Is.EqualTo("NO_TESTS"));
        Assert.That(inside.Message, Does.Contain("names no test (declared at lines "));
    }

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Test]
    public async Task Partial_Keeps_a_body_its_timeout_cut_short_from_driving_the_session()
    {
        var name = typeof(TimedOutSessionFixture).FullName + "." + nameof(TimedOutSessionFixture.Outlives_its_timeout);
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, name, CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var closing = outcome!;
        var session = outcome!.Session!;
        Assert.That(outcome.Failure, Is.TypeOf<TestException>());
        Assert.That(((TestException)outcome.Failure!).Code, Is.EqualTo("TEST_TIMEOUT"));
        Assert.That(outcome.Failure!.Message, Is.EqualTo("test timed out after 5000 ms in phase body"));
        Assert.That(outcome.Phase, Is.EqualTo(RunPhase.Body));

        // Upstream's "1 step ran": the step its timeout cut short (app.open('/about')) is not recorded.
        Assert.That(session.StepRecords, Has.Count.EqualTo(1));

        // Past the time the body's last line would have run: the session still observes the screen the body left, not the page that line opens.
        await Expect.That(session.Screen.GetByRole("heading", "Playground")).ToBeVisibleAsync();
        await Expect.That(session.Screen.GetByRole("heading", "About")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Finds_no_session_for_a_name_that_selects_no_test()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, "No.Such.Test", CancellationToken.None);

        Assert.That(outcome, Is.Null);
    }
}

/// <summary>The tests <see cref="OpenSessionTestOverE2eMcpTests"/> runs.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class SessionFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    // The project root is where the test sources are, so a step names the line it was called from.
    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Opens_the_browser_page() => await App.OpenAsync("/browser");

    [Test]
    [E2ESession("member")]
    public void Starts_from_a_saved_session()
    {
    }

    [Test]
    public async Task Fails_on_the_browser_page()
    {
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Loads")).ToHaveTextAsync("the count", timeout: TimeSpan.FromMilliseconds(200));
    }
}

/// <summary>The test <see cref="OpenSessionTestOverE2eMcpTests"/> runs for a body its timeout cut short.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class TimedOutSessionFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // The port's clock starts before the engine launches, so the timeout is longer than upstream's 1 s: the first page still opens inside it.
    protected override TimeSpan TestTimeout => TimeSpan.FromSeconds(5);

    [Test]
    public async Task Outlives_its_timeout()
    {
        await App.OpenAsync("/");
        await Task.Delay(TimeSpan.FromSeconds(6));
        await App.OpenAsync("/about");
    }
}

/// <summary>The test <see cref="OpenSessionTestOverE2eMcpTests"/> runs for the screenshot baseline.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class ScreenshotSessionFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    /// <summary>Where the stored screenshots of this file live: <c>&lt;test file&gt;-snapshots</c>.</summary>
    public static string SnapshotsDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path + "-snapshots";

    [Test]
    public async Task Matches_its_screenshot()
    {
        await App.OpenAsync("/swatches");
        await Expect.That(Screen.GetByTestId("far")).ToHaveScreenshotAsync("far.png");
    }
}

/// <summary>The tests of <c>sloppy.e2e.ts</c> that <see cref="OpenSessionTestOverE2eMcpTests"/> runs, but the one that outlives its timeout (<see cref="TimedOutSessionFixture"/>).</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class SloppySessionFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    private static async Task SeedAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("seed endpoint returned 500");
    }

    [Test]
    public async Task Forgets_an_await()
    {
        await App.OpenAsync("/browser");
        _ = SeedAsync();
        await Task.Delay(100);
        await Expect.That(Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Released by the test once the run of "leaves a promise behind" has returned, so its rejection lands after the turn and not during it:
    /// a timer of a fixed length races the run's own work on a slow runner.
    /// </summary>
    public static readonly TaskCompletionSource LeftRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Released by the test once the session of "leaves a promise for after the close" has closed.</summary>
    public static readonly TaskCompletionSource EarlyRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    public async Task Leaves_a_promise_behind()
    {
        await App.OpenAsync("/browser");
        _ = Task.Run(async () =>
        {
            await LeftRelease.Task;
            throw new InvalidOperationException("late failure");
        });
    }

    [Test]
    public async Task Leaves_a_promise_for_after_the_close()
    {
        await App.OpenAsync("/browser");
        _ = Task.Run(async () =>
        {
            await EarlyRelease.Task;
            throw new InvalidOperationException("later failure");
        });
    }
}

/// <summary>The test of <c>hooks.e2e.ts</c> that <see cref="OpenSessionTestOverE2eMcpTests"/> runs: its <c>beforeAll</c> fails.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class HooksSessionFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [OneTimeSetUp]
    public void BeforeAll() => throw new InvalidOperationException("database is down");

    [Test]
    public async Task Needs_the_database() => await App.OpenAsync("/browser");
}

/// <summary>The tests <see cref="OpenSessionTestOverE2eMcpTests"/> runs for <c>test.fail</c>: one fails as its mark expects, one passes though marked.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class ExpectedFailureFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    [Property("E2E.Fail", "the counter starts at one")]
    public async Task Counter_gap()
    {
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Loads")).ToHaveTextAsync("the counter starts at one", timeout: TimeSpan.FromMilliseconds(200));
    }

    [Test]
    public async Task Fixed_gap()
    {
        // The mark is set in the body, as test.fail is called there; the page opens, so the test passes though it is marked.
        global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentTest.Properties.Set("E2E.Fail", "the home page is broken");
        await App.OpenAsync("/");
    }
}

/// <summary>The test <see cref="OpenSessionTestOverE2eMcpTests"/> runs for a <c>beforeAll</c> that fails under a mark.</summary>
[Explicit("Run by OpenSessionTestOverE2eMcpTests")]
public sealed class MarkedHooksFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [OneTimeSetUp]
    public void Breaks_before_all() => throw new InvalidOperationException("database is down");

    [Test]
    [Property("E2E.Fail", "the database gap")]
    public async Task Needs_the_database() => await App.OpenAsync("/");
}
