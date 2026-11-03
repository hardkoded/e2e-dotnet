// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.McpTestRun;

/// <summary>
/// <c>describe('open_session {test}')</c> &gt; <c>describe('over e2e mcp')</c> of <c>tests/integration/mcp-test-run.test.ts</c> (tester-army/e2e#1315).
/// All three ported <c>it</c>s are <c>Partial_</c>: they run <see cref="NUnitSessionTestRunner"/>, the seam <c>e2e mcp</c> uses, and not the MCP server
/// (the server path was checked by hand; <c>McpSessionTest/SessionTestsTests</c> covers the loading and the refusals, and
/// <c>McpTestOutcome/TestOutcomeTests</c> the opening text). The first leaves out <c>afterEach</c> at <c>close_session</c> (hooks run before the
/// hand-over). The second checks the session holds the failing step's line and the evidence the text prints, not the text itself. The third
/// has no <c>file:line</c>, no target, no serial group, no several agents, and refuses a saved session before the run, where upstream also refuses the
/// setup test. Not ported: "runs the test files as they are now when the session opens again" (an assembly loads once per server),
/// "fails a test on a promise it left rejected, and keeps serving", "keeps a body its timeout cut short from driving the session",
/// "reports a beforeAll failure as the outcome, once", "compares a screenshot against the baseline an earlier session wrote" (no test
/// slot per process, no collection pass, no per-session screenshot run),
/// "records a session's agent steps for the next session and for e2e run" (describe 'the replay cache'; needs a model), and both
/// <c>describe('sessions side by side')</c> <c>it</c>s ("runs none of a test whose open was cancelled while it waited for its turn",
/// "collect and run their tests one at a time, and both pass"). The unit <c>it</c> in <c>mcp-test-lines.test.ts</c> ("drafts the lines after
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
