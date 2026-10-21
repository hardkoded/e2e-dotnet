// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('sanitizeText')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class SanitizeTextTests
{
    [Fact]
    public void Replaces_C0_DEL_and_C1_controls_keeps_tabs_and_newlines_and_leaves_non_BMP_text_intact()
    {
        Assert.Equal("a�b�c�d", TextRules.SanitizeText("a\u0000b\u007fc\u0085d"));
        Assert.Equal("��", TextRules.SanitizeText("\u0001\u0001"));
        Assert.Equal("a\tb\nc", TextRules.SanitizeText("a\tb\nc"));
        Assert.Equal("ok 😀 fine", TextRules.SanitizeText("ok 😀 fine"));
    }
}
