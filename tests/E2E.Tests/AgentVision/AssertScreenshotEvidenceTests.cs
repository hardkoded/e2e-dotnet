// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;

namespace E2E.Tests.AgentVision;

/// <summary>
/// The screenshot a judgment keeps as evidence, and <see cref="AgentException.Screenshot"/>. These are .NET-only tests:
/// upstream reads the evidence from the step's artifacts in its report, which the port does not have.
/// </summary>
public sealed class AssertScreenshotEvidenceTests
{
    [Fact]
    public async Task Keeps_the_screenshot_of_a_failed_assertion_and_names_it_on_the_exception()
    {
        var root = TempRoot();
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the page is About"));
        var result = await RunAsync(root, ctx => ctx.Agent.AssertAsync("the page is Home"), judge);

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.NotNull(error.Screenshot);
        Assert.Equal(".e2e/results/" + AttemptReport.ResultSegmentOf(null, "evidence") + "/attempt-1/screenshots/001-assert.png", error.Screenshot);
        Assert.Equal(ScreenshotEngine.Png(), await File.ReadAllBytesAsync(Path.Combine(root, error.Screenshot!)));
    }

    [Fact]
    public async Task Keeps_the_screenshot_of_a_passing_assertion_too()
    {
        var root = TempRoot();
        var result = await RunAsync(root, ctx => ctx.Agent.AssertAsync("the page is About"), new ScriptedModel(_ => ModelResponses.Done("passed", "it is")));

        Assert.Null(result.Error);
        Assert.True(File.Exists(Path.Combine(root, ".e2e", "results", AttemptReport.ResultSegmentOf(null, "evidence"), "attempt-1", "screenshots", "001-assert.png")));
    }

    [Fact]
    public async Task Takes_no_screenshot_when_the_assertion_turns_it_off()
    {
        var root = TempRoot();
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the page is About"));
        var result = await RunAsync(root, ctx => ctx.Agent.AssertAsync("the page is Home", new AssertOptions { Screenshot = false }), judge);

        Assert.Null(Assert.IsType<AgentException>(result.Error).Screenshot);

        // The run's report.json is written beside the results, so the directory may exist; no screenshot is in it.
        Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root, "*.png", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task Fails_the_assertion_alone_when_the_engine_captures_no_evidence()
    {
        var root = TempRoot();
        var judge = new ScriptedModel(_ => ModelResponses.Done("failed", "the page is About"));
        var result = await RunAsync(root, ctx => ctx.Agent.AssertAsync("the page is Home"), judge, new ScreenshotEngine(World()));

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Null(error.Screenshot);
    }

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "e2e-evidence-" + Guid.NewGuid().ToString("N"));

    private static DocumentWorld World() => CounterPage.World().Map("/about", page =>
    {
        page.Heading("About");
        page.Textbox("Password", "", secure: true);
    });

    private static async Task<CoreTests.Attempt> RunAsync(string root, Func<TestContext, Task> body, ScriptedModel judge, ScreenshotEngine? engine = null)
    {
        Directory.CreateDirectory(root);
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine ?? new ScreenshotEngine(World(), _ => ScreenshotEngine.Png()),
            Judge = judge,
            BaseUrl = "https://app.test",
            ProjectRoot = root,
            TestTitle = "evidence",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),

            // The tests look for what the assertion writes, so no trace page is written beside it.
            Trace = new ResolvedRecording(RecordingMode.Off, RecordingSource.Default),
        });
        Exception? error = null;
        try
        {
            await session.App.OpenAsync("/about");
            await body(session.Context);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return new CoreTests.Attempt(error, session.ModelCalls, session.Replayed, session.Missed);
    }
}
