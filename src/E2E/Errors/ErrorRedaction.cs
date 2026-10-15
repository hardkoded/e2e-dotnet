// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Redacts the text an error carries. Upstream redacts at the report boundary, <c>serializeError</c> in
/// <c>internal/errors.ts</c>, with the process redactor <c>setErrorRedactor</c> installs. The port has no report
/// boundary, so each place that builds a message from page text redacts it here. Sessions run side by side, so
/// the process redactor is every live session's redactor in turn.
/// </summary>
internal static class ErrorRedaction
{
    private static readonly Lock Gate = new();

    private static Func<string, string>[] _live = [];

    /// <summary>Adds a session's redactor to the process redactor until the result is disposed.</summary>
    public static IDisposable Install(Func<string, string> redact)
    {
        lock (Gate)
        {
            _live = [.. _live, redact];
        }

        return new Installed(redact);
    }

    /// <summary>The process redactor: <paramref name="text"/> with the secrets of every live session replaced.</summary>
    public static string Redact(string text)
    {
        Func<string, string>[] live;
        lock (Gate)
        {
            live = _live;
        }

        return live.Aggregate(text, static (current, redact) => redact(current));
    }

    /// <summary>
    /// A failure with its message and the messages of its causes redacted. A cause that carries no secret stays the
    /// exception that was thrown; one that does is rebuilt with the redacted text, and keeps its type and code when
    /// it is an engine, agent, or configuration exception.
    /// </summary>
    public static TestException Failure(string code, string message, Exception? cause, Func<string, string> redact)
    {
        var inner = Cause(cause, redact);
        return inner is null ? new TestException(code, redact(message)) : new TestException(code, redact(message), inner);
    }

    internal static Exception? Cause(Exception? cause, Func<string, string> redact)
    {
        if (cause is null)
        {
            return null;
        }

        var inner = Cause(cause.InnerException, redact);
        var message = redact(cause.Message);
        if (inner == cause.InnerException && string.Equals(message, cause.Message, StringComparison.Ordinal))
        {
            return cause;
        }

        return cause switch
        {
            EngineException engine => new EngineException(engine.Code, message, engine.Retryable, inner),
            AgentException agent when inner is null => new AgentException(agent.Code, message, agent.Blocked),
            AgentException agent => new AgentException(agent.Code, message, agent.Blocked, inner),
            ConfigurationException config when inner is null => new ConfigurationException(config.Code, message),
            ConfigurationException config => new ConfigurationException(config.Code, message, inner),
            E2EException e2e when inner is null => new TestException(e2e.Code, message),
            E2EException e2e => new TestException(e2e.Code, message, inner),
            _ when inner is null => new TestException("ERROR", message),
            _ => new TestException("ERROR", message, inner),
        };
    }

    private sealed class Installed(Func<string, string> redact) : IDisposable
    {
        public void Dispose()
        {
            lock (Gate)
            {
                var index = Array.IndexOf(_live, redact);
                if (index >= 0)
                {
                    _live = [.. _live[..index], .. _live[(index + 1)..]];
                }
            }
        }
    }
}
