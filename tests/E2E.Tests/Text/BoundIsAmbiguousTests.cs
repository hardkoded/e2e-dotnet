// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Text;

public sealed class BoundIsAmbiguousTests
{
    [Fact]
    public void Holds_for_text_bound_cuts_and_for_text_that_ends_like_one_of_its_cuts_and_for_nothing_else()
    {
        Assert.True(TextRules.BoundIsAmbiguous("abcdef", 5));
        Assert.True(TextRules.BoundIsAmbiguous("abcd…", 5));
        Assert.True(TextRules.BoundIsAmbiguous("abc…", 5));
        Assert.False(TextRules.BoundIsAmbiguous("abcde", 5));
        Assert.False(TextRules.BoundIsAmbiguous("ab…", 5));
        Assert.False(TextRules.BoundIsAmbiguous("loading…", 300));
    }
}
