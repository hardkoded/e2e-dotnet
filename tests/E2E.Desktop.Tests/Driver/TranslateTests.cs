// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using static E2E.Desktop.Tests.Driver.DriverTestSupport;

namespace E2E.Desktop.Tests.Driver;

/// <summary>Ported from <c>describe('translate')</c> in upstream's <c>packages/desktop/tests/unit/driver.test.ts</c>.</summary>
public sealed class TranslateTests
{
    private static void AssertTranslated(object cause, string label, bool input, string code, bool retryable = false)
    {
        var translated = Assert.IsType<EngineException>(DesktopDriverCalls.Translate(cause, label, input));
        Assert.Equal(code, translated.Code);
        Assert.Equal(retryable, translated.Retryable);
    }

    [Fact]
    public void Keeps_only_a_refusal_that_sent_nothing_retryable_or_refusable()
    {
        AssertTranslated(Failed("STALE_REF", "not_delivered"), "click", true, "NODE_STALE", true);
        AssertTranslated(Failed("ELEMENT_NOT_FOUND", "not_delivered"), "click", true, "NODE_STALE", true);
        AssertTranslated(
            Failed("APP_UNRESPONSIVE", "not_delivered", new() { ["retryable"] = true }),
            "snapshot",
            false,
            "NODE_STALE",
            true
        );
        AssertTranslated(Failed("APP_UNRESPONSIVE", "not_delivered"), "snapshot", false, "ENGINE_FAILURE");
        AssertTranslated(Failed("ACTION_NOT_SUPPORTED", "not_delivered"), "click", true, "UNSUPPORTED_CAPABILITY");
        AssertTranslated(Failed("POLICY_DENIED", "not_delivered"), "press", true, "UNSUPPORTED_CAPABILITY");
        var gone = Assert.IsType<EngineException>(
            DesktopDriverCalls.Translate(Failed("WINDOW_NOT_FOUND", "not_delivered"), "snapshot", false)
        );
        Assert.Equal("INVALID_STATE", gone.Code);
        Assert.Contains("app.open()", gone.Message);
        AssertTranslated(Failed("ACTION_FAILED", "not_delivered"), "click", true, "NOT_ACTIONABLE");
        AssertTranslated(Failed("TIMEOUT", "not_delivered"), "click", true, "NOT_ACTIONABLE");
        AssertTranslated(Failed("TIMEOUT", "not_delivered"), "snapshot", false, "OPERATION_TIMEOUT");
        AssertTranslated(Failed("INTERNAL", "not_delivered"), "snapshot", false, "ENGINE_FAILURE");
    }

    [Fact]
    public void Never_lets_an_input_that_may_have_landed_look_retryable()
    {
        foreach (var code in new[] { "STALE_REF", "ACTION_FAILED", "TIMEOUT", "ELEMENT_NOT_FOUND" })
        {
            AssertTranslated(Failed(code, "delivered_unverified"), "click", true, "ACTION_MAY_HAVE_COMMITTED");
        }
    }

    [Fact]
    public void Names_the_permission_to_grant_and_passes_an_engine_error_through()
    {
        var denied = Assert.IsType<EngineException>(
            DesktopDriverCalls.Translate(Failed("PERM_DENIED", "not_delivered"), "snapshot", false)
        );
        Assert.Equal("ENGINE_FAILURE", denied.Code);
        Assert.Contains("Grant Accessibility", denied.Message);
        var engineError = new EngineException("CANCELLED", "cancelled", retryable: false);
        Assert.Same(engineError, DesktopDriverCalls.Translate(engineError, "click", true));
        AssertTranslated("odd", "click", true, "ENGINE_FAILURE");
    }
}
