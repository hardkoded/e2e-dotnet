// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ScrollTo;

/// <summary>
/// Upstream's <c>describe('scrollTo')</c> of <c>scroll-to.test.ts</c>: <c>screen.scrollTo</c> and <c>locator.scrollTo</c> over a fake engine.
/// Not ported here: "gives the scroll only what the first observation left of the timeout" and "keeps a first observation that outlasts the timeout
/// as what it is, and scrolls nothing", because the port's screen has no <c>rootRef</c> step to observe in (an engine observes for itself); the
/// swipe pair ("gives a first screen swipe only what the observation left of the timeout", "sends no screen swipe once the first observation spent
/// the timeout") is in <c>CreateEngineSessionTests</c>. Of "refuses the edge %j before any lookup, action, or step" the values an enum cannot hold
/// ('middle', 'bottomRight', 0, undefined) are not ported: <see cref="ScrollEdge"/> is typed, so the undefined members of the enum are checked.
/// "refuses an option it does not take on the screen" is not ported: <see cref="ActionOptions"/> is a typed class.
/// </summary>
public sealed class ScrollToTests
{
    private static readonly SemanticNode Title = new()
    {
        Ref = "title",
        Role = "heading",
        Name = "Inbox",
    };

    private static readonly SemanticNode Feed = new()
    {
        Ref = "feed",
        Role = "list",
        Name = "Feed",
        TestId = "feed",
    };

    /// <summary>A screen over one list, keeping every action and the node it was addressed to.</summary>
    private sealed class FeedScreen
    {
        public FeedScreen(
            IReadOnlyCollection<string>? actions = null,
            Func<LocatorAction, Task>? perform = null,
            Func<Task<IReadOnlyList<SemanticNode>>>? relocate = null,
            TimeSpan? timeout = null
        )
        {
            var locates = 0;
            Screen = new Screen(
                _ =>
                {
                    Observes++;
                    return Task.FromResult(new Observation { Route = "/", Roots = [Title, Feed] });
                },
                async (node, action, _) =>
                {
                    Received.Add((node.Ref, action));
                    if (perform is not null)
                    {
                        await perform(action);
                    }
                },
                () => CancellationToken.None,
                () => { },
                timeout ?? TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                locate: relocate is null
                    ? null
                    : (_, _, _) =>
                    {
                        Locates++;
                        return ++locates > 1 ? relocate() : Task.FromResult<IReadOnlyList<SemanticNode>>([Feed]);
                    },
                scrollTo: async (edge, _) =>
                {
                    var action = new LocatorAction.ScrollTo(edge);
                    Received.Add(("root", action));
                    if (perform is not null)
                    {
                        await perform(action);
                    }
                }
            )
            {
                Steps = new StepRecorder("attempt"),
                ActionKinds = actions,
            };
        }

        public Screen Screen { get; }

        public List<(string Ref, LocatorAction Action)> Received { get; } = [];

        public int Locates { get; private set; }

        public int Observes { get; private set; }
    }

    [Fact]
    public async Task Scrolls_the_screen_through_the_observation_root_one_step_named_by_the_edge()
    {
        var feed = new FeedScreen();

        await feed.Screen.ScrollToAsync(ScrollEdge.Bottom);

        Assert.Equal([("root", (LocatorAction)new LocatorAction.ScrollTo(ScrollEdge.Bottom))], feed.Received);
        var step = Assert.Single(feed.Screen.Steps!.All());
        Assert.Equal(("screen.scrollTo", "bottom", StepStatus.Passed), (step.Api, step.Label, step.Status));
    }

    [Fact]
    public async Task Keeps_a_locate_that_times_out_after_a_stale_retry_as_the_locate_failure_not_the_edge_not_reached()
    {
        var performs = 0;
        var feed = new FeedScreen(
            perform: _ =>
                ++performs == 1
                    ? throw new EngineException(EngineErrorCodes.NodeStale, "replaced", retryable: true)
                    : Task.CompletedTask,
            relocate: () => throw new EngineException(EngineErrorCodes.OperationTimeout, "locate timed out")
        );

        var failure = await Record.ExceptionAsync(() =>
            feed.Screen.GetByTestId("feed").ScrollToAsync(ScrollEdge.Bottom)
        );

        Assert.Equal(1, performs);
        Assert.NotNull(failure);
        Assert.DoesNotContain("did not reach the edge", failure.Message);
    }

