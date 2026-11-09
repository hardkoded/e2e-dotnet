// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.McpTestRun;

/// <summary>
/// <c>describe('open_session {test}')</c> &gt; <c>describe('over e2e mcp')</c> of <c>tests/integration/mcp-test-run.test.ts</c> (tester-army/e2e#1315).
/// All four ported <c>it</c>s are <c>Partial_</c>: they run <see cref="NUnitSessionTestRunner"/>, the seam <c>e2e mcp</c> uses, and not the MCP server
/// (the server path was checked by hand; <c>McpSessionTest/SessionTestsTests</c> covers the loading and the refusals, and
/// <c>McpTestOutcome/TestOutcomeTests</c> the opening text). The first leaves out <c>afterEach</c> at <c>close_session</c> (hooks run before the
/// hand-over). The second checks the session holds the failing step's line and the evidence the text prints, not the text itself. The third
/// has no <c>file:line</c>, no target, no serial group, no several agents, and refuses a saved session before the run, where upstream also refuses the
/// setup test. The fourth ("compares a screenshot against the baseline an earlier session wrote") runs a fixture twice through the runner and
/// checks the first outcome fails with <c>no stored screenshot</c> and the second passes; it does not check the opening text or name the test
/// as <c>file:line</c>. Not ported, with the issue that extracts what each needs: "runs the test files as they are now when the session opens again"
/// (an assembly loads once per server; hardkoded/e2e-dotnet#1411), "fails a test on a promise it left rejected, and keeps serving", "keeps a body
/// its timeout cut short from driving the session", "reports a beforeAll failure as the outcome, once" (no outcome phases; #1410),
/// "records a session's agent steps for the next session and for e2e run" (describe 'the replay cache'; ported in part as `TheReplayCacheTests`, which lacks the `e2e run` half until the run seam of #1336),
/// the two <c>test.fail</c> <c>it</c>s ("tells a test that
/// failed as test.fail expected, and one that passed unexpectedly", "does not cover a beforeAll failure with test.fail"; the port has no <c>test.fail</c>,
/// see COMPATIBILITY.md). The unit <c>it</c> in <c>mcp-test-lines.test.ts</c> ("drafts the lines after
/// a test the session ran as the ones that continue it") is <c>McpTestLines/SessionTestLinesTests</c> in E2E.Cli.Tests.
/// </summary>
public sealed class OpenSessionTestOverE2eMcpTests
{
    [Test]
    public async Task Partial_Leaves_a_passing_test_on_its_final_screen_and_runs_its_afterEach_at_close_session()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Opens_the_browser_page), CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var session = outcome!.Session;
        Assert.That(outcome.Failure, Is.Null);
        await Expect.That(session.Screen.GetByLabel("Loads")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Partial_Opens_a_failing_test_with_its_code_line_code_frame_and_nearest_nodes_on_the_screen_it_failed_on()
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, typeof(SessionFixture).FullName + "." + nameof(SessionFixture.Fails_on_the_browser_page), CancellationToken.None);

        Assert.That(outcome, Is.Not.Null);
        await using var session = outcome!.Session;
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

    /// <summary>Runs the named test as a session and disposes its session; what is left is the outcome's name and failure.</summary>
    private static async Task<(string TestName, Exception? Failure)> RunAsync(string name)
    {
        var outcome = await new NUnitSessionTestRunner().RunAsync(typeof(SessionFixture).Assembly, name, CancellationToken.None);
        Assert.That(outcome, Is.Not.Null);
        await using var session = outcome!.Session;
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
