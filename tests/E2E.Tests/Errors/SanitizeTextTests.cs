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

    [Fact]
    public void Replaces_carriage_returns_SanitizeRecordedText_keeps_the_one_in_a_CRLF()
    {
        Assert.Equal("a\uFFFD\nb", TextRules.SanitizeText("a\r\nb"));
        Assert.Equal(
            "a\r\nb\uFFFD\uFFFD\uFFFDc",
            TextRules.SanitizeRecordedText("a\r\nb\u0007\u000c\u000e\u001b[31mc")
        );
        Assert.Equal(
            "failed\uFFFDpassed\uFFFD\r\nend\uFFFD",
            TextRules.SanitizeRecordedText("failed\rpassed\r\r\nend\r")
        );
    }
}
