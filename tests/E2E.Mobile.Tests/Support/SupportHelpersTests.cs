// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Mobile.Tests.Support;

public sealed class SupportHelpersTests
{
    [Fact]
    public async Task Sends_an_input_with_what_is_left_of_its_budget_and_reports_one_cut_off_as_possibly_committed()
    {
        // Upstream installs fake timers; here the budgets and the devices' answers run on a clock only the test moves.
        var time = new FakeTimeProvider();
        var never = () => new TaskCompletionSource<int>().Task;
        var cut = Assert.ThrowsAsync<EngineException>(() => new InputBudget("tap", TimeSpan.FromMilliseconds(300), static _ => { }, time).SendAsync(never, CancellationToken.None));
        time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", (await cut).Code);

        // Sent with almost nothing left, a tap still gets time to answer.
        var late = new InputBudget("tap", TimeSpan.FromMilliseconds(10), static _ => { }, time).SendAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), time);
            return "tapped";
        }, CancellationToken.None);
        time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal("tapped", await late);

        // A spent budget sends nothing; after an input went out, the next one is refused as possibly committed.
        var spent = new InputBudget("tap", TimeSpan.FromMilliseconds(100), static _ => { }, time);
        time.Advance(TimeSpan.FromMilliseconds(100));
        var dispatched = false;
        var unsent = await Assert.ThrowsAsync<EngineException>(() => spent.SendAsync(() => Task.FromResult(dispatched = true), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", unsent.Code);
        Assert.Equal("tap timed out before it was sent", unsent.Message);
        Assert.False(dispatched);
        var twoStep = new InputBudget("press", TimeSpan.FromMilliseconds(100), static _ => { }, time);
        await twoStep.SendAsync(() => Task.FromResult(0), CancellationToken.None);
        time.Advance(TimeSpan.FromMilliseconds(100));
        var second = await Assert.ThrowsAsync<EngineException>(() => twoStep.SendAsync(() => Task.FromResult(0), CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", second.Code);

        // A command that throws before it is sent does not count as sent.
        var refused = new InputBudget("press", TimeSpan.FromMilliseconds(100), static _ => { }, time);
        await Assert.ThrowsAsync<InvalidOperationException>(() => refused.SendAsync<int>(() => throw new InvalidOperationException("no target"), CancellationToken.None));
        time.Advance(TimeSpan.FromMilliseconds(100));
        var unsentAfter = await Assert.ThrowsAsync<EngineException>(() => refused.SendAsync(() => Task.FromResult(0), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", unsentAfter.Code);

        // A hold the budget cannot cover is refused unsent, with the reason.
        var held = false;
        var hold = await Assert.ThrowsAsync<EngineException>(
            () => new InputBudget("longPress", TimeSpan.FromSeconds(2), static _ => { }, time).SendAsync(() => Task.FromResult(held = true), CancellationToken.None, TimeSpan.FromSeconds(3)));
        Assert.Equal("NOT_ACTIONABLE", hold.Code);
        Assert.StartsWith("longPress holds for 3000 ms, but only 2000 ms of its timeout are left", hold.Message, StringComparison.Ordinal);
        Assert.False(held);
    }

    [Fact]
    public async Task Runs_a_read_with_what_is_left_of_its_budget_and_reports_one_cut_off_after_an_input_as_possibly_committed()
    {
        var time = new FakeTimeProvider();
        var hung = () => new TaskCompletionSource<string>().Task;
        Assert.Equal("tree", await new InputBudget("snapshot", TimeSpan.FromMilliseconds(300), static _ => { }, time).ReadAsync(() => Task.FromResult("tree"), CancellationToken.None));

        var cut = Assert.ThrowsAsync<EngineException>(() => new InputBudget("snapshot", TimeSpan.FromMilliseconds(200), static _ => { }, time).ReadAsync(hung, CancellationToken.None));
        time.Advance(TimeSpan.FromMilliseconds(200));
        var timedOut = await cut;
        Assert.Equal("OPERATION_TIMEOUT", timedOut.Code);
        Assert.Equal("snapshot timed out before the device answered", timedOut.Message);

        var spent = new InputBudget("snapshot", TimeSpan.FromMilliseconds(100), static _ => { }, time);
        time.Advance(TimeSpan.FromMilliseconds(100));
        var late = await Assert.ThrowsAsync<EngineException>(() => spent.ReadAsync(() => Task.FromResult("late"), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", late.Code);
        Assert.Equal("snapshot timed out before it was sent", late.Message);

        // After a swipe went out, a read that does not answer must not let a replay send the swipe again.
        var dismissal = new InputBudget("keyboard dismiss", TimeSpan.FromMilliseconds(300), static _ => { }, time);
        await dismissal.SendAsync(() => Task.FromResult(0), CancellationToken.None);
        var after = Assert.ThrowsAsync<EngineException>(() => dismissal.ReadAsync(hung, CancellationToken.None));
        time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", (await after).Code);
        var again = await Assert.ThrowsAsync<EngineException>(() => dismissal.ReadAsync(() => Task.FromResult("late"), CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", again.Code);
    }

    // .NET-only: upstream's budgets are numbers of milliseconds, and an infinite timeout (screen.tree with no timeout) has none.
    [Fact]
    public async Task Never_spends_an_infinite_budget_on_a_read_or_an_input()
    {
        var time = new FakeTimeProvider();
        var budget = new InputBudget("tree", Timeout.InfiniteTimeSpan, static _ => { }, time);
        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal("tree", await budget.ReadAsync(() => Task.FromResult("tree"), CancellationToken.None));
        Assert.Equal(1, await budget.SendAsync(() => Task.FromResult(1), CancellationToken.None, TimeSpan.FromHours(1)));
        Assert.True(budget.Remaining > TimeSpan.FromDays(1000));
    }

    [Fact]
    public void Reports_the_app_and_the_screen_title_as_a_location_that_is_not_a_URL()
    {
        Assert.Equal("com.apple.Preferences / General", DeviceSupport.ScreenLocation("com.apple.Preferences", "General"));
        Assert.Equal("com.apple.Preferences", DeviceSupport.ScreenLocation("com.apple.Preferences", null));
        Assert.Equal("General", DeviceSupport.ScreenLocation(null, "General"));
        Assert.Null(DeviceSupport.ScreenLocation(null, null));
        Assert.Equal("My App / Wi-Fi Settings", DeviceSupport.ScreenLocation("My App", " Wi-Fi  Settings "));

        // Two apps with a screen of the same name never share a location.
        Assert.NotEqual(DeviceSupport.ScreenLocation("com.apple.Preferences", "General"), DeviceSupport.ScreenLocation("com.example.other", "General"));

        // The harness applies its origin policy to anything that parses as a URL; a device location must not.
        Assert.False(Uri.TryCreate(DeviceSupport.ScreenLocation("com.apple.Preferences", "General"), UriKind.Absolute, out _));
    }

    [Fact]
    public void Swipes_against_the_scroll_direction_inside_the_rect()
    {
        var rect = new Rect(0, 100, 200, 400);
        Assert.Equal((new Point(100, 400), new Point(100, 200)), DeviceSupport.SwipeWithin(rect, ScrollDirection.Down, null));
        Assert.Equal((new Point(100, 140), new Point(100, 460)), DeviceSupport.SwipeWithin(rect, ScrollDirection.Up, Momentum.Fast));
        Assert.Equal((new Point(125, 300), new Point(75, 300)), DeviceSupport.SwipeWithin(rect, ScrollDirection.Right, Momentum.Slow));
        Assert.Equal((new Point(50, 300), new Point(150, 300)), DeviceSupport.SwipeWithin(rect, ScrollDirection.Left, null));
    }

    [Fact]
    public void Constrains_labels_to_safe_filenames()
    {
        Assert.Equal("after_checkout_1", DeviceSupport.SanitizeFilename("after checkout/1"));
        Assert.Equal("artifact", DeviceSupport.SanitizeFilename(""));
        Assert.Equal(64, DeviceSupport.SanitizeFilename(new string('x', 100)).Length);
    }
}
