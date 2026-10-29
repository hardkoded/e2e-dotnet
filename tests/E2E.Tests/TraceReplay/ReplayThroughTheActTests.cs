// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.StepCache.StepCachePipelineTests;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>replayTrace</c> through a whole act. Upstream drives <c>replayTrace</c> with a fake host (<see cref="ReplayTraceTests"/>);
/// these tests record a flow on a document page, replay it, and read what the act made of it: the looks it took, and the cache
/// result it reports. They are partial, because they check the act's wiring of the replay (its looks, its action slots, its
/// hand-off report) and not <c>ReplayOutcome</c>.
/// </summary>
public sealed class ReplayThroughTheActTests
{
    [Fact]
    public async Task Partial_Reads_the_start_capture_for_the_first_look_instead_of_observing_the_same_screen_again()
    {
        var looks = await RecordThenReplayAsync(TapWorld, ModelResponses.Tap("button", "Upgrade"));
        Assert.Equal([SettleMode.HeldStill, SettleMode.Raw], looks);
    }

    [Fact]
    public async Task Partial_Settles_each_look_as_far_as_the_previous_actions_policy_asks_held_still_after_a_tap_after_the_change_after_a_fill()
    {
        var tap = ModelResponses.Tap("button", "Upgrade");
        var looks = await RecordThenReplayAsync(
            TapWorld,
            tap,
            ModelResponses.Fill("textbox", "Email", "a@b.c"),
            tap,
            ModelResponses.Call("fill_secret", new { role = "textbox", name = "Password", secret = "password" }),
            tap,
            ModelResponses.Call("select", new { role = "combobox", name = "Plan", value = "Pro" }),
            tap);

        // The start holds still and serves the first tap; so does the look after each tap or select. The look
        // after a typed fill waits for its value only. A secret fill is masked out of the tree, so the look after
        // it holds still: there is no change to wait for.
        Assert.Equal(
            [
                SettleMode.HeldStill,
                SettleMode.HeldStill,
                SettleMode.AfterChange,
                SettleMode.HeldStill,
                SettleMode.HeldStill,
                SettleMode.HeldStill,
                SettleMode.HeldStill,
                SettleMode.Raw,
            ],
            looks);
    }

    [Fact]
    public async Task Partial_Takes_one_look_per_repeat_of_a_folded_scroll_on_a_list()
    {
        var looks = await RecordThenReplayAsync(ScrollWorld, ModelResponses.Call("scroll", new { role = "group", name = "Rows 1 to 12", direction = "down", times = 3 }));
        Assert.Equal([SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.Raw], looks);
    }

    /// <summary>Partial: the act records the list's share of the viewport and replays without the list; the replay's own checks are <c>ReplayTraceBarePointTapsTests</c>.</summary>
    [Fact]
    public async Task Partial_Scrolls_the_viewport_when_a_list_that_filled_the_screen_cannot_be_re_found()
    {
        var directory = CoreTests.TempCache();
        var next = 0;
        ModelResponse[] steps = [ModelResponses.Call("scroll", new { role = "group", name = "Rows 1 to 12", direction = "down", times = 2 })];
        DocumentWorld World(bool list) => new DocumentWorld().Map("/plan", page =>
        {
            var scrolls = 0;
            var status = page.Status("Scrolled 0");
            if (list)
            {
                page.Roots.Add(new DocumentElement { Role = "group", Name = "Rows 1 to 12", Rect = new BoundingBox(0, 0, 1280, 720), OnScroll = _ => status.Name = status.Text = "Scrolled " + ++scrolls });
            }

            page.OnScroll = _ => status.Name = status.Text = "Scrolled " + ++scrolls;
        });

        var recorded = await ActAsync(World(list: true), directory, new ScriptedModel(_ => next < steps.Length ? steps[next++] : ModelResponses.Done("passed", "Done.")));
        Assert.Equal("missed", recorded.Mode);
        var scroll = Assert.Single(ScrollTests.ReadEntry(directory).Actions);
        Assert.Equal((2, 1.0), (scroll.Times, scroll.Spans));
        // The recording keeps how long the app took to show its end state after the last action, plus the margin a replay waits.
        Assert.InRange(ScrollTests.ReadEntry(directory).EndWaitMs ?? -1, 10_000, 15_000);

        // The list is gone from the page, but it covered the screen: the viewport takes its scrolls.
        var replayed = await ActAsync(World(list: false), directory, new ScriptedModel(_ => throw new InvalidOperationException("no model call expected")));
        Assert.Equal("self-finalized", replayed.Mode);
    }

    [Fact]
    public async Task Partial_Repeats_a_folded_viewport_scroll_as_many_times_as_recorded_with_a_settled_look_between_repeats()
    {
        var looks = await RecordThenReplayAsync(ScrollWorld, ModelResponses.Call("scroll", new { direction = "down", times = 4 }));

        // Nothing to relocate, so the first repeat takes no look; each later one holds still first.
        Assert.Equal([SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.Raw], looks);
    }

