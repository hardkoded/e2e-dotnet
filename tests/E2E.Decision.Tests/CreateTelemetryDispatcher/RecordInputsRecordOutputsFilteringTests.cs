// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.CreateTelemetryDispatcher;

/// <summary>
/// The AI SDK's <c>create-telemetry-dispatcher.test.ts</c>, <c>describe('recordInputs / recordOutputs filtering')</c>. Only the download
/// error <c>it</c> is ported: the others send <c>generateText</c>, extraction, tool and speech events, which the port has no feature for.
/// </summary>
public sealed class RecordInputsRecordOutputsFilteringTests
{
    [Fact]
    public async Task Redacts_download_urls_from_errors_sent_to_integrations()
    {
        var integration = new ErrorIntegration();
        var telemetry = new TelemetryDispatcher(new TelemetryOptions { Integrations = [integration] });
        var error = new DownloadException(
            "https://files.example.com/invoice.pdf?X-Amz-Signature=secret",
            413,
            "Payload Too Large"
        );

        await telemetry.OnErrorAsync("call-1", error);

        var delivered = Assert.IsType<DownloadException>(Assert.Single(integration.Errors).Error);
        Assert.Equal("[redacted]", delivered.Url);
        Assert.Equal(413, delivered.StatusCode);
        Assert.DoesNotContain("secret", delivered.ToString());
        Assert.DoesNotContain("secret", delivered.Message);
        Assert.DoesNotContain("secret", delivered.StackTrace ?? "");
        Assert.Contains("secret", error.Url);
    }

    private sealed class ErrorIntegration : ITelemetry
    {
        public List<TelemetryErrorEvent> Errors { get; } = [];

        public Task OnErrorAsync(TelemetryErrorEvent @event)
        {
            Errors.Add(@event);
            return Task.CompletedTask;
        }
    }
}
