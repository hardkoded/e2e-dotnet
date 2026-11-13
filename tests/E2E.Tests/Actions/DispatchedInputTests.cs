// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Actions;

/// <summary>Upstream's <c>describe('dispatchedInput')</c> (<c>packages/web/tests/unit/actions.test.ts</c>): whether a failed Playwright action had dispatched its input.</summary>
public sealed class DispatchedInputTests
{
    /// <summary>
    /// A page closing under a click: Playwright puts the call log after the message. Upstream reads it from the error's <c>log</c>, which
    /// Playwright for .NET writes into the message.
    /// </summary>
    private static PlaywrightException Closed(params string[] log) =>
        new("elementHandle.click: Target page, context or browser has been closed" + (log.Length == 0 ? "" : "\nCall log:\n" + string.Join('\n', log)));

    [Fact]
    public void Reads_the_dispatch_from_the_log_of_an_error_whose_page_closed_under_the_action()
    {
        Assert.True(WebErrors.DispatchedInput(Closed("  - attempting click action", "    - element is visible, enabled and stable", "    - performing click action")));
    }

    [Fact]
    public void Is_false_while_the_log_ends_before_the_input_went_out()
    {
        Assert.False(WebErrors.DispatchedInput(Closed("  - attempting click action", "    - waiting for element to be visible, enabled and stable")));
        Assert.False(WebErrors.DispatchedInput(Closed()));
    }
}
