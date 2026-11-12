// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;

namespace E2E.Tests.AgentDerived;

/// <summary>
/// .NET-only: upstream's <c>derived</c> field of a <c>tool</c> action is a string union, which TypeScript checks with
/// <c>isDerivedReason</c> when a trace is read. C# spells it as an enum, so these pin its wire spelling and that a name upstream
/// lacks, or a number, makes the entry unreadable. No upstream <c>it</c> covers this.
/// </summary>
public sealed class DerivedReasonWireTests
{
    [Theory]
    [InlineData(DerivedReason.Pixels, "pixels")]
    [InlineData(DerivedReason.WholeNode, "whole-node")]
    [InlineData(DerivedReason.MintedToken, "minted-token")]
    [InlineData(DerivedReason.Date, "date")]
    public void A_gap_records_its_rule_as_upstream_spells_it(DerivedReason reason, string wire)
    {
        var gap = new E2E.Cache.TraceRecorder(() => Redactor.None).RecordDerivedGap(reason);

        var json = JsonSerializer.Serialize(gap, JsonDefaults.Options);

        Assert.Equal("tool", gap.Kind);
        Assert.Contains($"\"derived\": \"{wire}\"", json, StringComparison.Ordinal);
        Assert.Equal(reason, JsonSerializer.Deserialize<RecordedAction>(json, JsonDefaults.Options)!.Derived);
    }

    [Theory]
    [InlineData("\"rotated\"")]
    [InlineData("1")]
    public void A_rule_upstream_lacks_makes_the_action_unreadable(string derived)
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<RecordedAction>("{\"kind\":\"tool\",\"derived\":" + derived + "}", JsonDefaults.Options));
    }
}
