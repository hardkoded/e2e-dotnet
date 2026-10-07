// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SecretLedger;

public sealed class RedactFragmentsTests
{
    private const string Value = "cut-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6yQa1eUo5iRk0tYw2zXn7uM";

    [Fact]
    public void Rewrites_a_run_of_8_or_more_characters_of_a_value_anywhere_in_the_text_and_leaves_a_shorter_one()
    {
        var ledger = Ledger.Of(("apiKey", Value));
        Assert.Equal("cut <secret:apiKey>", ledger.RedactFragments("cut " + Value[..59]));
        Assert.Equal("sel <secret:apiKey> end", ledger.RedactFragments("sel " + Value[5..45] + " end"));
        Assert.Equal("a <secret:apiKey> b", ledger.RedactFragments("a " + Value[20..28] + " b"));
        Assert.Equal("a " + Value[20..27] + " b", ledger.RedactFragments("a " + Value[20..27] + " b"));
        Assert.Equal("plain text, 0123456789 and more", ledger.RedactFragments("plain text, 0123456789 and more"));
    }

    [Fact]
    public void Rewrites_a_fragment_in_another_case_and_leaves_a_shorter_one()
    {
        var ledger = Ledger.Of(("apiKey", Value));
        Assert.Equal("sel <secret:apiKey> end", ledger.RedactFragments("sel " + Value[5..45].ToUpperInvariant() + " end"));
        Assert.Equal("a <secret:apiKey> b", ledger.RedactFragments("a " + Value[20..28].ToLowerInvariant() + " b"));
        Assert.Equal("a " + Value[20..27].ToUpperInvariant() + " b", ledger.RedactFragments("a " + Value[20..27].ToUpperInvariant() + " b"));
    }

    [Fact]
    public void Changes_nothing_with_no_value_registered()
    {
        Assert.Equal("anything at all", Ledger.Of().RedactFragments("anything at all"));
    }
}
