// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>How a run ended. Upstream's <c>RunStatus</c>, with the values a single run in the port can have.</summary>
public enum RunStatus
{
    Passed,

    /// <summary>A test or an agent step failed.</summary>
    Failed,

    /// <summary>Credentials, the environment, test setup, or the agent's own budget stopped the run, rather than the app misbehaving.</summary>
    Blocked,
}

/// <summary>The run's exit code and status, derived from the error that ended it, as upstream's runner derives them.</summary>
internal static class Runner
{
    /// <summary>
    /// The process exit code for an error: 0 for none, 1 for a test failure, 2 for a configuration or agent policy error, 3 for an
    /// engine, app, or model provider failure. The class of an agent code follows upstream's table.
    /// </summary>
    public static int ExitCodeOf(E2EException? error) => error switch
    {
        null => 0,
        AgentException agent => AgentCodeExit(agent.Code),
        TestException => 1,
        ConfigurationException => 2,
        _ => 3,
    };

    public static RunStatus StatusOf(E2EException? error) => error switch
    {
        null => RunStatus.Passed,
        AgentException { Blocked: true } => RunStatus.Blocked,
        _ => RunStatus.Failed,
    };

    private static int AgentCodeExit(string code) => ErrorSerializer.ExitCodeForCategory(AgentError.CategoryByCode.GetValueOrDefault(code, "test"));
}
