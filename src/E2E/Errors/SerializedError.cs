// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;

namespace E2E;

/// <summary>
/// A failure as a report keeps it (upstream's <c>SerializedError</c>): classified, redacted, and bounded. The stack text is not
/// carried: no report reads a stack yet.
/// </summary>
/// <param name="Category"><c>test</c>, <c>configuration</c>, <c>infrastructure</c>, or <c>interrupted</c>.</param>
/// <param name="Code">The stable code of the failure.</param>
/// <param name="Message">The message, redacted, sanitized, and cut to 8 KiB.</param>
/// <param name="Retryable">Whether the caller may observe again and repeat the operation.</param>
/// <param name="Source">The line in the test file the failure unwound through, when the stack named one.</param>
/// <param name="Details">The structured facts of the failure, redacted, sanitized, and bounded; null when there are none.</param>
internal sealed record SerializedError(string Category, string Code, string Message, bool Retryable, SourceLocation? Source, ErrorDetails? Details = null);

/// <summary>
/// Upstream's <c>classifyError</c> and <c>serializeError</c> (<c>internal/errors.ts</c>): what a thrown value is, in the
/// runner's taxonomy, and the bounded shape a report keeps of it.
/// </summary>
internal static class ErrorSerializer
{
    private const int MaxMessageBytes = 8192;

    private const int MaxCauseDepth = 3;

    /// <summary>Bytes one detail text keeps.</summary>
    private const int MaxDetailBytes = 2048;

    /// <summary>The code a cancelled step carries.</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>The code, category, and message of a thrown value. A value the runner does not know is a test failure with code <c>ERROR</c>.</summary>
    public static (string Category, string Code, string Message, bool Retryable) Classify(Exception cause)
    {
        return cause switch
        {
            AgentException agent => (AgentError.CategoryByCode.GetValueOrDefault(agent.Code, "test"), agent.Code, agent.Message, false),
            TestException test => ("test", test.Code, test.Message, false),
            ConfigurationException configuration => ("configuration", configuration.Code, configuration.Message, false),
            EngineException engine => ("infrastructure", engine.Code, engine.Message, engine.Retryable),
            E2EException other => ("test", other.Code, other.Message, false),
            OperationCanceledException => ("interrupted", Cancelled, cause.Message, false),
            _ => ("test", "ERROR", MessageWithCauses(cause), false),
        };
    }

    /// <summary>
    /// Upstream's <c>translateEngineError</c> (<c>internal/errors.ts</c>): an engine failure as the test sees it, the engine error kept as the
    /// cause. A stale node is <c>NOT_FOUND</c> (upstream <c>LOCATOR_NOT_FOUND</c>), a missing frame the same, an ambiguous frame is
    /// <c>STRICT_MODE</c> (<c>LOCATOR_AMBIGUOUS</c>), a node that cannot take the action, an action that may have committed, and an operation
    /// that timed out are <c>ACTION_FAILED</c>, and no open app is <c>APP_NOT_OPEN</c>. <c>CANCELLED</c> is an infrastructure failure,
    /// <c>UNSUPPORTED_CAPABILITY</c> a configuration failure, and <c>ENGINE_FAILURE</c>, any other engine code, and any error that is no
    /// e2e error are an infrastructure <c>ENGINE_FAILURE</c>. An e2e error that is no engine error is returned as it is.
    /// </summary>
    /// <param name="cause">What the engine threw.</param>
    /// <param name="suffix">Text appended to the message, such as <c>: getByRole("button")</c>.</param>
    public static Exception TranslateEngineError(Exception cause, string suffix = "")
    {
        // A caller's cancel must surface as cancellation, not as an engine failure (.NET-only).
        if (cause is OperationCanceledException or (E2EException and not EngineException))
        {
            return cause;
        }

        if (cause is not EngineException engine)
        {
            return new EngineException(EngineErrorCodes.EngineFailure, cause.Message, cause);
        }

        return engine.Code switch
        {
            EngineErrorCodes.NodeStale => new TestException("NOT_FOUND", "node became stale" + suffix, engine),
            EngineErrorCodes.FrameNotFound => new TestException("NOT_FOUND", engine.Message + suffix, engine),
            EngineErrorCodes.FrameAmbiguous => new TestException("STRICT_MODE", engine.Message + suffix, engine),
            EngineErrorCodes.NotActionable or EngineErrorCodes.ActionMayHaveCommitted => new TestException("ACTION_FAILED", engine.Message + suffix, engine),
            EngineErrorCodes.OperationTimeout => new TestException("ACTION_FAILED", "operation timed out" + suffix, engine),
            EngineErrorCodes.Cancelled => new EngineException(EngineErrorCodes.Cancelled, "operation cancelled", engine),
            EngineErrorCodes.UnsupportedCapability => new ConfigurationException(EngineErrorCodes.UnsupportedCapability, engine.Message, engine),
            EngineErrorCodes.InvalidState => new TestException("APP_NOT_OPEN", engine.Message, engine),
            _ => new EngineException(EngineErrorCodes.EngineFailure, engine.Message, engine),
        };
    }

