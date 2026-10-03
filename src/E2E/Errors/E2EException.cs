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

    /// <summary>The step's account of what happened. The same text as <see cref="Exception.Message"/>.</summary>
    public string Explanation => Message;

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
}

/// <summary>Thrown by <see cref="TestContext.Skip"/> when a test stops early and is reported skipped.</summary>
public sealed class SkipException : E2EException
{
    public SkipException(string reason)
        : base("SKIPPED", reason)
    {
    }
}
