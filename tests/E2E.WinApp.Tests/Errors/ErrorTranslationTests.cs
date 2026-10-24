// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;

namespace E2E.WinApp.Tests.Errors;

/// <summary>.NET-only: upstream has no Windows engine. Each driver fault onto its engine code.</summary>
public sealed class ErrorTranslationTests
{
    [Theory]
    [InlineData(DriverFault.ElementVanished, EngineErrorCodes.NodeStale, true)]
    [InlineData(DriverFault.Offscreen, EngineErrorCodes.NotActionable, false)]
    [InlineData(DriverFault.ValueRejected, EngineErrorCodes.NotActionable, false)]
    [InlineData(DriverFault.ForegroundLost, EngineErrorCodes.NotActionable, false)]
    [InlineData(DriverFault.AppNotFound, EngineErrorCodes.InvalidState, false)]
    [InlineData(DriverFault.Launch, "ENVIRONMENT_UNAVAILABLE", false)]
    [InlineData(DriverFault.Ambiguous, EngineErrorCodes.EngineFailure, false)]
    [InlineData(DriverFault.Other, EngineErrorCodes.EngineFailure, false)]
    public void Maps_each_fault_onto_its_code(DriverFault fault, string code, bool retryable)
    {
        var error = Assert.IsType<EngineException>(WinAppErrors.TranslateError(new WindowDriverException(fault, "the library said no"), "perform tap"));
        Assert.Equal(code, error.Code);
        Assert.Equal(retryable, error.Retryable);
        Assert.Contains("perform tap failed: the library said no", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_not_found_names_the_open_to_make()
    {
        var error = Assert.IsType<EngineException>(WinAppErrors.TranslateError(new WindowDriverException(DriverFault.AppNotFound, "gone"), "observe"));
        Assert.Contains("call app.open()", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_is_cancelled_and_never_a_timeout()
    {
        var error = Assert.IsType<EngineException>(WinAppErrors.TranslateError(new OperationCanceledException(), "observe"));
        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
        Assert.Equal("observe cancelled", error.Message);
    }

    [Fact]
    public void An_engine_error_passes_through_untouched()
    {
        var original = new EngineException(EngineErrorCodes.NotActionable, "no", retryable: false);
        Assert.Same(original, WinAppErrors.TranslateError(original, "x"));
        var config = new ConfigurationException("INVALID_CONFIG", "no");
        Assert.Same(config, WinAppErrors.TranslateError(config, "x"));
    }

    [Fact]
    public void Any_other_exception_is_an_engine_failure_with_its_text_and_cause()
    {
        var cause = new InvalidOperationException("COM is sad");
        var error = Assert.IsType<EngineException>(WinAppErrors.TranslateError(cause, "screenshot"));
        Assert.Equal(EngineErrorCodes.EngineFailure, error.Code);
        Assert.Equal("screenshot failed: COM is sad", error.Message);
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public async Task Run_checks_the_token_first_and_translates_what_the_work_throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var called = false;
        var error = await Assert.ThrowsAsync<EngineException>(() => WinAppErrors.RunAsync("observe", _ => { called = true; return Task.CompletedTask; }, cancelled.Token));
        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
        Assert.False(called);

        var failed = await Assert.ThrowsAsync<EngineException>(() => WinAppErrors.RunAsync<int>("observe", _ => throw new WindowDriverException(DriverFault.Offscreen, "x"), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, failed.Code);
        Assert.Equal(3, await WinAppErrors.RunAsync("observe", _ => Task.FromResult(3), CancellationToken.None));
    }
}
