// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>redactTelemetryCredentials</c> (<c>telemetry/redact-telemetry-credentials.ts</c>): applies the provider-owned credential
/// redaction on the telemetry-only copy of a value. Only the SDK's metadata is visited. The decide events carry no request or response body,
/// so the error of <c>onError</c> is the one value that needs it. A <see cref="DownloadException"/> loses its URL.
/// </summary>
internal static class RedactTelemetryCredentials
{
    /// <summary>
    /// Returns the telemetry view of <paramref name="error"/>, or null when its redactor throws. An error with no telemetry view is returned
    /// unchanged.
    /// </summary>
    internal static Exception? Redact(Exception error) =>
        error is DownloadException download
            ? RedactDownloadError(download)
            : TelemetryRedaction.RedactTelemetryValue(error);

    /// <summary><c>redactDownloadError</c>: the URL of a failed download can carry a signature, so it is not published.</summary>
    private static DownloadException RedactDownloadError(DownloadException error) =>
        new("[redacted]", error.StatusCode, error.StatusText, "Failed to download a file.");
}