    [Fact]
    public async Task Scrolls_a_located_node_its_step_naming_the_node_and_the_edge()
    {
        var feed = new FeedScreen();

        await feed
            .Screen.GetByTestId("feed")
            .ScrollToAsync(ScrollEdge.Left, new ActionOptions { Timeout = TimeSpan.FromMilliseconds(500) });

        Assert.Equal([("feed", (LocatorAction)new LocatorAction.ScrollTo(ScrollEdge.Left))], feed.Received);
        var step = Assert.Single(feed.Screen.Steps!.All());
        Assert.Equal(
            ("locator.scrollTo", "getByTestId(\"feed\") → left", StepStatus.Passed),
            (step.Api, step.Label, step.Status)
        );
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task Refuses_the_edge_x_before_any_lookup_action_or_step(int edge)
    {
        var feed = new FeedScreen();

        foreach (
            var call in new Func<Task>[]
            {
                () => feed.Screen.ScrollToAsync((ScrollEdge)edge),
                () => feed.Screen.GetByTestId("feed").ScrollToAsync((ScrollEdge)edge),
            }
        )
        {
            var failure = await Assert.ThrowsAsync<TestException>(call);
            Assert.Equal("INVALID_ARGUMENT", failure.Code);
            Assert.StartsWith("scrollTo takes an edge: 'top', 'bottom', 'left', 'right'; got ", failure.Message);
        }

        Assert.Equal(0, feed.Locates);
        Assert.Equal(0, feed.Observes);
        Assert.Empty(feed.Received);
        Assert.Empty(feed.Screen.Steps!.All());
    }

    [Fact]
    public async Task Is_UNSUPPORTED_CAPABILITY_on_an_engine_that_does_not_declare_it_before_any_lookup()
    {
        var feed = new FeedScreen(actions: EngineActionKinds.Locator);

        var screenFailure = await Assert.ThrowsAsync<EngineException>(() =>
            feed.Screen.ScrollToAsync(ScrollEdge.Bottom)
        );
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, screenFailure.Code);
        Assert.Contains("the \"scrollTo\" action is not available on this target", screenFailure.Message);
        var locatorFailure = await Assert.ThrowsAnyAsync<E2EException>(() =>
            feed.Screen.GetByTestId("feed").ScrollToAsync(ScrollEdge.Bottom)
        );
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, locatorFailure.Code);

        Assert.Equal(0, feed.Locates);
        Assert.Equal(0, feed.Observes);
        Assert.Empty(feed.Received);
    }

    [Theory]
    [InlineData(EngineErrorCodes.OperationTimeout)]
    [InlineData(EngineErrorCodes.ActionMayHaveCommitted)]
    public async Task Says_the_edge_was_not_reached_when_the_engine_reports_its_cut_budget_as_x(string code)
    {
        var feed = new FeedScreen(
            perform: async _ =>
            {
                await Task.Delay(120);
                throw new EngineException(code, "still moving", retryable: false);
            },
            timeout: TimeSpan.FromMilliseconds(50)
        );

        var screenFailure = await Assert.ThrowsAsync<TestException>(() => feed.Screen.ScrollToAsync(ScrollEdge.Bottom));
        Assert.Equal("ACTION_FAILED", screenFailure.Code);
        Assert.Matches(
            @"^scrollTo\('bottom'\) did not reach the edge within \d+ms \(still moving\)\z",
            screenFailure.Message
        );
        var locatorFailure = await Assert.ThrowsAsync<TestException>(() =>
            feed.Screen.GetByTestId("feed").ScrollToAsync(ScrollEdge.Top)
        );
        Assert.Equal("ACTION_FAILED", locatorFailure.Code);
        Assert.Matches(
            @"^scrollTo\('top'\) did not reach the edge within \d+ms \(still moving\): getByTestId\(""feed""\)\z",
            locatorFailure.Message
        );
    }

    [Fact]
    public async Task Says_the_edge_was_not_reached_when_the_engine_stops_before_the_deadline_as_engines_leave_themselves_a_margin()
    {
        var feed = new FeedScreen(perform: _ =>
            throw new EngineException(
                EngineErrorCodes.OperationTimeout,
                "the content was still moving after 4 drags when the budget ran out",
                retryable: false
            )
        );

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            feed.Screen.GetByTestId("feed").ScrollToAsync(ScrollEdge.Bottom)
        );

        Assert.Equal("ACTION_FAILED", failure.Code);
        Assert.Matches(
            @"^scrollTo\('bottom'\) did not reach the edge within 1000ms \(the content was still moving after 4 drags when the budget ran out\): getByTestId\(""feed""\)\z",
            failure.Message
        );
    }

    [Fact]
    public async Task Keeps_a_locate_that_timed_out_before_anything_was_sent_as_the_locate_failure()
    {
        var feed = new FeedScreen(timeout: TimeSpan.FromMilliseconds(50));

        var failure = await Assert.ThrowsAsync<TestException>(() =>
            feed.Screen.GetByTestId("missing").ScrollToAsync(ScrollEdge.Bottom)
        );

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Empty(feed.Received);
    }

    [Fact]
    public async Task Refuses_a_frames_screen_whose_root_is_the_screen_around_the_frame()
    {
        var feed = new FeedScreen();
        var frame = new Screen(feed.Screen, expression => expression);

        var failure = await Assert.ThrowsAsync<TestException>(() => frame.ScrollToAsync(ScrollEdge.Bottom));

        Assert.Equal("INVALID_ARGUMENT", failure.Code);
        Assert.Contains("call it on a locator inside the frame", failure.Message);
        Assert.Empty(feed.Received);
    }

    [Fact]
    public async Task Keeps_an_engine_failure_that_is_not_the_deadline_as_the_engine_reported_it()
    {
        var feed = new FeedScreen(perform: _ =>
            throw new EngineException(
                EngineErrorCodes.NotActionable,
                "the soft keyboard covers everything there is to scroll",
                retryable: false
            )
        );

        var failure = await Assert.ThrowsAsync<TestException>(() => feed.Screen.ScrollToAsync(ScrollEdge.Bottom));

        Assert.Equal("ACTION_FAILED", failure.Code);
        Assert.Equal("the soft keyboard covers everything there is to scroll", failure.Message);
    }
}
