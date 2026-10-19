// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>
/// The screenshot a custom executor's judgment keeps as evidence. Upstream reads the step's artifacts and events from its report,
/// which the port does not have, so these read the files under the attempt's results and <see cref="AgentException.Screenshot"/>.
/// The engine is a fake one that captures a masked screenshot, where upstream drives Chromium.
/// </summary>
public sealed class AssertEvidenceUnderACustomExecutorTests
{
    private readonly List<string> _seen = [];

    private TestExecutor Judging() => new("judging", async (context, _) =>
    {
        _seen.Add(context.Step.Instruction);
        if (context.Step.Instruction.Contains("sign in", StringComparison.Ordinal))
        {
            var observation = await context.ObserveAsync();
            await context.Actions.TypeSecretAsync(TargetFor(observation.Text, "textbox \"Password\""), "admin.password");
            return Passed("filled the password");
        }

        return context.Step.Instruction.Contains("checkout", StringComparison.Ordinal)
            ? Failed("no checkout page exists here")
            : Passed("the counter reads 0");
    })
    {
        Version = "1",
    };

    /// <summary>
    /// Partial: the port has no step record, so it counts the files the attempt kept: one per assertion that took a screenshot,
    /// and the failed assertion's path on its exception.
    /// </summary>
    [Fact]
    public async Task Partial_Takes_the_screenshot_after_the_verdict_on_a_pass_and_on_a_failure_alike_and_none_with_screenshot_false()
    {
        var root = TempRoot();
        await using var session = await StartAsync(root, ExecutorKit.WorldWithAPassword(_ => { }));
        await session.App.OpenAsync("/");
        await session.Agent.AssertAsync("the counter shows zero", new AssertOptions { Screenshot = false });
        Assert.Empty(Screenshots(root));
        await session.Agent.AssertAsync("the counter shows zero");
        Assert.Single(Screenshots(root));
        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the checkout page is visible"));

        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal(2, Screenshots(root).Count);
        Assert.EndsWith("002-assert.png", error.Screenshot, StringComparison.Ordinal);
    }

    /// <summary>
    /// The path the test body is handed is relative to the project root, and names the file. The step's report is the
    /// session's evidence and the page.
    /// </summary>
    [Fact]
    public async Task Hands_the_failed_verdict_the_path_of_its_screenshot_and_the_runner_keeps_its_own_failure_capture_beside_it()
    {
        var root = TempRoot();
        var (session, error) = await FailAsync(root, "the checkout page is visible");

        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(@"^\.e2e/results/.+/screenshots/\d+-assert\.png$", error.Screenshot);
        Assert.Equal(ScreenshotEngine.Png(), await File.ReadAllBytesAsync(Path.Combine(root, error.Screenshot!)));
        Assert.Single(Screenshots(root));

        // What the runner saw when the failure landed is a capture of its own, not the step's evidence.
        var evidence = session.Evidence!;
        Assert.NotNull(evidence.Screen);
        Assert.Equal(E2E.FailureEvidence.ScreenshotArtifactId, evidence.Screenshot);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.Combine(root, error.Screenshot!)))!, "screenshots", "failure.png")));
    }

    /// <summary>
    /// Partial: the port has no step record, so it reads the refusal from the call. Upstream's configuration exit code and error
    /// category belong to its runner.
    /// </summary>
    [Fact]
    public async Task Partial_Refuses_vision_with_UNSUPPORTED_CAPABILITY_before_the_executor_is_asked_what_its_model_sees_is_its_own_call()
    {
        var root = TempRoot();
        await using var session = await StartAsync(root, ExecutorKit.WorldWithAPassword(_ => { }));
        await session.App.OpenAsync("/");

        var error = await Assert.ThrowsAsync<ConfigurationException>(() =>
            session.Agent.AssertAsync("the counter shows zero, judged from pixels", new AssertOptions { Vision = VisionMode.On }));

        Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
        Assert.Contains("vision", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the counter shows zero, judged from pixels", _seen);
    }

    /// <summary>
    /// Partial: the port records no policy event, so the denial shows as the missing screenshot: no path on either verdict, and
    /// no file kept. Upstream keeps the failure capture's screenshot after a secret fill; the port keeps its screen text and no pixels.
    /// </summary>
    [Fact]
    public async Task Partial_Denies_the_screenshot_as_PIXEL_TAINTED_after_a_secret_fill_on_both_verdicts_and_the_failure_capture_keeps_no_pixels_either()
    {
        var root = TempRoot();
        var session = await StartAsync(root, ExecutorKit.WorldWithAPassword(_ => { }));
        AgentException error;
        try
        {
            await session.App.OpenAsync("/");
            await session.Agent.ActAsync(
                "sign in with the given credentials",
                new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass", "password") } });

            await session.Agent.AssertAsync("the counter shows zero");
            error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync("the checkout page is visible"));
            session.Complete(error);
        }
        finally
        {
            await session.DisposeAsync();
        }

        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Null(error.Screenshot);
        Assert.Empty(Screenshots(root));
        Assert.NotNull(session.Evidence?.Screen);
        Assert.Null(session.Evidence?.Screenshot);
        Assert.Empty(Directory.GetFiles(Path.Combine(root, ".e2e"), "failure.png", SearchOption.AllDirectories));
    }

    private async Task<(E2ESession Session, AgentException Error)> FailAsync(string root, string statement)
    {
        var session = await StartAsync(root, ExecutorKit.WorldWithAPassword(_ => { }));
        try
        {
            await session.App.OpenAsync("/");
            var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.AssertAsync(statement));
            session.Complete(error);
            return (session, error);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-executor-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static List<string> Screenshots(string root) =>
        Directory.Exists(Path.Combine(root, ".e2e")) ? Directory.GetFiles(Path.Combine(root, ".e2e"), "*-assert.png", SearchOption.AllDirectories).ToList() : [];

    private Task<E2ESession> StartAsync(string root, DocumentWorld world) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ScreenshotEngine(world, _ => ScreenshotEngine.Png()),
            AgentExecutor = Judging(),
            BaseUrl = "https://counter.test",
            ProjectRoot = root,
            TestTitle = "evidence",
        });
}
