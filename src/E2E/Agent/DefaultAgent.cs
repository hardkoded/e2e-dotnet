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
    /// <summary>
    /// Upstream's <c>BASE_RULES</c>: the built-in agent's guidance for its action tools (<see cref="AgentTools.CreateGrammarTools"/>). Begin the
    /// <see cref="ToolLoopExecutorOptions.System"/> of an executor that offers the same tools with it. The verdict rules are <see cref="AgentTools.VerdictRules"/>,
    /// which the tool loop adds itself.
    /// </summary>
    public const string BaseRules =
        "You are an e2e test agent. Act only by calling tools. " +
        "Target a control by its role and name from the screen snapshot. " +
        "Never type a secret value. Call fill_secret with the secret name. " +
        "A secret looks like <secret:name>. " +
        "If what you need is not on the screen, scroll to it. " +
        "screenshot attaches the viewport's pixels when the tree lacks what you need (a shape on a canvas, a pin on a map, a region of an image, a control inside a system sheet) or contradicts what you expect; " +
        "once you have one, every action result carries a fresh screenshot so you can see what the action did. " +
        "tap_at, hover_at, drag_at, type_at, press_at, and select_at act at a point in the latest screenshot's pixel coordinates. " +
        "tap_at, hover_at, and drag_at act on the bare points where the engine can, and on the listed controls under them by id where it cannot; " +
        "type_at, press_at, and select_at act on a listed control under the point by its id. " +
        "Prefer ids: tap, fill, press, and select by id whenever the screen lists the target, " +
        "use the point tools only for a target the screen does not list, " +
        "and take a screenshot before guessing a point rather than aiming from memory or from what you expect to be drawn.";

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
            PrepareMessages = (messages, _) => new PreparedMessages(TranscriptCompaction.CompactScreenshotHistory(TranscriptCompaction.CompactScreenHistory(messages))),
            Tools = (context, _) => AgentTools.GrammarTools(context, options.Tools),
            BuildMessages = Opening,
        });
    }

    // The execution rules, then the configured text.
    private static string SystemFor(string? system) =>
        string.IsNullOrWhiteSpace(system) ? BaseRules : BaseRules + "\n\n" + system;

    // Upstream's formatReplayedPrefix: the mid-step hand-off notice. The agent continues from live state; redoing a replayed
    // action would double-commit a mutation.
    private static string FormatReplayedPrefix(ReplayedPrefix prefix)
    {
        var lines = new List<string> { "Cached replay already performed these recorded actions for this step:" };
        for (var index = 0; index < prefix.ReplayedActions.Count; index++)
        {
            lines.Add($"{index + 1}. {prefix.ReplayedActions[index]}");
        }

        lines.Add($"Replay stopped ({ReplayHandOffReasons.Name(prefix.StopReason)}) after {prefix.ReplayedActions.Count} of {prefix.TotalActions} recorded actions.");
        if (prefix.StopReason == ReplayHandOffReason.EndMismatch)
        {
            lines.Add("Every recorded action ran, but the screen does not show the recorded end state. Check whether the step actually took effect before doing anything — the recorded flow may have silently failed to commit.");
        }

        if (prefix.UncertainAction is not null)
        {
            lines.Add($"WARNING: the next action ({prefix.UncertainAction}) failed with an UNKNOWN commit state — its input may have reached the app. Verify the current state before doing anything like it again.");
        }

        lines.Add("Continue the step from the CURRENT screen state shown below — do NOT redo the actions above.");
        return string.Join('\n', lines);
    }

    private static async Task<IReadOnlyList<ModelMessage>> Opening(StepExecutorContext context)
    {
        var host = context.Host
            ?? throw new InvalidOperationException("The built-in agent drives the context of a step the runtime opened; it cannot run on a context built by hand.");
        var builder = new StringBuilder();
        if (Prompts.TestTitleLine(context.Attempt.TitlePath) is { } title)
        {
            builder.Append(title).Append("\n\n");
        }

        if (context.ReplayedPrefix is { } prefix)
        {
            builder.Append(FormatReplayedPrefix(prefix)).Append("\n\n");
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

        // The opening look is tree-only, unless no listed interactive node exists at all: then the opening prompt carries a screenshot,
        // because the tree describes a canvas, a game, or a semantics-free native screen too poorly to act on, and the model would only
        // spend a turn asking for one. One listed control is enough to leave the decision to the model; a small page is not a blind one.
        var screen = context.Start ?? await context.ObserveAsync().ConfigureAwait(false);
        if (screen.Pixels is null && AgentObservation.InteractiveNodeCount(screen.Text) == 0)
        {
            screen = await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true }).ConfigureAwait(false);
        }

        builder.Append('\n').Append(screen.Text);
        if (screen.Pixels is { } pixels)
        {
            PixelMode.For(context).Shown(pixels, context.Host?.Feed.Latest?.Viewport);
            builder.Append("\n\n").Append(PixelMode.Note(pixels));
        }
        else if (screen.PixelsWithheld is { } withheld)
        {
            builder.Append("\n\nNo screenshot: ").Append(PixelMode.WithheldAdvice(withheld));
        }

        return
        [
            new ModelMessage
            {
                Role = "user",
                Content = host.Scope.Redactor.Redact(builder.ToString()),
                Images = screen.Pixels is { } shot ? [new ModelImage(shot.Data, shot.MediaType, shot.Width, shot.Height)] : null,
            },
        ];
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

    private static string Point(Engine.Point point) =>
        "(" + point.X.ToString("R", CultureInfo.InvariantCulture) + ", " + point.Y.ToString("R", CultureInfo.InvariantCulture) + ")";

    private static string PointSuffix(RecordedPointPlace? place) =>
        place?.Target is { } target ? " on " + RecordedTarget.TargetLabel(target) : "";

    // How the pointer verbs read in a summary, upstream's NODE_ACTION_PROSE: the rest read as their kind.
    private static readonly Dictionary<string, string> Prose = new(StringComparer.Ordinal)
    {
        ["doubleTap"] = "double-tap",
        ["longPress"] = "long-press",
        ["secondaryTap"] = "secondary-tap",
        ["hover"] = "hover over",
    };

    internal static string Describe(RecordedAction action)
    {
        // Upstream's summaries of the keyboard actions: no node, so the focused field stands for it. The caller redacts the text.
        switch (action.Kind)
        {
            case "typeText":
                return "type " + UploadPaths.Quote(action.Value ?? "") + " into the focused field" + (action.Replace == true ? ", replacing its value" : "");
            case "pressKey":
                return "press " + UploadPaths.Quote(action.Key ?? "") + " on the focused field";
        }

        var builder = new StringBuilder(action.Kind switch { "tapAt" => "tap", "hoverAt" => "hover", "dragAt" => "drag", _ => Prose.GetValueOrDefault(action.Kind, action.Kind) });

        // Upstream's summary: the files, then the node they went to.
        if (action.Paths is { Count: > 0 } paths)
        {
            builder.Append(' ').Append(string.Join(", ", paths.Select(path => "\"" + path + "\""))).Append(" to");
        }

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

        // Upstream's summary of a drag: the node it was dropped on, in the container it sat in.
        if (action.Destination is { } destination)
        {
            builder.Append(" to ").Append(destination.Role ?? "node");
            if (destination.Name is not null)
            {
                builder.Append(" \"").Append(destination.Name).Append('"');
            }
            else if (destination.TestId is not null)
            {
                builder.Append(" testId=").Append(destination.TestId);
            }

            if (destination.Within is not null)
            {
                builder.Append(" in \"").Append(destination.Within).Append('"');
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

        // Upstream's summary of a point action: `tap the point (x, y) on <the node it sat in>`.
        if (action.Point is { } point)
        {
            builder.Append(" the point ").Append(Point(point)).Append(PointSuffix(action.Under));
        }

        if (action.From is { } from && action.To is { } to)
        {
            builder.Append(" from the point ").Append(Point(from.Point)).Append(PointSuffix(from.Under)).Append(" to the point ").Append(Point(to.Point)).Append(PointSuffix(to.Under));
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
