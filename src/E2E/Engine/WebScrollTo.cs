// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// <see cref="LocatorAction.ScrollTo"/> on the web: scrolls a container to one of its edges with the page's own scroll API. Upstream's
/// <c>scroll-to.ts</c> (<c>scrollToEdge</c>).
/// </summary>
internal static class WebScrollTo
{
    /// <summary>What the page function throws for a start node no longer in the document.</summary>
    private const string Detached = "e2e: scrollTo start node is not in the document";

    /// <summary>
    /// Below this much budget no jump starts: a jump is a round trip and a rendered frame, and one cut short would only report the budget late.
    /// </summary>
    private static readonly TimeSpan MinJump = TimeSpan.FromMilliseconds(50);

    /// <summary>How long past the budget a locator's own Playwright timeout may answer a find, so a node that is gone reads as gone, not as the budget.</summary>
    private static readonly TimeSpan FindGrace = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Scrolls the container of <paramref name="target"/> (the node, or its nearest ancestor that scrolls along the edge's axis), or with no
    /// target the page, else (an app shell whose page does not scroll) the container under the viewport's center, to <paramref name="edge"/>.
    /// The container is found once and held by a handle, so a located row that unmounts while the list grows does not lose it. Each jump sets
    /// the offset past the end, which the browser clamps, and waits for a rendered frame. The edge is reached on the first jump that moves
    /// nothing, so content that loads at the end after that frame (an IntersectionObserver on a sentinel row) gets another jump. The loop runs
    /// here, one evaluate per jump, so a cancelled step leaves nothing running in the page. Upstream's <c>scrollToEdge</c>.
    /// </summary>
    internal static async Task ScrollToEdgeAsync(
        IPage page,
        ActionTarget? target,
        ScrollEdge edge,
        OperationBudget operation
    )
    {
        var name = LocatorEngine.EdgeName(edge);
        var jumps = 0;
        double? firstExtent = null;
        var grew = false;

        // Three causes, three reasons: no jump fit the budget, the end kept moving, or the budget ended before a jump confirmed it.
        EngineException OutOfBudget()
        {
            var done = $"{jumps} {(jumps == 1 ? "jump" : "jumps")}";
            var reason =
                jumps == 0 ? $"the budget ran out before the first jump toward the {name} edge"
                : grew ? $"more content kept loading at the {name} edge after {done}, until the budget ran out"
                : $"the budget ran out after {done}, before a jump confirmed the {name} edge";
            return new EngineException(EngineErrorCodes.OperationTimeout, reason, retryable: false);
        }

        EngineException Stale(string what) =>
            new(EngineErrorCodes.NodeStale, $"{what} left the page during scrollTo; re-resolve", retryable: true);

        // Bounds a page call by what is left of the budget: a call the budget abandons is the budget running out. A call with a Playwright
        // timeout of its own gets a grace, so that timeout, which says the node was not found, answers before this bound does.
        async Task<T> Bounded<T>(Task<T> call, TimeSpan grace = default)
        {
            _ = call.ContinueWith(
                static abandoned => _ = abandoned.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            try
            {
                return await call.WaitAsync(
                        (operation.Left > TimeSpan.Zero ? operation.Left : TimeSpan.FromMilliseconds(1)) + grace
                    )
                    .ConfigureAwait(false);
            }
            catch (TimeoutException) when (!call.IsFaulted)
            {
                throw OutOfBudget();
            }
        }

        var args = new
        {
            edge = name,
            fromCenter = target is null,
            detached = Detached,
        };
        var timeout = (float)Math.Max(1, operation.Left.TotalMilliseconds);
        // An action waits for its node as every other one does: a node with no box yet (still arriving, or display: none) is waited on, not taken as at its edge.
        switch (target)
        {
            case LocatorTarget { Locator: var locator }:
                await locator
                    .WaitForAsync(
                        new Microsoft.Playwright.LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = timeout,
                        }
                    )
                    .ConfigureAwait(false);
                break;
            case ElementTarget { Element: var node }:
                await node.WaitForElementStateAsync(
                        ElementState.Visible,
                        new ElementHandleWaitForElementStateOptions { Timeout = timeout }
                    )
                    .ConfigureAwait(false);
                break;
        }

        IJSHandle? container = null;
        var found = false;
        try
        {
            while (true)
            {
                if (container is null)
                {
                    operation.ThrowIfCancellationRequested();
                    // After a container left the page, a find with too little budget left could not be followed by a jump.
                    if (found && operation.Left < MinJump)
                    {
                        throw OutOfBudget();
                    }

                    try
                    {
                        container = await Bounded(
                                FindContainerAsync(page, target, args, operation),
                                target is ElementTarget or null ? TimeSpan.Zero : FindGrace
                            )
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex.Message.Contains(Detached, StringComparison.Ordinal))
                    {
                        throw Stale("the node");
                    }
                    catch (TimeoutException) when (found && target is not null)
                    {
                        // A locator that cannot be found again once a container was, means the node it came from is gone.
                        throw Stale("the scroll container");
                    }
                    catch (TimeoutException)
                    {
                        throw OutOfBudget();
                    }

                    found = true;
                }

                // A handle that holds null is no element: nothing scrolls.
                if (container is not IElementHandle handle)
                {
                    return;
                }

                // Checked right before each jump, so a step cancelled or out of budget while the page was busy moves nothing.
                operation.ThrowIfCancellationRequested();
                if (operation.Left < MinJump)
                {
                    throw OutOfBudget();
                }

                var result = await Bounded(handle.EvaluateAsync<JumpResult>(PageScript.ScrollJump, name))
                    .ConfigureAwait(false);
                if (result.State == "settled")
                {
                    return;
                }

                if (result.State == "detached")
                {
                    await container.DisposeAsync().ConfigureAwait(false);
                    container = null;
                }
                else
                {
                    jumps++;
                    firstExtent ??= result.From;
                    if (result.Extent > firstExtent)
                    {
                        grew = true;
                    }
                }
            }
        }
        finally
        {
            if (container is not null)
            {
                try
                {
                    await container.DisposeAsync().ConfigureAwait(false);
                }
                catch (PlaywrightException) { }
            }
        }
    }

    /// <summary>The container a scroll toward the edge moves, as a handle that holds <c>null</c> when nothing scrolls.</summary>
    private static Task<IJSHandle> FindContainerAsync(
        IPage page,
        ActionTarget? target,
        object args,
        OperationBudget operation
    )
    {
        var options = new LocatorEvaluateHandleOptions
        {
            Timeout = (float)Math.Max(1, operation.Left.TotalMilliseconds),
        };
        return target switch
        {
            null => page.Locator(":root").EvaluateHandleAsync(PageScript.ScrollContainer, args, options),
            LocatorTarget { Locator: var locator } => locator.EvaluateHandleAsync(
                PageScript.ScrollContainer,
                args,
                options
            ),
            ElementTarget { Element: var element } => element.EvaluateHandleAsync(PageScript.ScrollContainer, args),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "unknown action target"),
        };
    }

    /// <summary>
    /// One jump: <c>settled</c> when it moved nothing and the container still held that end, with the same extent, after the next rendered
    /// frame; <c>moved</c> when it moved or the end did; <c>detached</c> when the container left the document. <see cref="From"/> is the
    /// scrollable extent before the jump, <see cref="Extent"/> the one after.
    /// </summary>
    private sealed class JumpResult
    {
        public string State { get; set; } = "moved";

        public double From { get; set; }

        public double Extent { get; set; }
    }
}
