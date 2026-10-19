// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>Base exception for the agent and engines. <see cref="Code"/> is stable.</summary>
public class E2EException : Exception
{
    public E2EException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public E2EException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable code, such as <c>ASSERTION_FAILED</c>.</summary>
    public string Code { get; }

    /// <summary>Structured facts of the failure beside its message, when the thrower had them. See <see cref="ErrorDetails"/>.</summary>
    public ErrorDetails? Details { get; init; }
}

/// <summary>
/// Structured facts of one failure, beside its prose message (upstream's <c>ErrorDetails</c>, <c>internal/errors.ts</c>). An assertion
/// carries what it expected and observed and how many nodes matched. A locator failure carries the locator as written, what it asked
/// for, and how long it waited. A report bounds the text fields.
/// </summary>
public sealed record ErrorDetails
{
    /// <summary>The locator as written, such as <c>getByRole("button", "Save")</c>.</summary>
    public string? Locator { get; init; }

    /// <summary>What an assertion expected.</summary>
    public string? Expected { get; init; }

    /// <summary>What an assertion observed.</summary>
    public string? Observed { get; init; }

    /// <summary>How many nodes the locator matched.</summary>
    public int? Matches { get; init; }

    /// <summary>The role a locator asked for.</summary>
    public string? Role { get; init; }

    /// <summary>The name, or the text, a locator asked for.</summary>
    public string? Name { get; init; }

    /// <summary>The test id a locator asked for.</summary>
    public string? TestId { get; init; }

    /// <summary>How long the locator waited, in milliseconds.</summary>
    public long? WaitedMs { get; init; }
}

/// <summary>A locator, expectation, or test failed.</summary>
public sealed class TestException : E2EException
{
    public TestException(string code, string message)
        : base(code, message)
    {
    }

    public TestException(string code, string message, Exception inner)
        : base(code, message, inner)
    {
    }
}

/// <summary>An agent step failed, was blocked, or could not talk to its model.</summary>
public sealed class AgentException : E2EException
{
    public AgentException(string code, string message)
        : base(code, message)
    {
    }

    public AgentException(string code, string message, Exception inner)
        : base(code, message, inner)
    {
    }

    public AgentException(string code, string message, bool blocked)
        : base(code, message)
    {
        Blocked = blocked;
    }

    public AgentException(string code, string message, bool blocked, Exception inner)
        : base(code, message, inner)
    {
        Blocked = blocked;
    }

    /// <summary>The step's account of what happened. The same text as <see cref="Exception.Message"/>.</summary>
    public string Explanation => Message;

    /// <summary>
    /// Where a failed <c>AssertAsync</c> kept its redacted screenshot, relative to the project root. Null when none was taken:
    /// <c>Screenshot = false</c>, a secret was filled, or the engine captures no pixels.
    /// </summary>
    public string? Screenshot { get; init; }

    /// <summary>
    /// True when the step failed because the test's own <c>OnAction</c> hook threw. That failure says nothing about the
    /// actions, so the step's recording is neither kept nor evicted for it. Upstream's <c>ActionHookError</c>.
    /// </summary>
    internal bool FromActionHook { get; init; }

    /// <summary>
    /// True when credentials, the environment, test setup, or the agent's own budget stopped the step,
    /// rather than the app misbehaving.
    /// </summary>
    public bool Blocked { get; }
}

/// <summary>The engine refused an operation or could not drive the app.</summary>
public sealed class EngineException : E2EException
{
    public EngineException(string code, string message)
        : base(code, message)
    {
    }

    public EngineException(string code, string message, Exception inner)
        : base(code, message, inner)
    {
    }

    /// <summary>
    /// Creates an engine error that may claim to be retryable. Only <see cref="EngineErrorCodes.NodeStale"/> and
    /// <see cref="EngineErrorCodes.FrameNotFound"/> can be retried. Any other retryable claim becomes a
    /// non-retryable <see cref="EngineErrorCodes.EngineFailure"/>, so an engine can never ask for an action that
    /// may have committed to run again.
    /// </summary>
    public EngineException(string code, string message, bool retryable, Exception? inner = null)
        : base(Coerce(code, retryable), message, inner!)
    {
        Retryable = retryable && EngineErrorCodes.IsRetryable(code);
    }

    /// <summary>Whether the caller may observe again and repeat the operation.</summary>
    public bool Retryable { get; }

    private static string Coerce(string code, bool retryable)
    {
        return retryable && !EngineErrorCodes.IsRetryable(code) ? EngineErrorCodes.EngineFailure : code;
    }
}

/// <summary>Stable engine error codes, as in the upstream engine contract.</summary>
public static class EngineErrorCodes
{
    /// <summary>The node or document a ref named is gone. Observe again and retry.</summary>
    public const string NodeStale = "NODE_STALE";

    public const string FrameNotFound = "FRAME_NOT_FOUND";

    public const string FrameAmbiguous = "FRAME_AMBIGUOUS";

    /// <summary>The target never became visible, enabled, stable, or editable. Nothing was dispatched.</summary>
    public const string NotActionable = "NOT_ACTIONABLE";

    /// <summary>The input was dispatched before the failure, so the action may have taken effect. Do not repeat it blindly.</summary>
    public const string ActionMayHaveCommitted = "ACTION_MAY_HAVE_COMMITTED";

    /// <summary>A navigation, read, or keyboard operation ran out of time.</summary>
    public const string OperationTimeout = "OPERATION_TIMEOUT";

    public const string Cancelled = "CANCELLED";

    public const string UnsupportedCapability = "UNSUPPORTED_CAPABILITY";

    public const string InvalidState = "INVALID_STATE";

    /// <summary>Any other engine failure.</summary>
    public const string EngineFailure = "ENGINE_FAILURE";

    /// <summary>Whether <paramref name="code"/> describes a repeatable read and may be retried.</summary>
    public static bool IsRetryable(string? code)
    {
        return code is NodeStale or FrameNotFound;
    }
}

/// <summary>Thrown by <see cref="TestContext.Skip"/> when a test stops early and is reported skipped.</summary>
public sealed class SkipException : E2EException
{
    public SkipException(string reason)
        : base("SKIPPED", reason)
    {
    }
}

/// <summary><c>e2e.config.json</c> is invalid. The code is <c>INVALID_CONFIG</c>.</summary>
public sealed class ConfigurationException : E2EException
{
    public ConfigurationException(string code, string message)
        : base(code, message)
    {
    }

    public ConfigurationException(string code, string message, Exception inner)
        : base(code, message, inner)
    {
    }
}
