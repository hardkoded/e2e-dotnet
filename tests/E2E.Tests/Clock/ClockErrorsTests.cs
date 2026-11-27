// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Clock;

/// <summary>Upstream's <c>describe('clock errors')</c> (<c>packages/web/tests/unit/clock.test.ts</c>, tester-army/e2e#2256).</summary>
public sealed class ClockErrorsTests
{
    private static PlaywrightException Failed(string text) => new(text);

    [Fact]
    public void Blames_a_page_timer_only_on_a_call_that_runs_timers_and_keeps_the_pages_first_line()
    {
        var thrown = Assert.IsType<TestException>(
            WebClock.TranslateClockError(
                Failed("clock.runFor: Error: boom\n    at ClockController._callFirstTimer"),
                "browser.clock.runFor",
                ClockMethod.RunFor
            )
        );
        Assert.Equal("EVALUATE_FAILED", thrown.Code);
        Assert.Equal("browser.clock.runFor: a page timer threw: Error: boom", thrown.Message);
        var odd = Assert.IsType<EngineException>(
            WebClock.TranslateClockError(
                Failed("clock.install: something odd"),
                "browser.clock.install",
                ClockMethod.Install
            )
        );
        Assert.Equal("ENGINE_FAILURE", odd.Code);
    }

    [Fact]
    public void Reads_pauseAt_before_the_current_time_as_the_tests_argument()
    {
        var thrown = Assert.IsType<TestException>(
            WebClock.TranslateClockError(
                Failed("clock.pauseAt: Error: Cannot fast-forward to the past"),
                "browser.clock.pauseAt",
                ClockMethod.PauseAt
            )
        );
        Assert.Equal("INVALID_ARGUMENT", thrown.Code);
        Assert.Equal("browser.clock.pauseAt: the time is before the clock's current time", thrown.Message);
    }

    [Fact]
    public void Leaves_a_closed_crashed_or_disconnected_browser_to_the_engine()
    {
        foreach (
            var text in new[]
            {
                "clock.runFor: Target page, context or browser has been closed",
                "clock.runFor: Page crashed",
                "clock.fastForward: Protocol error (Page.addScriptToEvaluateOnNewDocument): Session closed",
                "clock.pauseAt: Connection closed",
            }
        )
        {
            var thrown = Assert.IsType<EngineException>(
                WebClock.TranslateClockError(Failed(text), "browser.clock.runFor", ClockMethod.RunFor)
            );
            Assert.Equal("ENGINE_FAILURE", thrown.Code);
        }
    }

    [Fact]
    public void Blames_the_page_for_a_timer_error_whose_message_reads_like_a_closed_browser_or_a_time_in_the_past()
    {
        foreach (
            var page in new[]
            {
                "Error: Session closed",
                "Error: WebSocket connection closed",
                "Error: Protocol error: bad frame",
                "Error: Cannot fast-forward to the past",
            }
        )
        {
            var thrown = Assert.IsType<TestException>(
                WebClock.TranslateClockError(
                    Failed($"clock.runFor: {page}\n    at ClockController._callFirstTimer"),
                    "browser.clock.runFor",
                    ClockMethod.RunFor
                )
            );
            Assert.Equal("EVALUATE_FAILED", thrown.Code);
            Assert.Equal($"browser.clock.runFor: a page timer threw: {page}", thrown.Message);
        }
    }
}
