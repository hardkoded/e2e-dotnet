// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Cache;

namespace E2E;

/// <summary>What the built-in agent reads from the step's runtime (upstream's <c>BuiltInAgentOptions</c>, with the port's plumbing).</summary>
internal sealed class BuiltInAgentOptions
{
    public required Agent Host { get; init; }

    public required AttemptScope Scope { get; init; }

    public required ObservationFeed Feed { get; init; }

    public required ResolvedAgent Agent { get; init; }

    public required IReadOnlyDictionary<string, AgentTool> ProjectTools { get; init; }

    public required IReadOnlyList<ModelTool> OfferedProject { get; init; }

    public required CancellationToken CancellationToken { get; init; }
}

/// <summary>Upstream's <c>createBuiltInAgent</c> (<c>agent/default-agent.ts</c>): the tool loop with the built-in rules and opening prompt.</summary>
internal static class DefaultAgent
{
    // Upstream's BASE_RULES.
    private const string BaseRules =
        "You are an e2e test agent. Act only by calling tools. Finish with done. " +
        "Target a control by its role and name from the screen snapshot. " +
        "done status is passed, failed, or blocked. " +
        "blocked means credentials, the environment, or test setup prevented a verdict, and requires a code. " +
        "Never type a secret value. Call fill_secret with the secret name. " +
        "A secret looks like <secret:name>. " +
        "If what you need is not on the screen, scroll to it.";

    public static IStepExecutor CreateBuiltInAgent(BuiltInAgentOptions options) =>
        ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = BuiltInAgent.Name,
            Version = BuiltInAgent.Version,
            System = SystemFor(options.Agent),
            Opening = context => Opening(options.Scope, context),
            Host = options.Host,
            Scope = options.Scope,
            Feed = options.Feed,
            Agent = options.Agent,
            ProjectTools = options.ProjectTools,
            OfferedProject = options.OfferedProject,
            CancellationToken = options.CancellationToken,
        });

    // The project tools this engine's platform may be offered.
    public static IReadOnlyDictionary<string, AgentTool> ProjectToolsFor(ResolvedAgent agent, string platform) =>
        agent.Tools is null
            ? new Dictionary<string, AgentTool>()
            : agent.Tools.Where(pair => pair.Value.AppliesTo(platform)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    // The configured system text follows the built-in rules, and the project context follows both.
    private static string SystemFor(ResolvedAgent agent)
    {
        var system = BaseRules;
        if (!string.IsNullOrWhiteSpace(agent.System))
        {
            system += "\n\n" + agent.System;
        }

        if (!string.IsNullOrWhiteSpace(agent.Context))
        {
            system += "\n\nProject context:\n" + agent.Context;
        }

        return system;
    }

    // Upstream's formatReplayedPrefix opens the prompt when a replay ran first.
    private static string Opening(AttemptScope scope, StepExecutorContext context)
    {
        var builder = new StringBuilder();
        if (context.ReplayedPrefix is { } prefix)
        {
            builder.Append("Replay stopped (").Append(ReplayHandOffReasons.Name(prefix.StopReason)).Append(") after these actions:\n");
            foreach (var action in prefix.Actions)
            {
                builder.Append("- ").Append(Describe(action)).Append('\n');
            }

            builder.Append("Continue from the current screen. Do not repeat an action that already had its effect.\n\n");
        }

        var parameters = context.Step.Params;
        builder.Append("Goal: ").Append(Interpolate(context.Step.Instruction, parameters)).Append('\n');
        if (parameters is { Count: > 0 })
        {
            builder.Append("\nParams:\n");
            foreach (var pair in parameters)
            {
                builder.Append("- ").Append(pair.Key).Append(": ").Append(CacheKeys.Display(pair.Value)).Append('\n');
            }
        }

        if (scope.Completed.Count > 0)
        {
            builder.Append("\nCompleted steps:\n");
            foreach (var step in scope.Completed)
            {
                builder.Append("- ").Append(step).Append('\n');
            }
        }

        builder.Append('\n').Append(context.Start.Text);
        return scope.Redactor.Redact(builder.ToString());
    }

    private static string Interpolate(string instruction, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return instruction;
        }

        var text = instruction;
        foreach (var pair in parameters)
        {
            text = text.Replace("{" + pair.Key + "}", CacheKeys.Display(pair.Value), StringComparison.Ordinal);
        }

        return text;
    }

    internal static string Describe(RecordedAction action)
    {
        var builder = new StringBuilder(action.Kind);
        if (action.Role is not null || action.Name is not null || action.TestId is not null)
        {
            builder.Append(' ').Append(action.Role ?? "node");
            if (action.Name is not null)
            {
                builder.Append(" \"").Append(action.Name).Append('"');
            }
            else if (action.TestId is not null)
            {
                builder.Append(" testId=").Append(action.TestId);
            }
        }

        if (action.Url is not null)
        {
            builder.Append(' ').Append(action.Url);
        }

        if (action.Text is not null)
        {
            builder.Append(" \"").Append(action.Text).Append('"');
        }

        if (action.Direction is not null)
        {
            builder.Append(' ').Append(action.Direction);
        }

        if (action.Times is > 1)
        {
            builder.Append(" x").Append(action.Times.Value.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
