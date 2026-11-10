// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using IdHelpers = E2E.Internal.Ids;

namespace E2E.Tests.Ids;

/// <summary>Upstream's <c>describe('validateTitle')</c> (<c>tests/unit/ids.test.ts</c>).</summary>
public sealed class ValidateTitleTests
{
    [Fact]
    public void Rejects_an_empty_title_one_over_512_UTF_8_bytes_and_NUL()
    {
        Assert.NotNull(IdHelpers.ValidateTitle(""));
        Assert.NotNull(IdHelpers.ValidateTitle(new string('ż', 257)));
        Assert.Null(IdHelpers.ValidateTitle(new string('a', 512)));
        Assert.NotNull(IdHelpers.ValidateTitle("a\u0000b"));
    }

    // .NET-only: no upstream it. A JS string may hold an unpaired surrogate, which TextEncoder encodes as U+FFFD (three bytes).
    [Fact]
    public void Counts_an_unpaired_surrogate_as_the_replacement_character()
    {
        Assert.Null(IdHelpers.ValidateTitle("a\ud800"));
        Assert.Equal("a%EF%BF%BD", IdHelpers.EncodeTitle("a\ud800"));
    }
}
