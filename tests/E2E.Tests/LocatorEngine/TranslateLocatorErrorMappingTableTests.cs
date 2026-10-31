// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>translateLocatorError mapping table</c>. The codes are the port's (COMPATIBILITY, Error codes that differ): a
/// stale node or a missing frame is <c>NOT_FOUND</c> and an ambiguous frame is <c>STRICT_MODE</c>. The table is partial: the rows for
/// <c>CANCELLED</c>, <c>UNSUPPORTED_CAPABILITY</c> (upstream: category <c>configuration</c>), and <c>ENGINE_FAILURE</c> are not ported, since the
/// port returns an engine error of those codes as it is. <c>.NET-only</c> covers them below. The <c>wraps unknown errors</c> it is not
/// ported: the port translates only what an engine threw, and any other error passes through.
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
    [InlineData(EngineErrorCodes.NodeStale, "test", "NOT_FOUND")]
    [InlineData(EngineErrorCodes.FrameNotFound, "test", "NOT_FOUND")]
    [InlineData(EngineErrorCodes.FrameAmbiguous, "test", "STRICT_MODE")]
    [InlineData(EngineErrorCodes.NotActionable, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.ActionMayHaveCommitted, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.OperationTimeout, "test", "ACTION_FAILED")]
    [InlineData(EngineErrorCodes.InvalidState, "test", "APP_NOT_OPEN")]
    public void Partial_Engine_code_maps_to_category_and_code(string engineCode, string category, string code)
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
    public void Appends_the_locator_description_when_an_expression_is_provided()
    {
        var translated = LocatorResolver.TranslateLocatorError(new EngineException(EngineErrorCodes.NodeStale, "stale", retryable: false), Button);

        Assert.Contains("stale", translated.Message, StringComparison.Ordinal);
        Assert.EndsWith(": getByRole(\"button\")", translated.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EngineErrorCodes.Cancelled)]
    [InlineData(EngineErrorCodes.UnsupportedCapability)]
    [InlineData(EngineErrorCodes.EngineFailure)]
    public void NET_only_an_engine_error_with_no_test_code_passes_through(string engineCode)
    {
        var engineError = new EngineException(engineCode, "boom", retryable: false);

        Assert.Same(engineError, LocatorResolver.TranslateLocatorError(engineError, Button));
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
