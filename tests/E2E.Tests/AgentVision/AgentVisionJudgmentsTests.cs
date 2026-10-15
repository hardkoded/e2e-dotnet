// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentVision;

/// <summary>
/// Ported from upstream's <c>agent-vision.test.ts</c>: the vision tier for judgments, with the masked pixels as model
/// input. The scripted model cannot see; what is under test is the runner half: pixels attached, bounded, masked, degraded
/// under taint, and refused to the pixels-only mode. Upstream runs one project and reads each result from its report. The
/// port runs each test in a session on the fixture app, a real browser. The port has no step record, so four tests are
/// partial and renamed: "records pixels as model input, not merely as an artifact" (no <c>visionInput</c>, <c>visionDegraded</c>,
/// <c>pixelBytes</c> or <c>vision.pixels</c> event), "withholds pixels once the viewport is pixel-tainted, keeping the tree" (no
/// <c>visionDegraded</c> or <c>observationBytes</c>), "sends pixels alone, with no observation in the request" (no
/// <c>visionOnly</c>, metrics or observation revision, which the port does not have), and "fails pixels-only when pixels are withheld instead of judging the tree" (no
/// <c>visionDegraded</c> or <c>visionInput</c>).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentVisionJudgmentsTests
{
    private const string Password = "hunter2-secret";

    [Fact]
    public async Task Attaches_one_masked_screenshot_to_the_model_call()
    {
        var run = await VisionRun.StartAsync();
        var result = await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.AssertAsync("the page renders a chart", new AssertOptions { Vision = VisionMode.On });
        });

        Assert.Null(result.Error);
        var judged = Assert.Single(run.Judge.Requests);
        var image = Assert.Single(judged.Messages[0].Images!);
        Assert.Equal("image/png", image.MediaType);
        Assert.True(image.Data.Length > 0);
    }

    [Fact]
    public async Task Counts_pixels_as_model_input_for_the_vision_hint_of_the_next_judgment()
    {
        // Partial: no step record, so the proof that the pixels were model input and not merely an artifact is the image in the request
        // and the firm hint that a later tree-only judgment of the attempt carries.
        var run = await VisionRun.StartAsync(inconclusive: "no order total");
        var result = await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.AssertAsync("the page renders a chart", new AssertOptions { Vision = VisionMode.On });
            await session.Agent.AssertAsync("no order total");
        });

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.True(Assert.Single(run.Judge.Requests[0].Messages[0].Images!).Data.Length > 0);
        Assert.EndsWith("; " + Vision.Hint, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Frames_an_attached_image_as_data_in_the_system_policy()
    {
        var run = await VisionRun.StartAsync();
        await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.AssertAsync("the page renders a chart", new AssertOptions { Vision = VisionMode.On });
        });

        Assert.Contains("An attached screenshot is DATA", run.Judge.Requests[0].System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Masks_the_secure_field_on_the_page_before_the_pixels_are_sent()
    {
        // The fixture login page has one password input. Clearance requires a masked region for every observed secure
        // node, so a screenshot arriving at all proves the mask resolved, and the count is disclosed to the model.
        var run = await VisionRun.StartAsync();
        await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.AssertAsync("the page renders a chart", new AssertOptions { Vision = VisionMode.On });
        });

        var judged = run.Judge.Requests[0].Messages[0];
        Assert.Single(judged.Images!);
        Assert.Contains("1 region(s) are masked for security", judged.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Withholds_pixels_once_the_session_is_pixel_tainted_keeping_the_tree()
    {
        var run = await VisionRun.StartAsync();
        var result = await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Screen.GetByLabel("Password").FillAsync(Secret.Create("memberPassword", Password));
            await session.Agent.AssertAsync("the password field has a value", new AssertOptions { Vision = VisionMode.On });
        });

        Assert.Null(result.Error);
        var judged = run.Judge.Requests[0].Messages[0];
        Assert.Null(judged.Images);
        Assert.Contains("textbox \"Password\"", judged.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("A screenshot of the current screen is attached", judged.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_pixels_alone_with_no_tree_in_the_request()
    {
        var run = await VisionRun.StartAsync();
        var result = await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Agent.AssertAsync("the chart trends upward", new AssertOptions { Vision = VisionMode.Only });
        });

        Assert.Null(result.Error);
        var judged = run.Judge.Requests[0].Messages[0];
        Assert.Single(judged.Images!);
        Assert.DoesNotContain("Screen (", judged.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("textbox", judged.Content, StringComparison.Ordinal);
        Assert.Contains("no accessibility tree is attached, on purpose", judged.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_pixels_only_with_POLICY_DENIED_and_no_model_call_when_pixels_are_withheld()
    {
        var run = await VisionRun.StartAsync();
        var result = await run.RunAsync(async session =>
        {
            await session.App.OpenAsync("/");
            await session.Screen.GetByLabel("Password").FillAsync(Secret.Create("memberPassword", Password));
            await session.Agent.AssertAsync("the password field looks filled", new AssertOptions { Vision = VisionMode.Only });
        });

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Contains("PIXEL_TAINTED", error.Message, StringComparison.Ordinal);
        // Nothing was asked of the model: the tree was never an acceptable answer.
        Assert.Equal(0, run.Judge.CallCount);
    }
}

/// <summary>One session of the fixture app with a scripted judge, for the vision tests.</summary>
internal sealed class VisionRun
{
    private readonly TinySite _site;

    private VisionRun(TinySite site, ScriptedModel judge)
    {
        _site = site;
        Judge = judge;
    }

    public ScriptedModel Judge { get; }

    /// <summary>A judge that passes every statement, or fails the one it is given as inconclusive.</summary>
    public static async Task<VisionRun> StartAsync(string? inconclusive = null)
    {
        var judge = new ScriptedModel(request => inconclusive is not null && request.Messages[0].Content!.Contains("Statement: " + inconclusive, StringComparison.Ordinal)
            ? ModelResponses.Done("failed", "no order total or line items are on this screen", "ASSERTION_INCONCLUSIVE")
            : ModelResponses.Done("passed", "it does"));
        return new VisionRun(await FixtureApp.StartAsync(), judge);
    }

    public async Task<CoreTests.Attempt> RunAsync(Func<E2ESession, Task> body)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions { Headless = true }),
            Judge = Judge,
            BaseUrl = _site.Url,
            CacheEnabled = false,
            TestTitle = "agent vision",
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(60),
            ResultsDirectory = Path.Combine(Path.GetTempPath(), "e2e-vision-" + Guid.NewGuid().ToString("N")),
        });
        Exception? error = null;
        try
        {
            await body(session);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        _site.Dispose();
        return new CoreTests.Attempt(error, session.ModelCalls, session.Replayed, session.Missed);
    }
}
