// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Acp;

/// <summary>Options every ACP executor takes, whichever agent it starts. Upstream's <c>AcpAgentOptions</c> (<c>packages/acp/src/types.ts</c>).</summary>
public class AcpAgentOptions
{
    /// <summary>Variables added to the agent's environment, on top of this process's.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>
    /// A value of the session's <c>model</c> config option, such as <c>sonnet</c>. The session fails to start when the agent does
    /// not offer it; the error lists what it does offer. Omitted, the agent's own default model runs.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>Appended to the built-in rules the agent gets with the first step.</summary>
    public string? System { get; init; }

    /// <summary>The executor name in reports.</summary>
    public string? Name { get; init; }
}

/// <summary>Options for an executor that runs agent steps on any coding agent over the Agent Client Protocol. Upstream's <c>AcpOptions</c>.</summary>
public sealed class AcpOptions : AcpAgentOptions
{
    /// <summary>
    /// The command that starts the agent in ACP mode on stdio, such as Cursor's <c>agent</c> with <c>["acp"]</c>. The agent signs in
    /// the way it does on its own (its CLI login or its own API key variable).
    /// </summary>
    public required string Command { get; init; }

    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>A session mode id to select, such as Codex's <c>read-only</c>.</summary>
    public string? Mode { get; init; }

    /// <summary><c>_meta</c> for <c>session/new</c>: options only one agent reads (its adapter documents them).</summary>
    public IReadOnlyDictionary<string, object?>? SessionMeta { get; init; }
}
