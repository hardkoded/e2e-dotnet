// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The one way an error leaves a host's opening: classified, redacted, and stripped of its cause. Upstream's <c>run/redact-opening.ts</c>.</summary>
/// <remarks>.NET-only name: a C# member cannot share the name of its type, so the file's <c>redactOpening</c> lives in this class.</remarks>
internal static class OpeningRedaction
{
    /// <summary>Opening errors go straight to the caller (another test runner, an <c>E2E</c> program), never through a report writer. Upstream's <c>redactOpening</c>.</summary>
    public static async Task<T> RedactOpening<T>(Func<Task<T>> open)
    {
        try
        {
            return await open().ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            // Arbitrary engine causes can carry non-text secret data; only the sanitized record crosses out. The stack and the inner exception stay behind.
            var safe = ErrorSerializer.Serialize(cause, null, OpenSession.Redact);
            throw safe.Category switch
            {
                "configuration" => new ConfigurationException(safe.Code, safe.Message),
                "infrastructure" => new EngineException(safe.Code, safe.Message, safe.Retryable),
                _ => new E2EException(safe.Code, safe.Message) { Details = safe.Details },
            };
        }
    }
}
