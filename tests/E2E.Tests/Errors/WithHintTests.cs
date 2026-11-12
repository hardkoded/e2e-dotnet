// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('withHint')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class WithHintTests
{
    [Fact]
    public void Joins_a_clause_to_one_line_and_starts_a_new_line_after_a_paragraph()
    {
        Assert.Equal("nope; try this", TextRules.WithHint("nope", "try this"));
        Assert.Equal("first line\nsecond line\ntry this", TextRules.WithHint("first line\nsecond line\n\n", "try this"));
        Assert.Equal("nope", TextRules.WithHint("nope", ""));
    }
}