    /// <summary>Maps a category to its process exit code (upstream's <c>exitCodeForCategory</c>).</summary>
    public static int ExitCodeForCategory(string category) => category switch
    {
        "configuration" => 2,
        "infrastructure" => 3,
        "internal" => 4,
        "interrupted" => 130,
        _ => 1,
    };

    /// <summary>Combines exit codes for mixed outcomes using precedence 130, 4, 3, 2, 1, 0 (upstream's <c>combineExitCodes</c>).</summary>
    public static int CombineExitCodes(IEnumerable<int> codes)
    {
        var all = codes.ToList();
        return new[] { 130, 4, 3, 2, 1 }.FirstOrDefault(all.Contains);
    }

    /// <summary>
    /// The shape a report keeps of <paramref name="cause"/>. The message is redacted by <paramref name="redact"/> (the process
    /// redactor without one), sanitized, and bounded: an assertion that observed a secret on screen, or a message that quoted one,
    /// must not carry it into the report. <paramref name="source"/> names the line when the caller knows it better than the stack does.
    /// </summary>
    public static SerializedError Serialize(Exception cause, string? projectRoot = null, Func<string, string>? redact = null, SourceLocation? source = null)
    {
        var (category, code, message, retryable) = Classify(cause);
        redact ??= ErrorRedaction.Redact;
        return new SerializedError(
            category,
            code,
            TextRules.TruncateUtf8(TextRules.SanitizeText(redact(message)), MaxMessageBytes),
            retryable,
            source ?? LocateIn(cause, projectRoot),
            cause is E2EException { Details: { } details } ? BoundedDetails(details, redact) : null);
    }

    // Details as the report carries them: text redacted, sanitized, and bounded, empty text dropped, nothing when nothing is left.
    private static ErrorDetails? BoundedDetails(ErrorDetails details, Func<string, string> redact)
    {
        string? Text(string? text) => string.IsNullOrEmpty(text) ? null : TextRules.TruncateUtf8(TextRules.SanitizeText(redact(text)), MaxDetailBytes);
        var bounded = new ErrorDetails
        {
            Locator = Text(details.Locator),
            Expected = Text(details.Expected),
            Observed = Text(details.Observed),
            Matches = details.Matches,
            Role = Text(details.Role),
            Name = Text(details.Name),
            TestId = Text(details.TestId),
            WaitedMs = details.WaitedMs,
        };
        return bounded == new ErrorDetails() ? null : bounded;
    }

    // The failure's own frames first, then those of the causes it wraps, innermost last.
    private static SourceLocation? LocateIn(Exception cause, string? projectRoot)
    {
        for (var current = cause; current is not null; current = current.InnerException)
        {
            if (Source.Locate(new StackTrace(current, fNeedFileInfo: true), projectRoot) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// What a provisioning hook's failure is reported as: an e2e error stays itself, a caller's cancel is a cancel (an engine may call a
    /// lease that was cancelled a failed one), and anything else, such as an installer's plain exception, is an engine failure. Upstream's
    /// <c>translateProvisioningError</c> (<c>internal/errors.ts</c>).
    /// </summary>
    internal static Exception TranslateProvisioningError(Exception cause, string suffix, CancellationToken cancellationToken)
    {
        if (cause is E2EException { Code: EngineErrorCodes.Cancelled })
        {
            return cause;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new EngineException(EngineErrorCodes.Cancelled, "preparing the engine was cancelled", cause);
        }

        return cause is E2EException or OperationCanceledException ? cause : new EngineException(EngineErrorCodes.EngineFailure, cause.Message + suffix, cause);
    }

    /// <summary>
    /// An error's message followed by its causes, innermost last: <c>fetch failed: connect ECONNREFUSED</c>. The detail an
    /// operator acts on is often in the inner exception, which a bare message hides. A message already present is not repeated.
    /// </summary>
    internal static string MessageWithCauses(Exception error)
    {
        var parts = new List<string> { error.Message };
        var current = error.InnerException;
        for (var depth = 0; current is not null && depth < MaxCauseDepth; depth++)
        {
            if (current.Message.Length > 0 && !parts.Contains(current.Message, StringComparer.Ordinal))
            {
                parts.Add(current.Message);
            }

            current = current.InnerException;
        }

        return string.Join(": ", parts);
    }
}
