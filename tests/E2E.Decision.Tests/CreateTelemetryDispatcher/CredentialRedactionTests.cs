// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Decide.DecideTestHelpers;

namespace E2E.Decision.Tests.CreateTelemetryDispatcher;

/// <summary>The AI SDK's <c>create-telemetry-dispatcher.test.ts</c>, <c>describe('credential redaction')</c>.</summary>
public sealed class CredentialRedactionTests
{
    [Fact]
    public async Task Partial_applies_provider_owned_views_throughout_SDK_metadata_without_mutating_originals()
    {
        var integration = new RecordingTelemetry();
        var (model, _) = Setup();
        var original = new SecretException();
        model.FailOnce(original);

        await Assert.ThrowsAsync<SecretException>(() =>
            ExperimentalDecide.RunAsync(
                new DecideOptions
                {
                    Model = model,
                    State = [],
                    Questions = QuestionMap,
                    Telemetry = new TelemetryOptions { Integrations = [integration] },
                }
            )
        );

        var delivered = Assert.Single(integration.Errors).Error;
        Assert.NotSame(original, delivered);
        Assert.DoesNotContain("test-secret", delivered.Message);
        Assert.Contains("test-secret", original.Message);
    }

    [Fact]
    public void Partial_omits_an_annotated_value_if_its_redactor_throws()
    {
        var error = new BrokenRedactorException();

        Assert.Null(RedactTelemetryCredentials.Redact(error));
        Assert.Contains("test-secret", error.Message);
    }

    private sealed class SecretException() : Exception("failed with test-secret"), ITelemetryRedactable
    {
        public object RedactForTelemetry() => new Exception("failed with [redacted]");
    }

    private sealed class BrokenRedactorException() : Exception("failed with test-secret"), ITelemetryRedactable
    {
        public object RedactForTelemetry() => throw new InvalidOperationException("broken redactor");
    }
}