    private static readonly ModelResponse[] NavigateThenUpgrade = [ModelResponses.Call("navigate", new { url = "/plan" }), ModelResponses.Tap("button", "Upgrade")];

    private static Task<Step> ReplayStepAsync(DocumentWorld world, string directory, Func<TestContext, Task>? check, params ModelResponse[] steps)
    {
        var next = 0;
        var model = new ScriptedModel(_ => next < steps.Length ? steps[next++] : ModelResponses.Done("passed", "Done."));
        return StepAsync(world, new FileStepCache(directory), "/home", check, model);
    }

    private static async Task<string> RecordAsync(Func<DocumentWorld> world, params ModelResponse[] steps)
    {
        var directory = CoreTests.TempCache();
        var recorded = await ReplayStepAsync(world(), directory, ctx => Expect.That(ctx.Screen.GetByRole("status")).ToBeVisibleAsync(), steps);
        Assert.Null(recorded.Error);
        Assert.Equal("missed", recorded.Cache?.Mode);
        return directory;
    }

    [Fact]
    public async Task Partial_Still_diverges_at_once_on_ambiguity_for_a_target_with_no_recorded_position()
    {
        var tapped = 0;
        var directory = await RecordAsync(
            () => new DocumentWorld().Map("/home", page =>
            {
                var status = page.Status("Idle");
                page.Button("Upgrade", () => status.Name = status.Text = "Upgraded " + ++tapped);
            }),
            ModelResponses.Tap("button", "Upgrade"));
        tapped = 0;
        var twins = new DocumentWorld().Map("/home", page =>
        {
            page.Status("Idle");
            page.Button("Upgrade", () => tapped++);
            page.Button("Upgrade", () => tapped++);
        });

        var replay = await ReplayStepAsync(twins, directory, null, ModelResponses.Done("passed", "Done."));

        Assert.Equal(("missed", "target-ambiguous", 0), (replay.Cache?.Mode, replay.Cache?.Reason, replay.Cache?.ReplayedActions));
        Assert.Equal(0, tapped);
    }

    [Fact]
    public async Task Partial_Absorbs_an_action_failure_as_divergence_never_as_a_step_failure()
    {
        var failing = false;
        DocumentWorld World() => new DocumentWorld().Map("/home", page => page.Heading("Home")).Map("/plan", page =>
        {
            var status = page.Status("Idle");
            page.Button("Upgrade", () =>
            {
                if (failing)
                {
                    throw new EngineException("ACTION_FAILED", "element detached");
                }

                status.Name = status.Text = "Upgraded";
            });
        });
        var directory = await RecordAsync(World, NavigateThenUpgrade);
        failing = true;

        var replay = await ReplayStepAsync(World(), directory, null, ModelResponses.Done("passed", "Done."));

        Assert.Null(replay.Error);
        Assert.Equal(("agent-concluded", "action-failed", 1), (replay.Cache?.Mode, replay.Cache?.Reason, replay.Cache?.ReplayedActions));
    }

