// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Decision;

/// <summary>One action the loop dispatches, with everything its operation needs (upstream's <c>Action</c>).</summary>
public sealed class DecisionAction
{
    /// <summary>The operation, for an action on a target or a point.</summary>
    public Operation? Operation { get; init; }

    /// <summary>The control, for a viewport, history, or keyboard action.</summary>
    public Control? Control { get; init; }

    public Target? Target { get; init; }

    public string? Text { get; init; }

    public string? Name { get; init; }

    public IReadOnlyList<string>? Paths { get; init; }

    public string? DestinationId { get; init; }

    public Point? Point { get; init; }
}

/// <summary>Upstream's <c>dispatch.ts</c>.</summary>
public static class Dispatch
{
    /// <summary>
    /// Upstream's <c>perform</c>. Performs one action through the runner's grammar: the only place that touches <c>ctx.actions</c>. Resolves
    /// to a note for the model when the engine reports something worth telling it, nothing otherwise.
    /// </summary>
    public static async Task<string?> PerformAsync(
        StepExecutorContext ctx,
        DecisionAction action,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(action);
        var actions = ctx.Actions;
        var signal = cancellationToken.CanBeCanceled ? cancellationToken : ctx.Attempt.CancellationToken;
        if (action.Control is { } control)
        {
            switch (control)
            {
                case Control.ScrollUp:
                    await actions.ScrollAsync(null, ScrollDirection.Up, 1, signal).ConfigureAwait(false);
                    break;
                case Control.ScrollDown:
                    await actions.ScrollAsync(null, ScrollDirection.Down, 1, signal).ConfigureAwait(false);
                    break;
                case Control.Back:
                    await actions.BackAsync(signal).ConfigureAwait(false);
                    break;
                case Control.DismissKeyboard:
                    await actions.DismissKeyboardAsync(signal).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentException("The action names an unknown control.", nameof(action));
            }

            return null;
        }

        var target = action.Target;
        switch (action.Operation)
        {
            case Operation.Tap:
                await actions.TapAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.DoubleTap:
                await actions.DoubleTapAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.LongPress:
                await actions.LongPressAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.Hover:
                await actions.HoverAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.SecondaryTap:
                await actions.SecondaryTapAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.ScrollTo:
                await actions.ScrollToAsync(Node(target), signal).ConfigureAwait(false);
                return null;
            case Operation.Submit:
                await actions.PressAsync(Node(target), "Enter", signal).ConfigureAwait(false);
                return null;
            case Operation.Check:
                await actions.CheckAsync(Node(target), !(target!.Checked ?? false), signal).ConfigureAwait(false);
                return null;
            case Operation.Select:
                await actions.SelectAsync(Node(target), target!.OptionLabel ?? "", signal).ConfigureAwait(false);
                return null;
            case Operation.Type:
                await actions.TypeAsync(Node(target), action.Text!, signal).ConfigureAwait(false);
                return null;
            case Operation.TypeSecret:
                await actions.TypeSecretAsync(Node(target), action.Name!, signal).ConfigureAwait(false);
                return null;
            case Operation.Upload:
                await actions.UploadAsync(Node(target), action.Paths!, signal).ConfigureAwait(false);
                return null;
            case Operation.Drag:
                await actions
                    .DragAsync(Node(target), new ExecutorTarget(action.DestinationId!), signal)
                    .ConfigureAwait(false);
                return null;
            case Operation.TapAt:
            {
                // The engine's prose for a bare point ("no listed control is there") reads as a miss to a classifier; whether the
                // page changed says more.
                var result = await actions.TapAtAsync(action.Point!.Value, signal).ConfigureAwait(false);
                return result.Target is null ? null : "landed on listed control " + result.Target.Id;
            }

            case Operation.TypeAt:
                // The tap gives the drawn field focus; the keyboard then types into whatever took it. No replace: a device cannot clear
                // a focused field without a node, so text goes in at the caret everywhere.
                await actions.TapAtAsync(action.Point!.Value, signal).ConfigureAwait(false);
                await actions.TypeTextAsync(action.Text!, null, signal).ConfigureAwait(false);
                return null;
            default:
                throw new ArgumentException("The action names no operation or control.", nameof(action));
        }
    }

    private static ExecutorTarget Node(Target? target) =>
        new((target ?? throw new ArgumentException("The action has no target.")).Id);
}
