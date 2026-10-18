// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;

namespace E2E;

/// <summary>
/// A failure as a report keeps it (upstream's <c>SerializedError</c>): classified, redacted, and bounded. The structured
/// <c>details</c> and the stack text are not carried: the port's exceptions have no details, and no report reads a stack yet.
/// </summary>
/// <param name="Category"><c>test</c>, <c>configuration</c>, <c>infrastructure</c>, or <c>interrupted</c>.</param>
/// <param name="Code">The stable code of the failure.</param>
/// <param name="Message">The message, redacted, sanitized, and cut to 8 KiB.</param>
/// <param name="Retryable">Whether the caller may observe again and repeat the operation.</param>
/// <param name="Source">The line in the test file the failure unwound through, when the stack named one.</param>
internal sealed record SerializedError(string Category, string Code, string Message, bool Retryable, SourceLocation? Source);

/// <summary>
/// Upstream's <c>classifyError</c> and <c>serializeError</c> (<c>internal/errors.ts</c>): what a thrown value is, in the
/// runner's taxonomy, and the bounded shape a report keeps of it.
/// </summary>
internal static class ErrorSerializer
{
    private const int MaxMessageBytes = 8192;

    private const int MaxCauseDepth = 3;

    /// <summary>The code a cancelled step carries.</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>The code, category, and message of a thrown value. A value the runner does not know is a test failure with code <c>ERROR</c>.</summary>
    public static (string Category, string Code, string Message, bool Retryable) Classify(Exception cause)
    {
        return cause switch
        {
            AgentException agent => ("test", agent.Code, agent.Message, false),
            TestException test => ("test", test.Code, test.Message, false),
            ConfigurationException configuration => ("configuration", configuration.Code, configuration.Message, false),
            EngineException engine => ("infrastructure", engine.Code, engine.Message, engine.Retryable),
            E2EException other => ("test", other.Code, other.Message, false),
            OperationCanceledException => ("interrupted", Cancelled, cause.Message, false),
            _ => ("test", "ERROR", MessageWithCauses(cause), false),
        };
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
            source ?? LocateIn(cause, projectRoot));
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
