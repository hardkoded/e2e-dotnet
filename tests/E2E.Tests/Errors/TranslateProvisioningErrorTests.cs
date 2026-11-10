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

    [Fact]
    public void Keeps_the_category_of_a_harness_classified_error()
    {
        var config = ErrorSerializer.TranslateProvisioningError(new ConfigurationException("INVALID_CONFIG", "no such browser"), "", CancellationToken.None);
        Assert.Equal("configuration", ErrorSerializer.Classify(config).Category);
        var infra = ErrorSerializer.TranslateProvisioningError(new EngineException("BROWSER_INSTALL_FAILED", "exit 1"), "", CancellationToken.None);
        Assert.Equal("BROWSER_INSTALL_FAILED", ErrorSerializer.Classify(infra).Code);
    }
}
