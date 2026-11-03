// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Acp;

/// <summary>Options every ACP executor takes, whichever agent it starts. Upstream's <c>AcpAgentOptions</c> (<c>types.ts</c>).</summary>
public record AcpAgentOptions
{
    /// <summary>Variables added to the agent's environment, on top of this process's.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>
    /// A value of the session's <c>model</c> config option, such as <c>sonnet</c>. The session fails to start when the agent does not offer
    /// it; the error lists what it does offer. Unset, the agent's own default model runs.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>Appended to the built-in rules the agent gets with the first step.</summary>
    public string? System { get; init; }

    /// <summary>The executor name in reports.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// Options for an executor that runs agent steps on any coding agent over the Agent Client Protocol. Upstream's <c>AcpOptions</c>
/// (<c>types.ts</c>); its <c>command</c> and <c>args</c> are the arguments of <see cref="AcpExecutor.Create"/>.
/// </summary>
public sealed record AcpOptions : AcpAgentOptions
{
    /// <summary>A session mode id to select, such as Codex's <c>read-only</c>.</summary>
    public string? Mode { get; init; }

    /// <summary><c>_meta</c> for <c>session/new</c>: options only one agent reads (its adapter documents them). Values serialize as JSON.</summary>
    public IReadOnlyDictionary<string, object?>? SessionMeta { get; init; }
}
