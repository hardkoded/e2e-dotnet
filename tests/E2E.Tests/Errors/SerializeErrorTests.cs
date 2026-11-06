// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('serializeError')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class SerializeErrorTests
{
    [Fact]
    public void Partial_Produces_the_bounded_report_shape()
    {
        // PARTIAL: the port's SerializedError carries no phase; the rest of the shape is checked.
        var serialized = ErrorSerializer.Serialize(new TestException("ASSERTION_FAILED", "expected x"));
        Assert.Equal("test", serialized.Category);
        Assert.Equal("ASSERTION_FAILED", serialized.Code);
        Assert.Equal("expected x", serialized.Message);
        Assert.False(serialized.Retryable);
    }

    [Fact]
    public void Strips_control_characters_and_ANSI_colors_from_messages()
    {
        var serialized = ErrorSerializer.Serialize(new ConfigurationException("X", "a\u0007b\u0001c"));
        Assert.Equal("a�b�c", serialized.Message);
        var colored = ErrorSerializer.Serialize(new ConfigurationException("X", "\u001b[1m\u001b[31mUnauthenticated\u001b[0m request"));
        Assert.Equal("Unauthenticated request", colored.Message);
    }
}
