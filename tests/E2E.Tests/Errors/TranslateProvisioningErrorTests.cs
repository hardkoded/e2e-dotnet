// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('translateProvisioningError')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class TranslateProvisioningErrorTests
{
    [Fact]
    public void Never_yields_a_test_error_an_engine_error_of_any_code_is_infrastructure()
    {
        var stale = ErrorSerializer.TranslateProvisioningError(new EngineException(EngineErrorCodes.NodeStale, "ref gone", retryable: true), " while preparing", CancellationToken.None);
        var (staleCategory, staleCode, staleMessage, _) = ErrorSerializer.Classify(stale);
        Assert.Equal("infrastructure", staleCategory);
        Assert.Equal(EngineErrorCodes.EngineFailure, staleCode);
        Assert.Equal("ref gone while preparing", staleMessage);
        var plain = ErrorSerializer.TranslateProvisioningError(new InvalidOperationException("download failed"), " while preparing", CancellationToken.None);
        var (category, code, message, _) = ErrorSerializer.Classify(plain);
        Assert.Equal("infrastructure", category);
        Assert.Equal(EngineErrorCodes.EngineFailure, code);
        Assert.Equal("download failed while preparing", message);
    }

    [Fact]
    public void Keeps_a_cancellation_a_cancellation()
    {
        var cancelled = ErrorSerializer.TranslateProvisioningError(new EngineException(EngineErrorCodes.Cancelled, "browser install cancelled"), "", CancellationToken.None);
        var (category, code, _, _) = ErrorSerializer.Classify(cancelled);
        Assert.Equal("infrastructure", category);
        Assert.Equal(EngineErrorCodes.Cancelled, code);
    }

    // .NET-only: upstream has no caller token, so these pin the caller's cancel, not a case the upstream it names.
    [Fact]
    public void A_caller_cancel_during_provisioning_is_a_cancel_even_for_a_plain_failure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var translated = ErrorSerializer.TranslateProvisioningError(new InvalidOperationException("download failed"), " while preparing", cancellation.Token);
        var (category, code, _, _) = ErrorSerializer.Classify(translated);
        Assert.Equal("infrastructure", category);
        Assert.Equal(EngineErrorCodes.Cancelled, code);
    }

    [Fact]
    public void A_caller_cancel_during_provisioning_is_a_cancel_even_for_a_stale_node()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var translated = ErrorSerializer.TranslateProvisioningError(new EngineException(EngineErrorCodes.NodeStale, "ref gone", retryable: true), " while preparing", cancellation.Token);
        Assert.Equal(EngineErrorCodes.Cancelled, ErrorSerializer.Classify(translated).Code);
    }

    [Fact]
    public void A_harness_named_error_keeps_its_category_during_a_caller_cancel()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var translated = ErrorSerializer.TranslateProvisioningError(new ConfigurationException("INVALID_CONFIG", "no such browser"), "", cancellation.Token);
        Assert.Equal("configuration", ErrorSerializer.Classify(translated).Category);
    }

    [Fact]
    public void A_runner_named_engine_error_passes_through_unchanged()
    {
        var unreachable = new EngineException("APP_UNREACHABLE", "nothing answered") { Mapped = true };
        var translated = ErrorSerializer.TranslateProvisioningError(unreachable, " while preparing", CancellationToken.None);
        Assert.Same(unreachable, translated);
    }

    [Fact]
    public void An_abort_that_is_not_the_callers_cancel_is_an_engine_failure()
    {
        var translated = ErrorSerializer.TranslateProvisioningError(new OperationCanceledException("installer timed out"), " while preparing", CancellationToken.None);
        var (category, code, message, _) = ErrorSerializer.Classify(translated);
        Assert.Equal("infrastructure", category);
        Assert.Equal(EngineErrorCodes.EngineFailure, code);
        Assert.Equal("installer timed out while preparing", message);
    }

    [Fact]
    public void Keeps_the_category_of_a_harness_classified_error()
    {
        var config = ErrorSerializer.TranslateProvisioningError(new ConfigurationException("INVALID_CONFIG", "no such browser"), "", CancellationToken.None);
        Assert.Equal("configuration", ErrorSerializer.Classify(config).Category);
        var infra = ErrorSerializer.TranslateProvisioningError(new EngineException("BROWSER_INSTALL_FAILED", "exit 1"), "", CancellationToken.None);
        Assert.Equal("BROWSER_INSTALL_FAILED", ErrorSerializer.Classify(infra).Code);
    }
}
