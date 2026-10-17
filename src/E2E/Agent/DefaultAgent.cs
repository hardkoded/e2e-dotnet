// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Cache;

namespace E2E;

/// <summary>What the built-in agent is built from: an agents entry's <c>system</c> and its <c>tools</c> (upstream's <c>BuiltInAgentOptions</c>).</summary>
public sealed class BuiltInAgentOptions
{
    /// <summary>Appended to the built-in execution rules. Only the act loop reads it.</summary>
    public string? System { get; init; }

    /// <summary>
    /// Project tools offered to the model beside the agent's own, by name. A tool is offered only on the platforms it declares.
    /// </summary>
    public IReadOnlyDictionary<string, AgentTool>? Tools { get; init; }
}

/// <summary>Upstream's <c>createBuiltInAgent</c> (<c>agent/default-agent.ts</c>): the tool loop with the built-in rules, tools, and opening prompt.</summary>
public static class DefaultAgent
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

    /// <summary>
    /// Builds the built-in step executor: the model loop with the grammar tools of the target's engine and the project
    /// tools. It drives the step's own context, so it runs only where the runtime builds one, as a step's executor or inside a
    /// custom executor that hands its context on.
    /// </summary>
    public static IStepExecutor CreateBuiltInAgent(BuiltInAgentOptions? options = null)
    {
        options ??= new BuiltInAgentOptions();
        return ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = BuiltInAgent.Name,
            Version = BuiltInAgent.Version,
            System = _ => SystemFor(options.System),
            Tools = (context, _) => GrammarTools(context, options.Tools),
            BuildPrompt = Opening,
        });
    }

    // The execution rules, then the configured text.
    private static string SystemFor(string? system) =>
        string.IsNullOrWhiteSpace(system) ? BaseRules : BaseRules + "\n\n" + system;

    // The tools this engine can honor beside the project tools its platform may be offered, each run through the step's own actions.
    private static List<ToolLoopTool> GrammarTools(StepExecutorContext context, IReadOnlyDictionary<string, AgentTool>? tools)
    {
        var host = context.Host
            ?? throw new InvalidOperationException("The built-in agent drives the context of a step the runtime opened; it cannot run on a context built by hand.");
        var project = tools is null
            ? new Dictionary<string, AgentTool>()
            : tools.Where(pair => pair.Value.AppliesTo(context.Target.Platform)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var offered = project.Select(pair => AgentTools.Project(pair.Key, pair.Value)).ToList();
        var act = new ActTools(host.Scope, host.Feed);
        return AgentTools.CreateGrammarTools(host.Scope.EngineCapabilities, offered)
            .Select(definition => new ToolLoopTool(
                definition,
                async (call, token) =>
                {
                    var outcome = await act.ExecuteAsync(call, context.Actions, host.Accounting, project, token, context).ConfigureAwait(false);
                    return new ToolLoopResult(outcome.Content, !outcome.Succeeded);
                }))
            .ToList();
    }

    // Upstream's formatReplayedPrefix opens the prompt when a replay ran first.
    private static async Task<string> Opening(StepExecutorContext context)
    {
        var host = context.Host
            ?? throw new InvalidOperationException("The built-in agent drives the context of a step the runtime opened; it cannot run on a context built by hand.");
        var builder = new StringBuilder();
        if (context.ReplayedPrefix is { } prefix)
        {
            builder.Append("Replay stopped (").Append(ReplayHandOffReasons.Name(prefix.StopReason)).Append(") after these actions:\n");
            foreach (var action in prefix.ReplayedActions)
            {
                builder.Append("- ").Append(action).Append('\n');
            }

            if (prefix.UncertainAction is not null)
            {
                builder
                    .Append("WARNING: the next action (").Append(prefix.UncertainAction)
                    .Append(") failed with an UNKNOWN commit state: its input may have reached the app. Verify the current state before doing anything like it again.\n");
            }

            builder.Append("Continue from the current screen. Do not repeat an action that already had its effect.\n\n");
        }

        var parameters = host.Params;
        builder.Append("Goal: ").Append(Interpolate(context.Step.Instruction, parameters)).Append('\n');
        if (parameters is { Count: > 0 })
        {
            builder.Append("\nParams:\n");
            foreach (var pair in parameters)
            {
                builder.Append("- ").Append(pair.Key).Append(": ").Append(CacheKeys.Display(pair.Value)).Append('\n');
            }
        }

        if (context.Ledger.Length > 0)
        {
            builder.Append("\nCompleted steps:\n").Append(context.Ledger).Append('\n');
        }

        var screen = context.Start ?? await context.ObserveAsync().ConfigureAwait(false);
        builder.Append('\n').Append(screen.Text);
        return host.Scope.Redactor.Redact(builder.ToString());
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

        if (action.Modifiers is { Count: > 0 })
        {
            builder.Append(" with ").Append(string.Join('+', action.Modifiers)).Append(" held");
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
