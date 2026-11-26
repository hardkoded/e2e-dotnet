// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorActionOptions;

/// <summary>
/// The "tap count" describe of <c>locator-action-options.test.ts</c>. Not ported: of "rejects count %j before any lookup, action, or step", the
/// values a <see cref="long"/> cannot hold (1.5, NaN, Infinity, 2 ** 53 and above is not a safe integer upstream but is one here, and the
/// string and null), because <see cref="TapOptions.Count"/> is a typed <c>long?</c>; 0 and -1 are checked.
/// </summary>
public sealed class LocatorActionOptionsTapCountTests
{
    private static readonly string[] SpiV1Kinds = [.. EngineActionKinds.Locator];

    /// <summary>A screen over the box whose engine declares <paramref name="actions"/>, keeping every action it receives and counting lookups.</summary>
    private static (Screen Screen, List<LocatorAction> Received, List<int> Locates) CountScreen(
        IReadOnlyCollection<string>? actions = null,
        bool tapModifiers = false
    )
    {
        var locates = new List<int>();
        var (screen, received) = LocatorActionOptionsTests.BoxScreen(tapModifiers, onObserve: () => locates.Add(1));
        screen.ActionKinds = actions;
        return (screen, received, locates);
    }

    [Fact]
    public async Task Taps_once_for_count_1_as_with_no_count()
    {
        var (screen, received, _) = CountScreen();

        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 1 });
        await screen.GetByLabel("Agree").ClickAsync(new TapOptions { Count = 1 });

        Assert.Equal<LocatorAction>([new LocatorAction.Tap(), new LocatorAction.Tap()], received);
        Assert.Equal(["getByLabel(\"Agree\")", "getByLabel(\"Agree\")"], screen.Steps!.All().Select(s => s.Label));
    }

    [Fact]
    public async Task Sends_count_2_as_the_action_doubleTap_sends_labelled_with_the_count()
    {
        var (screen, received, _) = CountScreen(SpiV1Kinds);

        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 2 });
        await screen.GetByLabel("Agree").DoubleTapAsync();

        Assert.Equal<LocatorAction>([new LocatorAction.DoubleTap(), new LocatorAction.DoubleTap()], received);
        var steps = screen.Steps!.All();
        Assert.Equal(
            [
                ("locator.tap", "getByLabel(\"Agree\") 2 times", StepStatus.Passed),
                ("locator.doubleTap", "getByLabel(\"Agree\")", StepStatus.Passed),
            ],
            steps.Select(s => (s.Api, s.Label, s.Status))
        );
    }

    [Theory]
    [InlineData("tap")]
    [InlineData("click")]
    public async Task Sends_count_3_as_one_multiTap_and_names_the_count_in_the_step(string verb)
    {
        var (screen, received, _) = CountScreen();
        var box = screen.GetByLabel("Agree");
        var options = new TapOptions { Count = 3 };

        await (verb == "tap" ? box.TapAsync(options) : box.ClickAsync(options));

        Assert.Equal<LocatorAction>([new LocatorAction.MultiTap(3)], received);
        var step = Assert.Single(screen.Steps!.All());
        Assert.Equal(
            ($"locator.{verb}", "getByLabel(\"Agree\") 3 times", StepStatus.Passed),
            (step.Api, step.Label, step.Status)
        );
    }

    [Fact]
    public async Task Holds_modifiers_for_a_multiTap_on_an_engine_that_declares_tapModifiers()
    {
        var (screen, received, _) = CountScreen(tapModifiers: true);

        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 3, Modifiers = [KeyModifier.Shift] });
        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 2, Modifiers = [KeyModifier.Shift] });

        Assert.Equal(2, received.Count);
        var multi = Assert.IsType<LocatorAction.MultiTap>(received[0]);
        Assert.Equal(3, multi.Count);
        Assert.Equal([KeyModifier.Shift], multi.Modifiers);
        Assert.Equal([KeyModifier.Shift], Assert.IsType<LocatorAction.DoubleTap>(received[1]).Modifiers);
        Assert.Equal(
            ["getByLabel(\"Agree\") 3 times with Shift", "getByLabel(\"Agree\") 2 times with Shift"],
            screen.Steps!.All().Select(s => s.Label)
        );
    }

    [Fact]
    public async Task Refuses_count_3_on_an_engine_without_multiTap_before_resolving_while_count_2_still_works_there()
    {
        var (screen, received, locates) = CountScreen(SpiV1Kinds);

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            screen.GetByLabel("Missing").TapAsync(new TapOptions { Count = 3 })
        );

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Contains("the \"multiTap\" action is not available on this target", error.Message);
        Assert.Empty(locates);
        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 2 });
        Assert.Equal<LocatorAction>([new LocatorAction.DoubleTap()], received);
    }

    [Fact]
    public async Task Leaves_the_upper_limit_to_the_engine_sending_the_largest_safe_count_as_one_multiTap()
    {
        var (screen, received, _) = CountScreen();

        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = long.MaxValue });

        Assert.Equal<LocatorAction>([new LocatorAction.MultiTap(long.MaxValue)], received);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Rejects_count_x_before_any_lookup_action_or_step(long count)
    {
        var (screen, received, locates) = CountScreen();

        var error = await Assert.ThrowsAsync<TestException>(() =>
            screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = count })
        );

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.StartsWith("tap() count must be a positive safe integer, got ", error.Message);
        Assert.Empty(locates);
        Assert.Empty(received);
        Assert.Empty(screen.Steps!.All());
    }

    [Fact]
    public async Task Refuses_a_count_above_1_with_a_position_which_the_pointer_path_cannot_repeat_yet()
    {
        var (screen, received, _) = CountScreen();

        var error = await Assert.ThrowsAsync<TestException>(() =>
            screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 2, Position = new Point(1, 1) })
        );

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Contains("count with a position", error.Message);
        Assert.Empty(received);
    }

    [Fact]
    public async Task Still_taps_at_the_point_for_count_1_with_a_position_the_default_count()
    {
        var box = new SemanticNode
        {
            Ref = "agree",
            Role = "checkbox",
            Name = "Agree",
            Rect = new BoundingBox(10, 20, 100, 40),
        };
        var received = new List<LocatorAction>();
        var at = new List<string>();
        var screen = new Screen(
            new global::E2E.LocatorEngine(
                new global::E2E.LocatorEngineOptions
                {
                    Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = [box] }),
                    Perform = (_, action, _, _) =>
                    {
                        received.Add(action);
                        return Task.CompletedTask;
                    },
                    Cancellation = () => CancellationToken.None,
                    ActionTimeout = TimeSpan.FromSeconds(1),
                    AssertionTimeout = TimeSpan.FromSeconds(1),
                    Locate = (_, _, _) => Task.FromResult<IReadOnlyList<SemanticNode>>([box]),
                    PerformAt = (point, action, _, _) =>
                    {
                        at.Add($"{EngineActionKinds.Of(action)} @ {point.X},{point.Y}");
                        return Task.CompletedTask;
                    },
                }
            ),
            () => { }
        )
        {
            PointerActionKinds = ["tap"],
            Steps = new StepRecorder("attempt"),
        };

        await screen.GetByLabel("Agree").TapAsync(new TapOptions { Count = 1, Position = new Point(1, 1) });

        Assert.Equal(["tap @ 11,21"], at);
        Assert.DoesNotContain(received, action => action is not LocatorAction.ScrollIntoView);
        var step = Assert.Single(screen.Steps!.All());
        Assert.Equal(
            ("locator.tap", "getByLabel(\"Agree\") at (1, 1)", StepStatus.Passed),
            (step.Api, step.Label, step.Status)
        );
    }
}
