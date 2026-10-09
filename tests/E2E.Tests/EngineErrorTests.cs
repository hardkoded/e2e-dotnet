// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests;

[Collection(BrowserCollection.Name)]
public sealed class EngineErrorTests
{
    [Fact]
    public void Only_stale_reads_can_claim_to_be_retryable()
    {
        var stale = new EngineException(EngineErrorCodes.NodeStale, "gone", retryable: true);
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);

        var coerced = new EngineException(EngineErrorCodes.NotActionable, "blocked", retryable: true);
        Assert.Equal("ENGINE_FAILURE", coerced.Code);
        Assert.False(coerced.Retryable);

        Assert.False(new EngineException(EngineErrorCodes.NodeStale, "gone").Retryable);
    }

    [Fact]
    public void An_action_timeout_before_dispatch_is_not_actionable()
    {
        var cause = new System.TimeoutException(
            "Timeout 5000ms exceeded.\nCall log:\n  - waiting for element to be visible, enabled and stable\n  - element is not enabled");

        var error = Assert.IsType<EngineException>(WebErrors.ClassifyAction(cause, new LocatorAction.Tap()));

        Assert.Equal("NOT_ACTIONABLE", error.Code);
        Assert.False(error.Retryable);
        Assert.Contains("tap did not become actionable in time", error.Message, StringComparison.Ordinal);
        Assert.Contains("element is not enabled", error.Message, StringComparison.Ordinal);
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public void An_action_timeout_after_dispatch_may_have_committed()
    {
        var cause = new System.TimeoutException(
            "Timeout 5000ms exceeded.\nCall log:\n  - performing click action\n  - click action done\n  - waiting for scheduled navigations to finish");

        var error = Assert.IsType<EngineException>(WebErrors.ClassifyAction(cause, new LocatorAction.Tap()));

        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", error.Code);
        Assert.False(error.Retryable);
    }

    [Fact]
    public void A_detached_element_is_a_retryable_stale_node()
    {
        var error = Assert.IsType<EngineException>(WebErrors.ClassifyAction(new PlaywrightException("Element is not attached to the DOM"), new LocatorAction.Check()));

        Assert.Equal("NODE_STALE", error.Code);
        Assert.True(error.Retryable);
    }

    [Fact]
    public void A_sensitive_fill_redacts_its_value_and_drops_the_cause()
    {
        var error = Assert.IsType<EngineException>(WebErrors.ClassifyAction(new PlaywrightException("fill(\"hunter2\") failed"), new LocatorAction.Fill("hunter2", Sensitive: true)));

        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        Assert.Contains("[redacted]", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Navigation_and_reads_map_to_engine_codes()
    {
        Assert.Equal("OPERATION_TIMEOUT", WebErrors.Translate(new System.TimeoutException("Timeout 5000ms exceeded."), "navigate to /").Code);
        Assert.Equal("ENGINE_FAILURE", WebErrors.Translate(new PlaywrightException("net::ERR_CONNECTION_REFUSED"), "navigate to /").Code);

        var stale = WebErrors.NavigationStaleOr(new PlaywrightException("Execution context was destroyed, most likely because of a navigation"), "observe");
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        Assert.Equal("ENGINE_FAILURE", WebErrors.NavigationStaleOr(new PlaywrightException("boom"), "observe").Code);
    }
}
