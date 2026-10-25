// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ScrollUntilVisible;

/// <summary>One swipe a fake engine took: where it was addressed, which way, and how far.</summary>
internal sealed record Swipe(string Ref, ScrollDirection Direction, Momentum? Momentum);

/// <summary>What a screen shows before any swipe, then after each; the last entry stays.</summary>
internal sealed class ScrollScript
{
    public required IReadOnlyList<IReadOnlyList<SemanticNode>> Screens { get; init; }

    /// <summary>Whether the engine takes a swipe; default true.</summary>
    public bool Swipeable { get; init; } = true;

    /// <summary>Every swipe hangs until the scroll's deadline ends it.</summary>
    public bool Hang { get; init; }

    /// <summary>Every swipe hangs this long on the engine's own clock, then fails as an engine reporting a timeout does.</summary>
    public TimeSpan? HangFor { get; init; }

    /// <summary>The action timeout of the screen; default 1 second.</summary>
    public TimeSpan ActionTimeout { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// A screen over a fake engine that pages through <see cref="ScrollScript.Screens"/> one swipe at a time and logs every swipe, as
/// upstream's <c>scrollScreen</c> builds one. The viewport swipe is addressed to <c>viewport</c>: the port has no observation-root
/// node to address it to.
/// </summary>
internal sealed class ScrollScreen
{
    public static readonly SemanticNode Target = new() { Ref = "target", Role = "button", Name = "Accept", Text = "Accept" };

    public static readonly SemanticNode HiddenTarget = new() { Ref = "target", Role = "button", Name = "Accept", Text = "Accept", States = new NodeStates { Hidden = true } };

    /// <summary>The scroll container every screen shows, with whatever it has scrolled to as its children.</summary>
    public static SemanticNode Feed(params SemanticNode[] children) => new() { Ref = "feed", Role = "list", Name = "Feed", Children = children };

    private static readonly TimeSpan SwipeTime = TimeSpan.FromMilliseconds(16);

    private readonly List<Swipe> _swipes = [];

    public ScrollScreen(ScrollScript script)
    {
        Steps = new StepRecorder("attempt");
        IReadOnlyList<SemanticNode> Current()
        {
            lock (_swipes)
            {
                return script.Screens[Math.Min(_swipes.Count, script.Screens.Count - 1)];
            }
        }

        async Task Swipe(string reference, ScrollDirection direction, Momentum? momentum, CancellationToken token)
        {
            if (script.Hang)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            if (script.HangFor is { } hang)
            {
                await Task.Delay(hang, CancellationToken.None);
                throw new EngineException(EngineErrorCodes.OperationTimeout, "swipe exceeded " + hang.TotalMilliseconds + " ms", retryable: false);
            }

            // A swipe resolves once the scroll it caused is on screen, a frame or more later.
            await Task.Delay(SwipeTime, CancellationToken.None);
            lock (_swipes)
            {
                _swipes.Add(new Swipe(reference, direction, momentum));
            }
        }

        Screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = Current() }),
            async (node, action, token) =>
            {
                if (action is not LocatorAction.Swipe swipe)
                {
                    throw new InvalidOperationException("unexpected " + action);
                }

                await Swipe(node.Ref, swipe.Direction, swipe.Momentum, token);
            },
            () => CancellationToken.None,
            () => { },
            script.ActionTimeout,
            script.ActionTimeout,
            swipe: script.Swipeable
                ? (direction, momentum, token) => Swipe("viewport", direction, momentum, token)
                : null)
        {
            Steps = Steps,
        };
    }

    public Screen Screen { get; }

    public StepRecorder Steps { get; }

    public IReadOnlyList<Swipe> Swipes
    {
        get
        {
            lock (_swipes)
            {
                return [.. _swipes];
            }
        }
    }
}
