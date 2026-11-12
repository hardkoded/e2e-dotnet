// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Errors;

/// <summary>
/// <c>describe('pageScriptFailure')</c> of upstream's <c>errors.test.ts</c>. Upstream mocks a Playwright page whose <c>:root</c> answers
/// the utility-world count in turn; the port feeds the same answers to the delegate form of <c>PageScriptFailureAsync</c>.
/// </summary>
public sealed class PageScriptFailureTests
{
    private static readonly Exception PageError = new("locator.evaluateAll: TypeError: Map is not a constructor");

    /// <summary>The probes of a page whose <c>:root</c> answers the utility-world count with <paramref name="counts"/> in turn and fails the main-world evaluate.</summary>
    private static Task<TestException?> FailureAsync(params bool[] counts)
    {
        var call = 0;
        return WebErrors.PageScriptFailureAsync(
            () => counts[call++] ? Task.CompletedTask : Task.FromException(new Exception("Target page, context or browser has been closed")),
            () => Task.FromException(new Exception("locator.evaluate: TypeError: Map is not a constructor")),
            PageError,
            "locate");
    }

    [Fact]
    public async Task Classifies_a_failure_the_page_caused_while_both_worlds_answer_around_it()
    {
        var failure = await FailureAsync(true, true);
        Assert.NotNull(failure);
        Assert.Equal("EVALUATE_FAILED", failure.Code);
    }

    [Fact]
    public async Task Leaves_a_frame_that_stops_answering_after_the_main_world_probe_to_infrastructure()
    {
        Assert.Null(await FailureAsync(true, false));
    }

    [Fact]
    public async Task Leaves_a_frame_the_utility_world_cannot_read_to_infrastructure()
    {
        Assert.Null(await FailureAsync(false));
    }
}
