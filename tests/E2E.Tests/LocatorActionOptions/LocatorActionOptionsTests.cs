// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorActionOptions;

/// <summary>
/// "%s rejects an unknown option before any lookup, action, or step" is not ported: the port's options are a typed
/// class. "%s performs with a timeout" runs for every verb the port has (not <c>setInputFiles</c>, which the file's
/// other tests do not reach either). The nested "modifiers" describe is <see cref="LocatorActionOptionsModifiersTests"/>.
/// </summary>
public sealed class LocatorActionOptionsTests
{
    private static readonly SemanticNode Box = new() { Ref = "agree", Role = "checkbox", Name = "Agree" };

    private static readonly SemanticNode Bin = new() { Ref = "bin", Role = "region", Name = "Bin" };

    private static readonly Dictionary<string, Func<Locator, ActionOptions, Screen, Task>> Calls = new()
    {
        ["tap"] = (locator, options, _) => locator.TapAsync(options),
        ["click"] = (locator, options, _) => locator.ClickAsync(options),
        ["doubleTap"] = (locator, options, _) => locator.DoubleTapAsync(options),
        ["dblclick"] = (locator, options, _) => locator.DblClickAsync(options),
        ["secondaryTap"] = (locator, options, _) => locator.SecondaryTapAsync(new ClickOptions { Timeout = options.Timeout }),
        ["longPress"] = (locator, options, _) => locator.LongPressAsync(new LongPressOptions { Timeout = options.Timeout }),
        ["fill"] = (locator, options, _) => locator.FillAsync("yes", options),
        ["clear"] = (locator, options, _) => locator.ClearAsync(options),
        ["press"] = (locator, options, _) => locator.PressAsync("Enter", options),
        ["check"] = (locator, options, _) => locator.CheckAsync(options),
        ["uncheck"] = (locator, options, _) => locator.UncheckAsync(options),
        ["selectOption"] = (locator, options, _) => locator.SelectOptionAsync("Team", options),
        ["focus"] = (locator, options, _) => locator.FocusAsync(options),
        ["hover"] = (locator, options, _) => locator.HoverAsync(options),
        ["dragTo"] = (locator, options, screen) => locator.DragToAsync(screen.GetByRole("region", "Bin"), options),
        ["scrollIntoView"] = (locator, options, _) => locator.ScrollIntoViewAsync(options),
        ["swipe"] = (locator, options, _) => locator.SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Down, Timeout = options.Timeout }),
    };

    public static TheoryData<string> Verbs => new(Calls.Keys);

    /// <summary>A screen over a checkbox and a drop region that keeps every action the engine receives. Modifiers are held when <paramref name="tapModifiers"/> is true.</summary>
    internal static (Screen Screen, List<LocatorAction> Received) BoxScreen(bool tapModifiers = false)
    {
        var received = new List<LocatorAction>();
        var observation = new Observation { Route = "/", Roots = [Box, Bin] };
        var screen = new Screen(
            _ => Task.FromResult(observation),
            (_, action, _) =>
            {
                received.Add(action);
                return Task.CompletedTask;
            },
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1))
        {
            Capabilities = tapModifiers ? EngineCapabilities.TapModifiers : EngineCapabilities.None,
        };
        return (screen, received);
    }

    [Theory]
    [MemberData(nameof(Verbs))]
    public async Task Performs_with_a_timeout(string verb)
    {
        var (screen, performed) = BoxScreen();

        await Calls[verb](screen.GetByLabel("Agree"), new ActionOptions { Timeout = TimeSpan.FromSeconds(1) }, screen);

        Assert.Single(performed);
    }

    [Fact]
    public async Task LongPress_sends_a_duration_from_100_through_10000_and_none_when_unset_so_the_engine_default_holds()
    {
        var (screen, received) = BoxScreen();
        var box = screen.GetByLabel("Agree");

        await box.LongPressAsync();
        await box.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(100) });
        await box.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(10_000) });

        Assert.Equal<LocatorAction>(
            [new LocatorAction.LongPress(), new LocatorAction.LongPress(TimeSpan.FromMilliseconds(100)), new LocatorAction.LongPress(TimeSpan.FromMilliseconds(10_000))],
            received);
        Assert.Null(((LocatorAction.LongPress)received[0]).Duration);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(10_001)]
    [InlineData(1.5)]
    public async Task LongPress_refuses_a_duration_of_x_before_any_action(double milliseconds)
    {
        var (screen, received) = BoxScreen();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            screen.GetByLabel("Agree").LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(milliseconds) }));

        Assert.Contains("longPress duration must be an integer from 100 through 10000", error.Message);
        Assert.Empty(received);
    }
}
