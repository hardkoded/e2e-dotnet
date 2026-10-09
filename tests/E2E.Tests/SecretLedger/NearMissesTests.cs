// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Tests.SecretLedger;

public sealed class NearMissesTests
{
    public static TheoryData<string, string> Values => new()
    {
        { "letters", "abcdefghijklmnopqrstuvwxyzABCDEFG" },
        { "whitespace runs", string.Join(' ', Enumerable.Range(0, 13).Select(index => "w" + index)) },
        { "accented letters", new string('é', 31) },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void Rejects_text_one_character_short_of_a_long_value_made_of_X_in_one_pass(string kind, string value)
    {
        _ = kind;
        var ledger = Ledger.Of(("long", value));
        var nearMiss = value[..^1] + "!";
        var started = Stopwatch.StartNew();
        Assert.Equal(nearMiss, ledger.Redact(nearMiss));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1), "took " + started.Elapsed);
    }
}
