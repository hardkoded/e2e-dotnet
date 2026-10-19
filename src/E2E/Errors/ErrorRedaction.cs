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
    /// it is an engine, agent, or configuration exception. The <paramref name="details"/> text is redacted too.
    /// </summary>
    public static TestException Failure(string code, string message, Exception? cause, Func<string, string> redact, ErrorDetails? details = null)
    {
        var inner = Cause(cause, redact);
        var redacted = details is null ? null : Redact(details, redact);
        return inner is null
            ? new TestException(code, redact(message)) { Details = redacted }
            : new TestException(code, redact(message), inner) { Details = redacted };
    }

    private static ErrorDetails Redact(ErrorDetails details, Func<string, string> redact) => details with
    {
        Locator = details.Locator is null ? null : redact(details.Locator),
        Expected = details.Expected is null ? null : redact(details.Expected),
        Observed = details.Observed is null ? null : redact(details.Observed),
        Role = details.Role is null ? null : redact(details.Role),
        Name = details.Name is null ? null : redact(details.Name),
        TestId = details.TestId is null ? null : redact(details.TestId),
    };

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
            EngineException engine => new EngineException(engine.Code, message, engine.Retryable, inner) { Details = Detail(engine, redact) },
            AgentException agent when inner is null => new AgentException(agent.Code, message, agent.Blocked) { Screenshot = agent.Screenshot, Details = Detail(agent, redact) },
            AgentException agent => new AgentException(agent.Code, message, agent.Blocked, inner) { Screenshot = agent.Screenshot, Details = Detail(agent, redact) },
            ConfigurationException config when inner is null => new ConfigurationException(config.Code, message) { Details = Detail(config, redact) },
            ConfigurationException config => new ConfigurationException(config.Code, message, inner) { Details = Detail(config, redact) },
            E2EException e2e when inner is null => new TestException(e2e.Code, message) { Details = Detail(e2e, redact) },
            E2EException e2e => new TestException(e2e.Code, message, inner) { Details = Detail(e2e, redact) },
            _ when inner is null => new TestException("ERROR", message),
            _ => new TestException("ERROR", message, inner),
        };
    }

    private static ErrorDetails? Detail(E2EException error, Func<string, string> redact) => error.Details is { } details ? Redact(details, redact) : null;

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