    /// <summary>Reads the uncertain action as the hand-off notice words it, and the reason from the act's cache detail. <c>ReplayTraceTests</c> checks the outcome.</summary>
    [Fact]
    public async Task Names_the_uncertain_action_in_the_hand_off_notice()
    {
        var uncertain = false;
        DocumentWorld World() => new DocumentWorld().Map("/home", page => page.Heading("Home")).Map("/plan", page =>
        {
            var status = page.Status("Idle");
            page.Button("Upgrade", () =>
            {
                if (uncertain)
                {
                    throw new EngineException(EngineErrorCodes.ActionMayHaveCommitted, "input may have reached the app");
                }

                status.Name = status.Text = "Upgraded";
            });
        });
        var directory = await RecordAsync(World, NavigateThenUpgrade);
        uncertain = true;
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "Done."));

        var replay = await StepAsync(World(), new FileStepCache(directory), "/home", null, model);

        Assert.Null(replay.Error);
        Assert.Equal(("agent-concluded", "action-uncertain", 1), (replay.Cache?.Mode, replay.Cache?.Reason, replay.Cache?.ReplayedActions));
        Assert.Contains("tap button \"Upgrade\"", replay.Cache?.Detail, StringComparison.Ordinal);
        var opening = model.Requests[0].Messages[0].Content!;
        Assert.Contains("Replay stopped (action-uncertain) after these actions:", opening, StringComparison.Ordinal);
        Assert.Contains("WARNING: the next action (tap button \"Upgrade\") failed with an UNKNOWN commit state", opening, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Rethrows_runtime_hard_stops_untouched()
    {
        var exhausted = false;
        DocumentWorld World() => new DocumentWorld().Map("/home", page => page.Heading("Home")).Map("/plan", page =>
        {
            var status = page.Status("Idle");
            page.Button("Upgrade", () =>
            {
                if (exhausted)
                {
                    throw new AgentException("STEP_BUDGET_EXHAUSTED", "out of actions");
                }

                status.Name = status.Text = "Upgraded";
            });
        });
        var directory = await RecordAsync(World, NavigateThenUpgrade);
        exhausted = true;

        var replay = await ReplayStepAsync(World(), directory, null, ModelResponses.Done("passed", "Done."));

        Assert.Equal("STEP_BUDGET_EXHAUSTED", Assert.IsType<AgentException>(replay.Error).Code);
    }

    [Fact]
    public async Task Partial_Hands_off_when_a_smaller_scrolled_region_cannot_be_re_found_never_scrolling_the_viewport_for_it()
    {
        var scrolled = 0;
        var directory = await RecordAsync(
            () => new DocumentWorld().Map("/home", page =>
            {
                var status = page.Status("Idle");
                page.Roots.Add(new DocumentElement { Role = "group", Name = "Recommended", OnScroll = _ => status.Name = status.Text = "Scrolled " + ++scrolled });
            }),
            ModelResponses.Call("scroll", new { role = "group", name = "Recommended", direction = "right" }));
        scrolled = 0;
        var viewportScrolls = 0;
        var without = new DocumentWorld().Map("/home", page =>
        {
            page.Button("Upgrade");
            page.OnScroll = _ => viewportScrolls++;
        });

        var replay = await ReplayStepAsync(without, directory, null, ModelResponses.Done("passed", "Done."));

        Assert.Equal(("missed", "target-not-found", 0), (replay.Cache?.Mode, replay.Cache?.Reason, replay.Cache?.ReplayedActions));
        Assert.Equal((0, 0), (scrolled, viewportScrolls));
    }

    private static DocumentWorld Signup() => new DocumentWorld().Map("/signup", page => page.Textbox("Email"));

    private static ScriptedModel FillsEmail(string email) => new(request =>
        string.Join('\n', request.Messages.Select(message => message.Content)).Contains("filled", StringComparison.Ordinal)
            ? ModelResponses.Done("passed", "filled")
            : ModelResponses.Fill("textbox", "Email", email));

    private static Task<CoreTests.Attempt> SignupAsync(string directory, IAgentModel model, Func<TestContext, ActResult, Task> after, IReadOnlyDictionary<string, object?> parameters) =>
        CoreTests.RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/signup");
                var result = await ctx.Agent.ActAsync("sign up with {email}", new ActOptions { Params = parameters });
                await after(ctx, result);
            },
            Signup(),
            model,
            directory);

    /// <summary>A recorded value that a param expands to a secret's marker fills that secret, as replay always did.</summary>
    [Fact]
    public async Task A_param_that_expands_to_a_secret_marker_fills_the_declared_secret()
    {
        var directory = CoreTests.TempCache();
        IReadOnlyDictionary<string, object?> Params(string email) => new Dictionary<string, object?> { ["email"] = Values.Unique(email), ["password"] = Secret.Create("password", "hunter2-long") };
        var first = await SignupAsync(directory, FillsEmail("ada+1@example.test"), (ctx, _) => Expect.That(ctx.Screen.GetByLabel("Email")).ToHaveValueAsync("ada+1@example.test"), Params("ada+1@example.test"));
        Assert.Null(first.Error);

        var second = await SignupAsync(directory, FillsEmail("unused"), (ctx, _) => Expect.That(ctx.Screen.GetByLabel("Email")).ToHaveValueAsync("hunter2-long"), Params("<secret:password>"));

        Assert.Null(second.Error);
        Assert.Equal((1, 0), (second.Replayed, second.ModelCalls));
    }

    [Fact]
    public async Task A_literal_value_is_typed_as_recorded()
    {
        var directory = CoreTests.TempCache();
        var none = new Dictionary<string, object?>();
        var first = await SignupAsync(directory, FillsEmail("plain@example.test"), (ctx, _) => Expect.That(ctx.Screen.GetByLabel("Email")).ToHaveValueAsync("plain@example.test"), none);
        Assert.Null(first.Error);

        var second = await SignupAsync(directory, FillsEmail("unused"), (ctx, _) => Expect.That(ctx.Screen.GetByLabel("Email")).ToHaveValueAsync("plain@example.test"), none);

        Assert.Null(second.Error);
        Assert.Equal((1, 0), (second.Replayed, second.ModelCalls));
    }

    [Fact]
    public async Task A_secret_marker_the_step_did_not_declare_hands_off_as_action_failed()
    {
        var directory = CoreTests.TempCache();
        IReadOnlyDictionary<string, object?> Params(string email) => new Dictionary<string, object?> { ["email"] = Values.Unique(email) };
        var first = await SignupAsync(directory, FillsEmail("ada+1@example.test"), (ctx, _) => Expect.That(ctx.Screen.GetByLabel("Email")).ToHaveValueAsync("ada+1@example.test"), Params("ada+1@example.test"));
        Assert.Null(first.Error);
        CacheInfo? cache = null;

        var second = await SignupAsync(directory, new ScriptedModel(_ => ModelResponses.Done("passed", "ok")), (_, result) =>
        {
            cache = result.Cache;
            return Task.CompletedTask;
        }, Params("<secret:absent>"));

        Assert.Null(second.Error);
        Assert.Equal(("agent-concluded", "action-failed"), (cache?.Mode, cache?.Reason));
        Assert.Contains("Replay needs secret 'absent'", cache?.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_scroll_to_text_replays_on_the_list_it_was_recorded_on_or_on_the_viewport(bool onList)
    {
        var directory = CoreTests.TempCache();
        async Task Body(TestContext ctx)
        {
            await ctx.App.OpenAsync("/rows");
            await ctx.Agent.ActAsync("open row 12");
            await Expect.That(ctx.Screen.GetByRole("status")).ToContainTextAsync("Opened Row 12");
        }

        object scroll = onList ? new { text = "Row 12", role = "list", name = "Rows" } : new { text = "Row 12" };
        var first = await ScrollTests.RunAsync(Body, ScrollTests.Rows(), ScrollTests.Sequence(ModelResponses.Call("scroll_to", scroll), ModelResponses.Tap("button", "Row 12"), ModelResponses.Done("passed", "opened")), directory);
        Assert.Null(first.Error);
        Assert.Equal(onList ? "list" : null, ScrollTests.ReadEntry(directory).Actions[0].Role);

        var second = await ScrollTests.RunAsync(Body, ScrollTests.Rows(), ScrollTests.Sequence(), directory);

        Assert.Null(second.Error);
        Assert.Equal((1, 0), (second.Replayed, second.ModelCalls));
    }

    // Every action changes what the page shows, so no look waits out a change window.
    private static DocumentWorld TapWorld() => new DocumentWorld().Map("/plan", page =>
    {
        var taps = 0;
        var status = page.Status("Taps 0");
        page.Button("Upgrade", () => status.Name = status.Text = "Taps " + ++taps);
        page.Textbox("Email");
        page.Textbox("Password", secure: true);
        page.Roots.Add(new DocumentElement { Role = "combobox", Name = "Plan", Value = "Free" });
    });

    private static DocumentWorld ScrollWorld() => new DocumentWorld().Map("/plan", page =>
    {
        var scrolls = 0;
        var status = page.Status("Scrolled 0");
        page.Roots.Add(new DocumentElement { Role = "group", Name = "Rows 1 to 12", OnScroll = _ => status.Name = status.Text = "Scrolled " + ++scrolls });
        page.OnScroll = _ => status.Name = status.Text = "Scrolled " + ++scrolls;
    });

    /// <summary>Records the steps live on a fresh cache, then replays them, and returns the replaying act's looks.</summary>
    private static async Task<IReadOnlyList<SettleMode>> RecordThenReplayAsync(Func<DocumentWorld> world, params ModelResponse[] steps)
    {
        var directory = CoreTests.TempCache();
        var next = 0;
        var recorded = await ActAsync(world(), directory, new ScriptedModel(_ => next < steps.Length ? steps[next++] : ModelResponses.Done("passed", "Done.")));
        Assert.Equal("missed", recorded.Mode);

        var replayed = await ActAsync(world(), directory, new ScriptedModel(_ => throw new InvalidOperationException("no model call expected")));
        Assert.Equal("self-finalized", replayed.Mode);
        return replayed.Looks;
    }

    private static async Task<(string? Mode, IReadOnlyList<SettleMode> Looks)> ActAsync(DocumentWorld world, string directory, IAgentModel model)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            Model = model,
            BaseUrl = "https://billing.test",
            Cache = new FileStepCache(directory),
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "replay > looks",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(30),
            StepTimeout = TimeSpan.FromSeconds(20),
        });
        Exception? error = null;
        string? mode = null;
        try
        {
            await session.Context.App.OpenAsync("/plan");
            var parameters = new Dictionary<string, object?> { ["password"] = Secret.Create("password", "hunter2") };
            mode = (await session.Context.Agent.ActAsync("work the plan page", new ActOptions { Params = parameters })).Cache?.Mode;

            // A passing check verifies the act, so its recording is written.
            await Expect.That(session.Context.Screen.GetByRole("heading", "Missing")).ToBeHiddenAsync();
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        Assert.Null(error);
        return (mode, [.. session.Context.Agent.Feed.Looks]);
    }
}
