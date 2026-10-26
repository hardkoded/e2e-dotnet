// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.SecretLedger;

/// <summary>Upstream's <c>describe('a marker split across writes')</c>, nested in <c>describe('StreamRedactor')</c>.</summary>
public sealed class AMarkerSplitAcrossWritesTests
{
    private static Redactor Short() => Ledger.Of(("apiKey", "apq000"));

    [Fact]
    public void Holds_an_unfinished_marker_until_it_closes_so_no_piece_of_it_is_redacted_on_its_own()
    {
        var stream = new StreamRedactor(Short());
        Assert.Equal("", stream.Push("<secret:api"));
        Assert.Equal("", stream.Push("Key>"));
        Assert.Equal("<secret:apiKey>", stream.Flush());
    }

    [Fact]
    public void Releases_a_lone_lt_whole_once_ordinary_text_follows_it()
    {
        var stream = new StreamRedactor(Short());
        Assert.Equal("", stream.Push("see <"));
        Assert.Equal("see <b> and more", stream.Push("b> and more text"));
        Assert.Equal(" text", stream.Flush());
    }

    [Fact]
    public void Releases_an_unfinished_marker_as_is_on_flush()
    {
        var stream = new StreamRedactor(Short());
        Assert.Equal("note ", stream.Push("note <secret:abc"));
        Assert.Equal("<secret:abc", stream.Flush());
    }

    [Fact]
    public void Holds_no_unfinished_marker_as_long_as_the_longest_known_marker()
    {
        var stream = new StreamRedactor(Short());
        var name = new string('a', 20);
        Assert.Equal($"note <secret:{name[..15]}", stream.Push($"note <secret:{name}"));
        Assert.Equal(name[15..], stream.Flush());
    }
}
