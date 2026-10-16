// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;

namespace E2E.Tests.TraceDecide;

/// <summary>The replay precondition: the screen the recording began on, as a route. Upstream's <c>decideTraceReplay</c> describe.</summary>
public sealed class DecideTraceReplayTests
{
    private static readonly TraceReplayDecision Replay = TraceReplayDecision.Replay;

    private static readonly TraceReplayDecision WrongContext = TraceReplayDecision.Miss("wrong-context");

    /// <summary>Partial: upstream "replays on the same route, whatever the record id, the query, or the fragment" also replays across a query (<c>?tab=notes</c>). The port keeps a query value that is not minted as part of the screen (<c>CacheRoute</c>).</summary>
    [Fact]
    public void Replays_on_the_same_route_whatever_the_record_id_or_the_fragment()
    {
        var recorded = Entry("/backend/customers/companies-v2/ad3339e0-074f-45e0-b778-3dee8ab545eb");
        Assert.Equal(Replay, Decide.DecideTraceReplay(recorded, "/backend/customers/companies-v2/8374a7a7-a64a-422d-9183-4350756c7f07"));
        Assert.Equal(Replay, Decide.DecideTraceReplay(recorded, "/backend/customers/companies-v2/8374a7a7-a64a-422d-9183-4350756c7f07#top"));
        Assert.Equal(Replay, Decide.DecideTraceReplay(Entry("/orders/42"), "/orders/43917"));
        Assert.Equal(Replay, Decide.DecideTraceReplay(Entry("/#/orders/42"), "/#/orders/7"));
    }

    [Fact]
    public void Misses_on_another_route_on_a_slug_it_cannot_tell_from_a_route_word_and_without_a_location()
    {
        var recorded = Entry("/backend/customers/companies-v2/ad3339e0-074f-45e0-b778-3dee8ab545eb");
        Assert.Equal(WrongContext, Decide.DecideTraceReplay(recorded, "/backend/customers/people-v2/8374a7a7-a64a-422d-9183-4350756c7f07"));
        Assert.Equal(WrongContext, Decide.DecideTraceReplay(Entry("/settings/billing"), "/settings/members"));

        // Nothing recorded at the start is specific to the screen, so an undecided route is another screen.
        Assert.Equal(WrongContext, Decide.DecideTraceReplay(Entry("/products/summer-sneaker"), "/products/winter-boot"));
        Assert.Equal(WrongContext, Decide.DecideTraceReplay(recorded, null));
    }

    [Fact]
    public void Needs_no_starting_point_when_the_recording_opens_with_a_navigate()
    {
        Assert.Equal(Replay, Decide.DecideTraceReplay(Entry("/anywhere", Navigate()), "/elsewhere"));
    }

    [Fact]
    public void Compares_a_device_screen_title_as_itself()
    {
        Assert.Equal(Replay, Decide.DecideTraceReplay(Entry("Settings"), "Settings"));
        Assert.Equal(WrongContext, Decide.DecideTraceReplay(Entry("Settings"), "General"));
    }

    private static RecordedAction Navigate() => new() { Kind = "navigate", Url = "/companies" };

    /// <summary>A recording that began on <paramref name="startPath"/>. It taps first, unless <paramref name="first"/> says otherwise.</summary>
    private static CacheEntry Entry(string? startPath, RecordedAction? first = null) =>
        new() { Route = startPath, Actions = [first ?? new RecordedAction { Kind = "tap", Role = "button", Name = "Save" }] };
}
