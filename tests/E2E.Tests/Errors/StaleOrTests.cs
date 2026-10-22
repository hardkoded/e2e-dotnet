// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Errors;

public sealed class StaleOrTests
{
    private static readonly string[] NavigationRaces =
    [
        "Execution context was destroyed, most likely because of a navigation.",
        "locator.evaluate: Frame was detached",
    ];

    private static EngineException Engine(Exception error) => Assert.IsType<EngineException>(error);

    [Fact]
    public void Treats_a_navigation_race_like_a_stale_node()
    {
        foreach (var text in NavigationRaces)
        {
            var error = Engine(WebErrors.StaleOr(new Exception(text), "read"));
            Assert.Equal("NODE_STALE", error.Code);
            Assert.True(error.Retryable);
        }
    }

    [Fact]
    public void Still_reports_detachment_and_misses_as_stale()
    {
        var error = Engine(WebErrors.StaleOr(new Exception("element is not attached to the DOM"), "read"));
        Assert.Equal("NODE_STALE", error.Code);
        Assert.True(error.Retryable);
    }

    [Fact]
    public void Keeps_a_timeout_a_timeout_locate_resolves_once_and_never_waits()
    {
        var error = Engine(WebErrors.StaleOr(new TimeoutException("waiting for element"), "read"));
        Assert.Equal("OPERATION_TIMEOUT", error.Code);
        Assert.False(error.Retryable);
    }

    [Theory]
    [InlineData("resolved to hidden")]
    [InlineData("element is not attached to the DOM")]
    [InlineData("element was detached from the DOM")]
    [InlineData("no element found")]
    [InlineData("frame was detached")]
    public void Keeps_a_timeout_a_timeout_when_its_call_log_says(string line)
    {
        var timeout = new TimeoutException($"Timeout 5000ms exceeded.\nCall log:\n  - waiting for locator('#x')\n  - locator {line}");
        var error = Engine(WebErrors.StaleOr(timeout, "read"));
        Assert.Equal("OPERATION_TIMEOUT", error.Code);
        Assert.False(error.Retryable);
    }
}
