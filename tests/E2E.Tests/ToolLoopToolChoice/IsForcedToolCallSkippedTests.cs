// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// The "isForcedToolCallSkipped" describe. Upstream matches the AI SDK's <c>AI_ToolChoiceViolationError</c> by name; the port matches
/// <see cref="ToolChoiceViolationException"/>, the type its loop builds for the same fact.
/// </summary>
public sealed class IsForcedToolCallSkippedTests
{
    private static ToolChoiceViolationException Violation(string finishReason) => new(finishReason);

    [Fact]
    public void Matches_a_finished_prose_reply_by_the_SDK_error_name()
    {
        Assert.True(ToolChoice.IsForcedToolCallSkipped(Violation("stop")));
    }

    [Fact]
    public void Ignores_a_reply_cut_off_or_filtered_and_anything_not_named_as_the_violation()
    {
        Assert.False(ToolChoice.IsForcedToolCallSkipped(Violation("length")));
        Assert.False(ToolChoice.IsForcedToolCallSkipped(Violation("content-filter")));
        Assert.False(ToolChoice.IsForcedToolCallSkipped(new InvalidOperationException("x")));
        Assert.False(ToolChoice.IsForcedToolCallSkipped(null));
    }
}
