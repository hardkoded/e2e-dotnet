// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>translateLocatorError mapping table</c>. The port keeps an <c>OperationCanceledException</c> as it is, so a caller's cancel
/// stays a cancel (.NET-only).
/// </summary>
public sealed class TranslateLocatorErrorMappingTableTests
{
    private static readonly LocatorExpression Button = new Screen(
        _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
        (_, _, _) => Task.CompletedTask,
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(300)).GetByRole("button").Query;

    [Theory]
    [InlineData(EngineErrorCodes.NodeStale, "test", "LOCATOR_NOT_FOUND")]
    [InlineData(EngineErrorCodes.FrameNotFound, "test", "LOCATOR_NOT_FOUND")]
    [InlineData(EngineErrorCodes.FrameAmbiguous, "test", "LOCATOR_AMBIGUOUS")]
    [InlineData(EngineErrorCodes.NotActionable, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.ActionMayHaveCommitted, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.OperationTimeout, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.Cancelled, "infrastructure", "CANCELLED")]
    [InlineData(EngineErrorCodes.UnsupportedCapability, "configuration", "UNSUPPORTED_CAPABILITY")]
    [InlineData(EngineErrorCodes.InvalidState, "test", "APP_NOT_OPEN")]
    [InlineData(EngineErrorCodes.EngineFailure, "infrastructure", "ENGINE_FAILURE")]
    public void Engine_code_maps_to_category_and_code(string engineCode, string category, string code)
    {
        var engineError = new EngineException(engineCode, "boom", retryable: false);

        var translated = LocatorResolver.TranslateLocatorError(engineError);

        var classified = ErrorSerializer.Classify(translated);
        Assert.Equal(category, classified.Category);
        Assert.Equal(code, classified.Code);
        Assert.IsType<EngineException>(translated.InnerException);
    }

    [Fact]
    public void Passes_existing_E2EErrors_through_unchanged()
    {
        var original = new ConfigurationException("INVALID_CONFIG", "bad config");

        Assert.Same(original, LocatorResolver.TranslateLocatorError(original));
    }

    [Fact]
    public void Wraps_unknown_errors_as_infrastructure_ENGINE_FAILURE()
    {
        var translated = LocatorResolver.TranslateLocatorError(new InvalidOperationException("socket hangup"));

        var classified = ErrorSerializer.Classify(translated);
        Assert.Equal("infrastructure", classified.Category);
        Assert.Equal("ENGINE_FAILURE", classified.Code);
        Assert.Contains("socket hangup", translated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Appends_the_locator_description_when_an_expression_is_provided()
    {
        var translated = LocatorResolver.TranslateLocatorError(new EngineException(EngineErrorCodes.NodeStale, "stale", retryable: false), Button);

        Assert.Contains("stale", translated.Message, StringComparison.Ordinal);
        Assert.EndsWith(": getByRole(\"button\")", translated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NET_only_a_cancel_passes_through_as_a_cancel()
    {
        var cancel = new OperationCanceledException();

        Assert.Same(cancel, LocatorResolver.TranslateLocatorError(cancel, Button));
    }

    [Fact]
    public async Task NET_only_an_unopened_app_reaches_the_test_as_APP_NOT_OPEN()
    {
        var failure = new EngineException(EngineErrorCodes.InvalidState, "boom");
        var screen = new Screen(
            _ => throw failure,
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(300));

        var translated = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").CountAsync());

        Assert.Equal("APP_NOT_OPEN", translated.Code);
        Assert.Same(failure, translated.InnerException);
    }
}
