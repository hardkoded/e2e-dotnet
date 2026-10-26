// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceIdentity;

/// <summary>Upstream's <c>describe('keyContextChanges')</c> in <c>trace-identity.test.ts</c>.</summary>
public sealed class KeyContextChangesTests
{
    private static readonly TraceKeyContext Context = new("trace-1", "conservative/6", new string('p', 64), "web", "web", 1, new string('a', 64), new string('c', 64));

    [Fact]
    public void Names_what_changed_since_the_recording_with_the_values_a_reader_can_act_on()
    {
        var recorded = Context with { EngineSpiVersion = 0, AppIdentity = new string('b', 64) };
        Assert.Equal(
            ["the engine contract (0 -> 1)", "the app's identity (app.identity, else its URL) or environment"],
            Identity.KeyContextChanges(recorded, Context));
    }

    [Fact]
    public void Says_nothing_for_an_entry_that_recorded_no_context_or_the_same_one()
    {
        Assert.Empty(Identity.KeyContextChanges(null, Context));
        Assert.Empty(Identity.KeyContextChanges(Context, Context));
    }
}
