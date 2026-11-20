// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>redactTelemetryCredentials</c> (<c>telemetry/redact-telemetry-credentials.ts</c>): a telemetry-only copy of a value with
/// the credentials it may carry removed. The original is never changed.
/// </summary>
internal static class RedactTelemetryCredentials
{
    /// <summary>What a redacted download URL reads as (<c>'[redacted]'</c>).</summary>
    private const string Redacted = "[redacted]";

    /// <summary>
    /// Returns <paramref name="value"/>, or a copy of it that is safe to publish: a <see cref="DownloadException"/> loses its URL, which can
    /// carry a signature, and a <see cref="TelemetryErrorEvent"/> carries the redacted error.
    /// </summary>
    internal static T Redact<T>(T value) =>
        value switch
        {
            DownloadException error => (T)(object)RedactDownloadError(error),
            TelemetryErrorEvent { Error: { } error } @event when !ReferenceEquals(Redact(error), error) => (T)
                (object)(@event with { Error = Redact(error) }),
            _ => value,
        };

    private static DownloadException RedactDownloadError(DownloadException error) =>
        new(Redacted, error.StatusCode, error.StatusText, "Failed to download a file.");
}
