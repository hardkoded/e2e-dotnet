// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Ids;

/// <summary>Upstream's <c>describe('encodeTitle')</c> (<c>tests/unit/ids.test.ts</c>).</summary>
public sealed class EncodeTitleTests
{
    [Fact]
    public void Percent_encodes_every_other_byte_as_uppercase_HH()
    {
        Assert.Equal("a%20b", AttemptReport.EncodeTitle("a b"));
        Assert.Equal("a%2Fb", AttemptReport.EncodeTitle("a/b"));
        Assert.Equal("a%25b", AttemptReport.EncodeTitle("a%b"));
        Assert.Equal("a%3Ab", AttemptReport.EncodeTitle("a:b"));
    }

    [Fact]
    public void Applies_NFC_normalization_then_encodes_the_UTF_8_bytes()
    {
        var decomposed = "e\u0301"; // e + combining acute
        Assert.Equal("%C3%A9", AttemptReport.EncodeTitle(decomposed));
        Assert.Equal("%E6%97%A5", AttemptReport.EncodeTitle("日"));
    }
}
