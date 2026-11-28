// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// What a frame the engine reports missing means to a resolve (upstream's <c>MissingFrame</c>).
/// </summary>
internal enum MissingFrame
{
    /// <summary>Treated like a node that is not there yet: the resolve keeps polling for it.</summary>
    Wait,

    /// <summary>Answers zero matches.</summary>
    Empty,
}

/// <summary>
/// What a <see cref="LocatorEngine"/> runs over (upstream's <c>LocatorEngineOptions</c>). Upstream hands the engine a <c>TargetSession</c>;
/// the port hands it the session's calls (.NET-only): the attempt reads the screen through its scope, and a test builds an engine from
/// bare functions.
/// </summary>
internal sealed class LocatorEngineOptions
{
    /// <summary>One observation of the screen.</summary>
    public required Func<CancellationToken, Task<Observation>> Observe { get; init; }

    /// <summary>One observation within a budget the engine bounds its reads by (upstream's <c>operation.timeoutMs</c>); null answers with <see cref="Observe"/>.</summary>
    public Func<TimeSpan, CancellationToken, Task<Observation>>? ObserveWithin { get; init; }

    /// <summary>The engine's own resolve (upstream's <c>session.locate</c>); null resolves over <see cref="Observe"/>.</summary>
    public Func<
        LocatorExpression,
        TimeSpan,
        CancellationToken,
        Task<IReadOnlyList<SemanticNode>>
    >? Locate { get; init; }

    /// <summary>
    /// Reads one located node from the resolution that minted it (upstream's <c>session.read</c>), failing with <c>NODE_STALE</c> when the
    /// node was superseded since. Null when the engine's locate answers the nodes themselves, so there is nothing to go stale.
    /// </summary>
    public Func<SemanticNode, OperationContext, CancellationToken, Task<SemanticNode>>? Read { get; init; }

    /// <summary>
    /// How much of a node is inside the viewport (upstream's <c>session.viewportIntersection</c>); null when the engine cannot measure
    /// (upstream's <c>session.measuresViewport</c> false).
    /// </summary>
    public Func<
        SemanticNode,
        OperationContext,
        CancellationToken,
        Task<ViewportIntersection>
    >? ViewportIntersection { get; init; }

    /// <summary>One action on a node (upstream's <c>session.perform</c>), with the context that says who acts.</summary>
    public required Func<SemanticNode, LocatorAction, OperationContext, CancellationToken, Task> Perform { get; init; }

    /// <summary>
    /// <see cref="LocatorAction.ScrollTo"/> on the screen: upstream's <c>session.rootRef</c> followed by <c>session.perform(root, scrollTo)</c>, which
    /// the port's engines take as one call (as <see cref="IEngineSession.SwipeAsync(ScrollDirection, Momentum?, OperationContext, CancellationToken)"/>
    /// does for the viewport swipe). Null when the engine has no such action.
    /// </summary>
    public Func<ScrollEdge, OperationContext, CancellationToken, Task>? ScrollTo { get; init; }

    /// <summary>One pointer action at a viewport point (upstream's <c>session.performAt</c>); null when the engine has none.</summary>
    public Func<Point, PointerAction, OperationContext, CancellationToken, Task>? PerformAt { get; init; }

    /// <summary>
    /// Types text into whatever holds focus, appending unless the flag asks to replace (upstream's <c>session.keyboard.type</c>); null when the
    /// engine has no keyboard.
    /// </summary>
    public Func<string, bool, CancellationToken, Task>? TypeText { get; init; }

    /// <summary>The running phase's token (upstream's <c>budget.signal</c>), read per operation.</summary>
    public required Func<CancellationToken> Cancellation { get; init; }

    /// <summary>How long an action waits for its node.</summary>
    public required TimeSpan ActionTimeout { get; init; }

    /// <summary>How long an assertion waits.</summary>
    public required TimeSpan AssertionTimeout { get; init; }

    /// <summary>
    /// How long an app open, restart, or link open may take, once a program has set a default timeout; null in a run, where the test's timeout
    /// bounds them (upstream's <c>lifecycleTimeout</c>).
    /// </summary>
    public TimeSpan? LifecycleTimeout { get; init; }

    /// <summary>The clock every deadline reads.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;
}

/// <summary>Per-attempt locator execution engine: runner-owned query polling, strictness, and action retry (upstream's <c>LocatorEngine</c>, <c>locator/engine.ts</c>).</summary>
internal sealed class LocatorEngine(LocatorEngineOptions options)
{
    /// <summary>How long an action waits for its node.</summary>
    internal TimeSpan ActionTimeout { get; private set; } = options.ActionTimeout;

    /// <summary>How long an assertion waits (upstream's <c>assertionTimeout</c>).</summary>
    internal TimeSpan AssertionTimeout { get; private set; } = options.AssertionTimeout;

    /// <summary>The default timeout of an app open, restart, or link open, when a program set one (upstream's <c>lifecycleTimeout</c>).</summary>
    internal TimeSpan? LifecycleTimeout { get; private set; } = options.LifecycleTimeout;

    /// <summary>
    /// Replaces the default timeouts of the operations that start from now on, for an <c>@e2e-dev/core</c> session's <c>setDefaultTimeout</c>. A
    /// running operation keeps the deadline it started with (upstream's <c>setDefaultTimeouts</c>).
    /// </summary>
    internal void SetDefaultTimeouts(TimeSpan action, TimeSpan assertion, TimeSpan lifecycle)
    {
        ActionTimeout = action;
        AssertionTimeout = assertion;
        LifecycleTimeout = lifecycle;
    }

    internal TimeProvider Clock { get; } = options.Clock;

    internal TimeSpan PollInterval { get; } = Time.PollInterval;

    /// <summary>
    /// The budget the whole diagnostic failure walk shares: best effort, never a wait (upstream's <c>STEP_COUNT_BUDGET_MS</c>). One deadline
    /// bounds every prefix resolve, so a multi-step chain cannot cost more than one short budget; the real bound is the budget plus one read.
    /// </summary>
    private static readonly TimeSpan StepCountBudget = TimeSpan.FromMilliseconds(500);

    /// <summary>The end of the attempt's test timeout, which no read may outlast (upstream's <c>budget.deadline</c>); null outside a session or with no test timeout.</summary>
    internal Deadline? BudgetDeadline { get; set; }

    /// <summary>What the attempt's engine declares; none outside a session. Upstream's <c>session.tapModifiers</c> is <see cref="EngineCapabilities.TapModifiers"/>.</summary>
    internal EngineCapabilities Capabilities { get; set; }

    /// <summary>
    /// The locator action kinds the engine declared (upstream's <c>session.actions</c>). Null declares every kind, as an engine built over
    /// bare functions does; a session sets it from <see cref="IEngineSession.ActionKinds"/>.
    /// </summary>
    internal IReadOnlyCollection<string>? ActionKinds { get; set; }

    /// <summary>The pointer action kinds the engine declared (upstream's <c>session.pointerActions</c>). Null declares none.</summary>
    internal IReadOnlyCollection<string>? PointerActionKinds { get; set; }

    /// <summary>Records what an action did under the running step (upstream's <c>recordEvent</c>); null where no step records.</summary>
    internal StepRecorder? Steps { get; set; }

    /// <summary>Replaces secret values in text an event quotes from the screen, before it is clipped (upstream's <c>redact</c>); identity outside a session.</summary>
    internal Func<string, string> Redact { get; set; } = static text => text;

    /// <summary>
    /// Whether the engine resolves a locator itself. Upstream's engines all do; a port engine that does not has its locator resolved
    /// over <see cref="ObserveAsync(CancellationToken)"/> (.NET-only), until it implements <see cref="IEngineSession.LocateAsync(LocatorExpression, TimeSpan, CancellationToken)"/>.
    /// </summary>
    internal bool LocatesInEngine => options.Locate is not null;

    /// <summary>Canonical visibility predicate over a semantic node snapshot (upstream's <c>isNodeVisible</c>).</summary>
    internal static bool IsNodeVisible(SemanticNode? node) => node is { States: { Hidden: false, EmptyBox: false } };

    /// <summary>
    /// Denies reading a value, text, or attribute when any node is a secure field: the engine withholds them there, so what reads as
    /// missing is redacted, not empty or absent (upstream's <c>denySecureRead</c>).
    /// </summary>
    internal static void DenySecureRead(IReadOnlyList<SemanticNode> nodes, string label)
    {
        if (nodes.Any(node => node.States.Secure))
        {
            throw new TestException("POLICY_DENIED", "reading values from a secure field is denied: " + label);
        }
    }

    /// <summary>
    /// The facts of a locator failure an error keeps beside the message (upstream's <c>locatorDetails</c>): the locator as written,
    /// what it asked for, and how long it waited, when it waited.
    /// </summary>
    internal static ErrorDetails LocatorDetails(LocatorExpression expression, TimeSpan? waited = null) =>
        expression.ExpressionHints() with
        {
            Locator = expression.Describe(),
            WaitedMs = waited is { } elapsed ? (long)elapsed.TotalMilliseconds : null,
        };

    /// <summary>
    /// Translates an engine error into the runner-owned public taxonomy (upstream's <c>translateLocatorError</c>), naming
    /// <paramref name="expression"/> in the message when one is given. A missing or ambiguous frame also carries the locator's details.
    /// </summary>
    internal static Exception TranslateLocatorError(Exception cause, LocatorExpression? expression = null)
    {
        var translated = ErrorSerializer.TranslateEngineError(
            cause,
            expression is null ? "" : ": " + expression.Describe()
        );
        return
            expression is not null
            && translated is TestException { Code: "LOCATOR_NOT_FOUND" or "LOCATOR_AMBIGUOUS" } frame
            && cause is EngineException { Code: EngineErrorCodes.FrameNotFound or EngineErrorCodes.FrameAmbiguous }
            ? new TestException(frame.Code, frame.Message, frame.InnerException!)
            {
                Details = LocatorDetails(expression),
            }
            : translated;
    }

    /// <summary>The method a test called, as upstream names the action: tap, doubleTap, fill, selectOption.</summary>
    internal static string ApiName(LocatorAction action) =>
        action switch
        {
            LocatorAction.Tap => "tap",
            LocatorAction.DoubleTap => "doubleTap",
            LocatorAction.MultiTap => "multiTap",
            LocatorAction.Fill => "fill",
            LocatorAction.Press => "press",
            LocatorAction.Select => "selectOption",
            LocatorAction.SetInputFiles or LocatorAction.SetInputFilePayloads => "setInputFiles",
            LocatorAction.Check => "check",
            LocatorAction.Uncheck => "uncheck",
            LocatorAction.Clear => "clear",
            LocatorAction.Focus => "focus",
            LocatorAction.Blur => "blur",
            LocatorAction.ScrollIntoView => "scrollIntoView",
            LocatorAction.Swipe => "swipe",
            LocatorAction.ScrollTo => "scrollTo",
            LocatorAction.SecondaryTap => "secondaryTap",
            LocatorAction.LongPress => "longPress",
            LocatorAction.Hover => "hover",
            LocatorAction.DragTo => "dragTo",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown locator action"),
        };

    /// <summary>
    /// The failure of an action whose deadline passed before it was sent. A read may finish past its deadline so its result is classed
    /// right; an action never starts past it (upstream's <c>lateAction</c>).
    /// </summary>
    internal static TestException LateAction(string kind, string target) =>
        new("ACTION_FAILED", "operation timed out before the " + kind + " was sent: " + target);

    /// <summary>The token an operation runs under: the caller's, ending with the running phase's too (upstream's <c>signal</c>).</summary>
    internal CancellationToken Token(CancellationToken cancellationToken) =>
        Time.Link(cancellationToken, options.Cancellation());

    /// <summary>The reads of one wait on <paramref name="deadline"/>: the last may run past it, up to the test timeout (upstream's <c>reads</c>).</summary>
    internal ReadWindow Reads(Deadline deadline) =>
        new(deadline, Clock, BudgetDeadline ?? new Deadline(DateTimeOffset.MaxValue));

    /// <summary>
    /// The timeout of one action-family operation: <paramref name="timeout"/> when given, the action timeout otherwise. Upstream's
    /// <c>deadline(timeoutMs)</c> builds the deadline itself; <see cref="Deadline"/> is also this type's name here (.NET-only name). A
    /// negative <paramref name="timeout"/> is <c>INVALID_ARGUMENT</c>. Zero sets no deadline of its own: the budget is what is left of the
    /// running phase's deadline.
    /// </summary>
    internal TimeSpan TimeoutOf(TimeSpan? timeout) =>
        Time.StepBudget(timeout, "the step", ActionTimeout, BudgetDeadline, Clock);

    /// <summary>
    /// Deadline for one action-family operation (upstream's <c>deadline</c>; .NET-only name, as <c>Deadline</c> is the type). Zero sets no
    /// deadline of its own, so the running phase's alone bounds it.
    /// </summary>
    internal Deadline DeadlineFor(TimeSpan? timeout = null) =>
        Time.StepDeadline(timeout, "the step", ActionTimeout, BudgetDeadline, Clock);

    /// <summary>Deadline for one assertion on a matcher's own <paramref name="timeout"/>, the assertion timeout by default; <paramref name="api"/> names the call in a refusal.</summary>
    internal Deadline AssertionDeadlineFor(TimeSpan? timeout, string api) =>
        Time.StepDeadline(timeout, api, AssertionTimeout, BudgetDeadline, Clock);

    /// <summary>One observation of the screen, an unopened app reported as <c>APP_NOT_OPEN</c>.</summary>
    internal async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        return await AppNotOpen
            .GuardAsync(() =>
                options.Observe(cancellationToken == default ? options.Cancellation() : cancellationToken)
            )
            .ConfigureAwait(false);
    }

    /// <summary><see cref="ObserveAsync(CancellationToken)"/> within <paramref name="timeout"/>, the budget the engine bounds its reads by (upstream's <c>operation.timeoutMs</c>).</summary>
    internal async Task<Observation> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var token = cancellationToken == default ? options.Cancellation() : cancellationToken;
        return await AppNotOpen
            .GuardAsync(() => options.ObserveWithin is { } within ? within(timeout, token) : options.Observe(token))
            .ConfigureAwait(false);
    }

    /// <summary>One immediate engine resolve: the nodes the query matches now, within <paramref name="timeout"/> of the engine's own work. Upstream's <c>session.locate</c>.</summary>
    private async Task<IReadOnlyList<SemanticNode>> SessionLocateAsync(
        LocatorExpression expression,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        return await AppNotOpen
            .GuardAsync(() =>
                options.Locate!(
                    expression,
                    timeout,
                    cancellationToken == default ? options.Cancellation() : cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The context of an engine call bounded by <paramref name="deadline"/>, marked with the test origin: the engine may act without settling.
    /// Upstream's <c>operationWithin</c>: the time left, never under 1 ms.
    /// </summary>
    internal OperationContext OperationWithin(Deadline deadline) =>
        OperationContext.Test with
        {
            Timeout = Time.Between(deadline.Remaining(Clock.GetUtcNow()), TimeSpan.FromMilliseconds(1), Time.Unbounded),
        };

    /// <summary>Carries out one action on a node within <paramref name="deadline"/> (upstream's <c>session.perform(ref, action, this.operationWithin(deadline))</c>).</summary>
    internal async Task SessionPerformAsync(
        SemanticNode node,
        LocatorAction action,
        Deadline deadline,
        CancellationToken cancellationToken
    )
    {
        // The session adapter's own guard (upstream's `session.ts`): an action built per attempt, such as a drag, is checked when it is sent.
        CheckAction(action);
        await AppNotOpen
            .GuardAsync(() =>
                options.Perform(
                    node,
                    action,
                    OperationWithin(deadline),
                    cancellationToken == default ? options.Cancellation() : cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    /// <summary>Carries out one pointer action at a viewport point, with no node behind it. Upstream's <c>session.performAt</c>.</summary>
    internal async Task SessionPerformAtAsync(
        Point point,
        PointerAction action,
        Deadline deadline,
        CancellationToken cancellationToken
    )
    {
        CheckPointerAction(action);
        var perform = options.PerformAt
            ?? throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                "the pointer actions are not supported by this engine."
            );
        await AppNotOpen
            .GuardAsync(() =>
                perform(
                    point,
                    action,
                    OperationWithin(deadline),
                    cancellationToken == default ? options.Cancellation() : cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Refuses an action the engine did not declare, before the node is resolved: <c>UNSUPPORTED_CAPABILITY</c>. Upstream's
    /// <c>checkAction</c>; its modifiers check is the tap family's own (<c>ActionDispatcher.RequireClickModifiers</c>).
    /// </summary>
    internal void CheckAction(LocatorAction action)
    {
        var kind = EngineActionKinds.Of(action);
        if (ActionKinds is { } declared && !declared.Contains(kind))
        {
            if (action is LocatorAction.SetInputFilePayloads)
            {
                throw new EngineException(
                    EngineErrorCodes.UnsupportedCapability,
                    "in-memory files for setInputFiles are not available on this target; update its engine"
                        + (declared.Contains("setInputFiles") ? " or pass a file path" : string.Empty)
                );
            }

            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                $"the \"{kind}\" action is not available on this target: its engine declares {(declared.Count == 0 ? "no actions" : string.Join(", ", declared))}"
            );
        }

        if (action is LocatorAction.Press press)
        {
            Keys.RequireKey(press.Key);
        }
    }

    /// <summary>Reports whether the target engine declares this locator action kind (upstream's <c>supports</c>). An engine that declares none is taken to do them all.</summary>
    internal bool Supports(string kind) => ActionKinds is not { } declared || declared.Contains(kind);

    /// <summary>Refuses a pointer action the engine did not declare: <c>UNSUPPORTED_CAPABILITY</c>. Upstream's <c>checkPointerAction</c>.</summary>
    internal void CheckPointerAction(PointerAction action)
    {
        var kind = EngineActionKinds.Of(action);
        var declared = PointerActionKinds ?? [];
        if (!declared.Contains(kind))
        {
            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                $"the \"{kind}\" action at a point is not available on this target: its engine declares {(declared.Count == 0 ? "no pointer actions" : string.Join(", ", declared))}"
            );
        }
    }

    /// <summary>
    /// One immediate resolve, retrying a retryable engine error within the deadline (upstream's <c>resolveOnce</c>). The engine answers it
    /// when it resolves locators, and a role query of its skips a hidden node unless <paramref name="includeHidden"/> asks for it; any
    /// other engine is resolved over its observation. <paramref name="reads"/> bounds the retries and the budget of each read, and defaults
    /// to a window of the action timeout. With <see cref="MissingFrame.Empty"/> a retryable <c>FRAME_NOT_FOUND</c> is zero matches instead:
    /// nothing under a frame that is not in the document matches, and a read that promised not to wait must not wait for it.
    /// </summary>
    internal async Task<IReadOnlyList<SemanticNode>> ResolveOnceAsync(
        LocatorExpression expression,
        CancellationToken token,
        bool includeHidden = false,
        ReadWindow? reads = null,
        MissingFrame missingFrame = MissingFrame.Wait
    )
    {
        reads ??= Reads(new Deadline(ActionTimeout, Clock));
        if (!LocatesInEngine)
        {
            Observation observation;
            try
            {
                observation = await reads.ReadAsync(budget => ObserveAsync(budget, token)).ConfigureAwait(false);
            }
            catch (EngineException ex) when (!token.IsCancellationRequested)
            {
                var translated = TranslateLocatorError(ex, expression);
                if (ReferenceEquals(translated, ex))
                {
                    throw;
                }

                throw translated;
            }

            return LocatorResolver.Resolve(observation, expression, includeHidden);
        }

        var query = includeHidden ? expression with { IncludeHidden = true } : expression;
        EngineException? retried = null;
        var pollStarted = Clock.GetTimestamp();
        while (true)
        {
            try
            {
                var nodes = await reads
                    .ReadAsync(budget => SessionLocateAsync(query, budget, token))
                    .ConfigureAwait(false);

                // An engine answers `visible` inside scopes, filters, and indices, where only it can. A direct query is held to the same
                // predicate here, so it never resolves to a node its own state calls hidden.
                return expression.Visible && expression.Kind is not ("filter" or "index" or "selector")
                    ? nodes.Where(node => IsNodeVisible(node)).ToList()
                    : nodes;
            }
            catch (EngineException ex) when (retried is not null && !token.IsCancellationRequested && reads.CutOff(ex))
            {
                // A retry the deadline cut off saw nothing new: the wait ends on the failure it was retrying.
                throw TranslateLocatorError(retried, expression);
            }
            catch (EngineException ex)
                when (missingFrame == MissingFrame.Empty
                    && ex is { Code: EngineErrorCodes.FrameNotFound, Retryable: true }
                )
            {
                return [];
            }
            catch (EngineException ex)
                when (ex.Retryable && !reads.Deadline.Expired(Clock.GetUtcNow()) && !token.IsCancellationRequested)
            {
                retried = ex;
                var now = Clock.GetUtcNow();
                var pause = Time.PollDelay(Clock.GetElapsedTime(pollStarted));
                var left = reads.Deadline.Remaining(now);
                await Task.Delay(left < pause ? left : pause, Clock, token).ConfigureAwait(false);
            }
            catch (EngineException ex) when (!token.IsCancellationRequested)
            {
                var translated = TranslateLocatorError(ex, expression);
                if (ReferenceEquals(translated, ex))
                {
                    throw;
                }

                throw translated;
            }
        }
    }

    /// <summary>
    /// Resolves every current match at once, without waiting for one. A frame the engine reports missing is zero matches, since nothing
    /// under it is in the document. A stale resolve is still retried within the deadline, as every resolve does.
    /// </summary>
    internal Task<IReadOnlyList<SemanticNode>> ResolveNowAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow? reads = null
    ) => ResolveOnceAsync(expression, token, reads: reads, missingFrame: MissingFrame.Empty);

    /// <summary>
    /// Reads every current match at once, in document order: a missing frame reads as no matches. A locate answers the nodes themselves,
    /// so there is no separate read to go stale between a resolve and a read (upstream's <c>session.read</c>).
    /// </summary>
    internal Task<IReadOnlyList<SemanticNode>> ReadAllNowAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow? reads = null
    ) => ResolveNowAsync(expression, token, reads);

    /// <summary>One sample of <see cref="ReadViewportAsync"/>: the one match, how many there were, and what was measured of it.</summary>
    internal sealed record ViewportRead(SemanticNode? Node, int Count, bool Stale, ViewportIntersection? Intersection);

    /// <summary>The context of an engine read on <paramref name="budget"/>, marked with the test origin (upstream's <c>readOperation</c>).</summary>
    internal static OperationContext ReadOperation(TimeSpan budget) =>
        OperationContext.Test with
        {
            Timeout = Time.Between(budget, TimeSpan.FromMilliseconds(1), Time.Unbounded),
        };

    /// <summary>
    /// Reads the one current match and measures how much of it is inside the viewport, for <c>ToBeInViewportAsync</c>: one resolve and one
    /// measure of the node it answered, all inside <paramref name="reads"/>, never a second observation. Zero matches or a missing frame read
    /// as no node. A node that went stale before it was measured is <see cref="ViewportRead.Stale"/>, so the poll samples again instead of
    /// reading the node as gone. An engine that cannot measure is refused before the resolve, so a missing match never hides it. Upstream's
    /// <c>readViewport</c>.
    /// </summary>
    internal async Task<ViewportRead> ReadViewportAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow reads
    )
    {
        if (options.ViewportIntersection is not { } measure)
        {
            throw new ConfigurationException(
                EngineErrorCodes.UnsupportedCapability,
                "toBeInViewport is not available on this target: its engine does not implement viewportIntersection"
            );
        }

        var (node, count, stale) = await ReadOneAsync(expression, token, reads, MissingFrame.Empty)
            .ConfigureAwait(false);
        if (node is null)
        {
            return new ViewportRead(null, count, stale, null);
        }

        try
        {
            var intersection = await reads
                .ReadAsync(budget =>
                    AppNotOpen.GuardAsync(() =>
                        measure(node, ReadOperation(budget), token == default ? options.Cancellation() : token)
                    )
                )
                .ConfigureAwait(false);
            return new ViewportRead(node, 1, false, intersection);
        }
        catch (EngineException ex) when (ex.Code == EngineErrorCodes.NodeStale)
        {
            return new ViewportRead(node, 1, true, null);
        }
        catch (EngineException ex) when (!token.IsCancellationRequested)
        {
            var translated = TranslateLocatorError(ex, expression);
            if (ReferenceEquals(translated, ex))
            {
                throw;
            }

            throw translated;
        }
    }

    /// <summary>
    /// Reads the one current match at once, for <c>isVisible</c> and <c>isHidden</c>: null for zero matches, a missing frame, or a node that
    /// went stale; two matches are <c>LOCATOR_AMBIGUOUS</c>.
    /// </summary>
    internal async Task<SemanticNode?> ReadNowAsync(LocatorExpression expression, CancellationToken token)
    {
        var (node, _, _) = await ReadOneAsync(expression, token, null, MissingFrame.Empty).ConfigureAwait(false);
        return node;
    }

    /// <summary>
    /// Resolves and reads the one current match. A node superseded between the resolve and the read is one match with no node and
    /// <c>stale</c> set: a poll samples again, and <see cref="ReadNowAsync"/> reads it as gone. Upstream's <c>readOne</c>.
    /// </summary>
    private async Task<(SemanticNode? Node, int Count, bool Stale)> ReadOneAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow? reads,
        MissingFrame missingFrame
    )
    {
        var located = await TryReadAsync(expression, token, reads, missingFrame).ConfigureAwait(false);
        if (located is null)
        {
            return (null, 0, false);
        }

        if (options.Read is not { } read)
        {
            return (located, 1, false);
        }

        reads ??= Reads(new Deadline(ActionTimeout, Clock));
        try
        {
            var node = await reads
                .ReadAsync(budget =>
                    AppNotOpen.GuardAsync(() =>
                        read(located, ReadOperation(budget), token == default ? options.Cancellation() : token)
                    )
                )
                .ConfigureAwait(false);
            return (node, 1, false);
        }
        catch (EngineException ex) when (ex.Code == EngineErrorCodes.NodeStale)
        {
            return (null, 1, true);
        }
    }

    /// <summary>
    /// Reads one node for polling surfaces; null while zero matches. A poll that waits for absence passes <see cref="MissingFrame.Empty"/>:
    /// nothing under a frame that is not in the document is attached, so the sample is zero matches rather than a wait for the frame.
    /// </summary>
    internal async Task<SemanticNode?> TryReadAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow? reads = null,
        MissingFrame missingFrame = MissingFrame.Wait
    )
    {
        var matches = await ResolveOnceAsync(expression, token, reads: reads, missingFrame: missingFrame)
            .ConfigureAwait(false);
        return matches.Count > 1 ? throw await AmbiguousAsync(expression, matches.Count, token).ConfigureAwait(false)
            : matches.Count == 1 ? matches[0]
            : null;
    }

    /// <summary>
    /// Waits up to the action timeout for the one match of a read. Zero matches poll until the action timeout, as an action's resolve
    /// does, so a read of a node the app is still rendering answers once it is there. More than one match fails at once. Upstream's <c>read</c>.
    /// </summary>
    internal async Task<SemanticNode> ReadAsync(LocatorExpression expression, CancellationToken token)
    {
        var started = Clock.GetUtcNow();
        var deadline = started + ActionTimeout;
        var reads = Reads(new Deadline(deadline));
        var stamp = Stopwatch.GetTimestamp();
        var sampled = false;
        var pollStarted = Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () =>
                    Stopwatch.GetElapsedTime(stamp)
                )
                .ConfigureAwait(false);
            sampled = true;
            if (node is not null)
            {
                return node;
            }

            if (Clock.GetUtcNow() >= deadline)
            {
                throw await NotFoundAsync(expression, Clock.GetUtcNow() - started, token).ConfigureAwait(false);
            }

            await Task.Delay(Time.PollDelay(Clock.GetElapsedTime(pollStarted)), Clock, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until <paramref name="deadline"/> for the one match. Zero matches poll until the deadline; more than one match fails at once.
    /// Upstream's <c>resolveExactlyOne</c>, which a drag uses for the node it drops on.
    /// </summary>
    internal async Task<SemanticNode> ResolveExactlyOneAsync(
        LocatorExpression expression,
        Deadline deadline,
        CancellationToken token
    )
    {
        var started = Clock.GetTimestamp();
        var reads = Reads(deadline);
        var sampled = false;
        var pollStarted = Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () => Clock.GetElapsedTime(started))
                .ConfigureAwait(false);
            sampled = true;
            if (node is not null)
            {
                return node;
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw await NotFoundAsync(expression, Clock.GetElapsedTime(started), token).ConfigureAwait(false);
            }

            await Task.Delay(Time.PollDelay(Clock.GetElapsedTime(pollStarted)), Clock, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <see cref="TryReadAsync"/> for a wait: a resolve the deadline cut off, after one that matched nothing, is the wait running out
    /// (upstream's <c>resolveCounted</c>), reported as not found rather than as the timeout of the read.
    /// </summary>
    private async Task<SemanticNode?> ResolveCountedAsync(
        LocatorExpression expression,
        CancellationToken token,
        ReadWindow reads,
        bool sampled,
        Func<TimeSpan> elapsed
    )
    {
        try
        {
            return await TryReadAsync(expression, token, reads).ConfigureAwait(false);
        }
        catch (Exception ex) when (sampled && !token.IsCancellationRequested && reads.CutOff(ex))
        {
            throw await NotFoundAsync(expression, elapsed(), token, ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs exactly one action against exactly one enabled match. Stale nodes are re-resolved while the deadline remains; a possibly
    /// committed action is never repeated. An action that itself names other nodes (dragTo) is built per attempt, so its nodes are
    /// re-resolved together with the source. With <paramref name="anyState"/> a hidden or disabled match is acted on too, as for a scroll into view,
    /// which needs no visibility.
    /// </summary>
    internal async Task PerformAsync(
        LocatorExpression expression,
        Func<Deadline, CancellationToken, Task<LocatorAction>> build,
        TimeSpan? timeout,
        CancellationToken token,
        bool anyState = false
    )
    {
        var started = Clock.GetUtcNow();
        var window = DeadlineFor(timeout);
        var deadline = window.EndsAt;
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        // Each resolve counts its own rounds and wait, as upstream's resolveCounted does: a stale retry starts them over.
        var resolveStamp = stamp;
        var rounds = 0;
        var stale = 0;
        var reads = Reads(window);
        var pollStarted = Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled: rounds > 0, () =>
                    Stopwatch.GetElapsedTime(resolveStamp)
                )
                .ConfigureAwait(false);
            rounds++;
            if (node is not null && (anyState || node is { States.Disabled: false, States.Hidden: false }))
            {
                var waitedMs = rounds > 1 ? (long)Stopwatch.GetElapsedTime(resolveStamp).TotalMilliseconds : 0;
                var action = await build(window, token).ConfigureAwait(false);
                if (window.Expired(Clock.GetUtcNow()))
                {
                    var late = LateAction(KindOf(action), expression.Describe());
                    RecordAction(node, action, startedAt, stamp, waitedMs, stale, StepEventStatus.Failed, late.Code);
                    throw late;
                }

                try
                {
                    await SessionPerformAsync(node, action, window, token).ConfigureAwait(false);
                }
                catch (EngineException ex)
                    when (ex is { Code: EngineErrorCodes.NodeStale, Retryable: true }
                        && !token.IsCancellationRequested
                        && !window.Expired(Clock.GetUtcNow())
                    )
                {
                    // The node went stale under the action: look again, within the deadline.
                    stale++;
                    rounds = 0;
                    resolveStamp = Stopwatch.GetTimestamp();
                    pollStarted = Clock.GetTimestamp();
                    continue;
                }
                catch (Exception ex)
                {
                    var failure = token.IsCancellationRequested ? ex : TranslateLocatorError(ex, expression);
                    RecordAction(
                        node,
                        action,
                        startedAt,
                        stamp,
                        waitedMs,
                        stale,
                        token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed,
                        ErrorSerializer.Classify(failure).Code
                    );
                    if (ReferenceEquals(failure, ex))
                    {
                        throw;
                    }

                    throw failure;
                }

                RecordAction(node, action, startedAt, stamp, waitedMs, stale, StepEventStatus.Passed, null);
                return;
            }

            if (Clock.GetUtcNow() >= deadline)
            {
                throw node is null
                    ? await NotFoundAsync(expression, Clock.GetUtcNow() - started, token).ConfigureAwait(false)
                    : new TestException(
                        "NOT_ACTIONABLE",
                        expression.Describe() + (node.States.Hidden ? " is hidden." : " is disabled.")
                    );
            }

            await Task.Delay(Time.PollDelay(Clock.GetElapsedTime(pollStarted)), Clock, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Types <paramref name="chunks"/> into the one match, composed from the engine's focus action and its keyboard (upstream's
    /// <c>pressSequentially</c>), so no engine contract carries a typing action. The whole of it shares one deadline of the action
    /// timeout: a pause is cut at the deadline and a keystroke is never sent past it, so a slow engine cannot stretch the action beyond its
    /// budget. The focus retries a stale node like any action; the typing never does, because a keystroke may already have landed. An
    /// engine that declares no <c>focus</c> action or no keyboard is refused with <c>UNSUPPORTED_CAPABILITY</c> before any node resolves.
    /// </summary>
    internal async Task PressSequentiallyAsync(
        LocatorExpression expression,
        IReadOnlyList<string> chunks,
        TimeSpan delay,
        TimeSpan? timeout,
        CancellationToken token
    )
    {
        CheckAction(new LocatorAction.Focus());
        if (options.TypeText is not { } typeText || (Capabilities & EngineCapabilities.Keyboard) == 0)
        {
            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                "pressSequentially is not available on this target: its engine declares no keyboard"
            );
        }

        var deadline = DeadlineFor(timeout);
        await PerformUntilAsync(expression, new LocatorAction.Focus(), deadline, token).ConfigureAwait(false);
        TestException TimedOut(int typed) =>
            new(
                "ACTION_FAILED",
                $"operation timed out after typing {typed.ToString(CultureInfo.InvariantCulture)} of {chunks.Count.ToString(CultureInfo.InvariantCulture)} characters: {expression.Describe()}"
            );

        for (var index = 0; index < chunks.Count; index++)
        {
            if (index > 0)
            {
                // A pause the deadline cuts short ends the typing by itself. Reading the clock after the sleep would not: a timer may fire
                // a hair early, and one more character would go out past the budget.
                var remaining = deadline.Remaining(Clock.GetUtcNow());
                var cut = remaining < delay;
                await Task.Delay(cut ? remaining : delay, Clock, token).ConfigureAwait(false);
                if (cut)
                {
                    throw TimedOut(index);
                }
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw TimedOut(index);
            }

            try
            {
                await AppNotOpen.GuardAsync(() => typeText(chunks[index], false, token)).ConfigureAwait(false);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                var translated = TranslateLocatorError(ex, expression);
                if (ReferenceEquals(translated, ex))
                {
                    throw;
                }

                throw translated;
            }
        }
    }

    /// <summary>
    /// Performs one action on the one enabled match, waiting for it until <paramref name="deadline"/> a caller shares across steps (upstream's
    /// <c>performUntil</c>). With <paramref name="anyState"/> a hidden or disabled match is acted on too, as upstream's resolve does not look at the state.
    /// </summary>
    internal async Task PerformUntilAsync(
        LocatorExpression expression,
        LocatorAction action,
        Deadline deadline,
        CancellationToken token,
        bool anyState = false,
        Func<Exception, Exception?>? performFailure = null
    )
    {
        var started = Clock.GetTimestamp();
        var reads = Reads(deadline);
        var sampled = false;
        var pollStarted = Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () => Clock.GetElapsedTime(started))
                .ConfigureAwait(false);
            sampled = true;
            if (node is not null && (anyState || node is { States.Disabled: false, States.Hidden: false }))
            {
                if (deadline.Expired(Clock.GetUtcNow()))
                {
                    throw LateAction(KindOf(action), expression.Describe());
                }

                try
                {
                    await SessionPerformAsync(node, action, deadline, token).ConfigureAwait(false);
                }
                catch (EngineException ex)
                    when (ex is { Code: EngineErrorCodes.NodeStale, Retryable: true }
                        && !token.IsCancellationRequested
                        && !deadline.Expired(Clock.GetUtcNow())
                    )
                {
                    // The node went stale under the action: look again, within the deadline.
                    pollStarted = Clock.GetTimestamp();
                    continue;
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    var translated = performFailure?.Invoke(ex) ?? TranslateLocatorError(ex, expression);
                    if (ReferenceEquals(translated, ex))
                    {
                        throw;
                    }

                    throw translated;
                }

                return;
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw node is null
                    ? await NotFoundAsync(expression, Clock.GetElapsedTime(started), token).ConfigureAwait(false)
                    : new TestException(
                        "NOT_ACTIONABLE",
                        expression.Describe() + (node.States.Hidden ? " is hidden." : " is disabled.")
                    );
            }

            await Task.Delay(Time.PollDelay(Clock.GetElapsedTime(pollStarted)), Clock, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Scrolls a container to an edge within one deadline: the node <paramref name="expression"/> names, or the screen's container when there is
    /// none. A scroll that ran out of budget (an engine timeout, or input that may have landed) did not reach the edge; an engine stops a little
    /// before the deadline, so that is said whether or not it has passed, with the engine's reason, which tells a list still moving from a stuck
    /// read, and names the step's timeout rather than the time the engine took. Only the scroll's own failure is read that way: a locate or a first
    /// observation that ran out stays what it was. Upstream's <c>scrollTo</c>.
    /// </summary>
    internal async Task ScrollToAsync(
        LocatorExpression? expression,
        ScrollEdge edge,
        TimeSpan? timeout,
        CancellationToken token
    )
    {
        var action = new LocatorAction.ScrollTo(edge);
        CheckAction(action);
        var deadline = DeadlineFor(timeout);
        var budgetMs = (long)Math.Round((deadline.EndsAt - Clock.GetUtcNow()).TotalMilliseconds);
        var target = expression is null ? "" : ": " + expression.Describe();
        Exception? NotReached(Exception cause) =>
            cause
                is EngineException
                {
                    Code: EngineErrorCodes.OperationTimeout or EngineErrorCodes.ActionMayHaveCommitted
                } engine
                ? new TestException(
                    "ACTION_FAILED",
                    $"scrollTo('{EdgeName(edge)}') did not reach the edge within {budgetMs.ToString(CultureInfo.InvariantCulture)}ms ({engine.Message}){target}",
                    cause
                )
                : null;

        if (expression is null)
        {
            await ScrollScreenToAsync(action, deadline, NotReached, token).ConfigureAwait(false);
            return;
        }

        await PerformUntilAsync(expression, action, deadline, token, performFailure: NotReached).ConfigureAwait(false);
    }

    /// <summary>The name of an edge as a test spells it: <c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>.</summary>
    internal static string EdgeName(ScrollEdge edge) =>
        edge switch
        {
            ScrollEdge.Top => "top",
            ScrollEdge.Bottom => "bottom",
            ScrollEdge.Left => "left",
            ScrollEdge.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "unknown scroll edge"),
        };

    /// <summary>The screen's <c>scrollTo</c>, recorded as an engine event like a node action. Upstream's <c>scrollScreenTo</c>.</summary>
    private async Task ScrollScreenToAsync(
        LocatorAction.ScrollTo action,
        Deadline deadline,
        Func<Exception, Exception?> notReached,
        CancellationToken token
    )
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        var detail = $"scrollTo {EdgeName(action.Edge)} on the screen";
        void Record(StepEventStatus status, string? code) =>
            Steps?.RecordEvent(
                new StepEvent
                {
                    Kind = StepEventKind.Engine,
                    Name = "scrollTo",
                    StartedAt = startedAt,
                    DurationMs = (long)Stopwatch.GetElapsedTime(stamp).TotalMilliseconds,
                    Status = status,
                    Code = code,
                    Detail = detail,
                }
            );

        var scroll = options.ScrollTo
            ?? throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                "scroll is not supported by this engine."
            );
        if (deadline.Expired(Clock.GetUtcNow()))
        {
            var late = LateAction("scrollTo", "the screen");
            Record(StepEventStatus.Failed, late.Code);
            throw late;
        }

        try
        {
            await AppNotOpen
                .GuardAsync(() =>
                    scroll(action.Edge, OperationWithin(deadline), token == default ? options.Cancellation() : token)
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var failure = token.IsCancellationRequested ? ex : notReached(ex) ?? TranslateLocatorError(ex);
            Record(
                token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed,
                ErrorSerializer.Classify(failure).Code
            );
            if (ReferenceEquals(failure, ex))
            {
                throw;
            }

            throw failure;
        }

        Record(StepEventStatus.Passed, null);
    }

    /// <summary>
    /// Performs one pointer action at a viewport point, with no node behind it. Upstream's <c>performAt</c>. The point, and the
    /// <paramref name="target"/> of a swipe (which stands in for the <see cref="PointerAction.SwipeTo"/> action's own), may be percentages of the viewport:
    /// they cost one screen read inside the action's timeout.
    /// </summary>
    internal async Task PerformAtAsync(
        ScreenPoint point,
        PointerAction action,
        TimeSpan? timeout,
        CancellationToken token,
        ScreenPoint? target = null
    )
    {
        CheckPointerAction(action);
        var deadline = DeadlineFor(timeout);
        if (target is null)
        {
            var at = (await InPixelsAsync([point], deadline, token).ConfigureAwait(false))[0];
            await DispatchAtAsync(at, action, deadline, token).ConfigureAwait(false);
            return;
        }

        var resolved = await InPixelsAsync([point, target], deadline, token).ConfigureAwait(false);
        await DispatchAtAsync(
                resolved[0],
                action is PointerAction.SwipeTo swipe ? swipe with { Target = resolved[1] } : action,
                deadline,
                token
            )
            .ConfigureAwait(false);
    }

    /// <summary>The points in CSS pixels, with one viewport read if any of them is a percentage. Upstream's <c>inPixels</c>.</summary>
    private async Task<Point[]> InPixelsAsync(ScreenPoint[] points, Deadline deadline, CancellationToken token)
    {
        if (points.All(Geometry.IsPixelPoint))
        {
            return [.. points.Select(point => new Point(point.X.Pixels, point.Y.Pixels))];
        }

        var viewport = await ViewportAsync(deadline, token).ConfigureAwait(false);
        return [.. points.Select(point => Geometry.ResolveScreenPoint(point, viewport))];
    }

    /// <summary>The viewport size, read from one screen observation. Upstream's <c>viewport</c>.</summary>
    private async Task<TraceViewport> ViewportAsync(Deadline deadline, CancellationToken token) =>
        (await ObserveAsync(OperationWithin(deadline).Timeout!.Value, token).ConfigureAwait(false)).Viewport
        ?? throw new TestException(
            "ACTION_FAILED",
            "the engine did not report the viewport size, so a percentage point cannot be resolved"
        );

    /// <summary>
    /// Performs one pointer action at an offset from the origin of exactly one match (<see cref="SemanticNode.Origin"/>, else its box's top-left corner): the node is scrolled into view when the engine
    /// can, then its box is read and the pointer dispatched at the point, with no node behind the gesture. The node has to be visible with a
    /// box within the deadline; one that never is fails as <c>LOCATOR_NOT_FOUND</c>, as <c>WaitForAsync</c> does. Upstream's <c>performWithin</c>.
    /// </summary>
    internal async Task PerformWithinAsync(
        LocatorExpression expression,
        Point offset,
        PointerAction action,
        TimeSpan? timeout,
        CancellationToken token
    )
    {
        CheckPointerAction(action);
        var deadline = DeadlineFor(timeout);
        var started = Clock.GetTimestamp();
        if (ActionKinds is not { } declared || declared.Contains("scrollIntoView"))
        {
            await PerformUntilAsync(expression, new LocatorAction.ScrollIntoView(), deadline, token, anyState: true)
                .ConfigureAwait(false);
        }

        var origin = default(Point);
        await Time.PollReadsAsync(
                new PollReadsOptions
                {
                    Deadline = deadline,
                    Reads = Reads(deadline),
                    Clock = Clock,
                    Token = token,
                    Negated = false,
                    Evaluate = async reads =>
                    {
                        // A box read at the deadline could never be tapped: skip the read so the wait ends as LOCATOR_NOT_FOUND.
                        if (deadline.Expired(Clock.GetUtcNow()))
                        {
                            return false;
                        }

                        var node = await TryReadAsync(expression, token, reads).ConfigureAwait(false);
                        if (node is not { States.Hidden: false, Rect: { } rect })
                        {
                            return false;
                        }

                        origin = node.Origin ?? new Point(rect.X, rect.Y);
                        return true;
                    },
                    OnTimeout = cause =>
                    {
                        var message =
                            "locator did not become visible with a box to act within: " + expression.Describe();
                        var details = LocatorDetails(expression, Clock.GetElapsedTime(started));
                        return cause is null
                            ? new TestException("LOCATOR_NOT_FOUND", message) { Details = details }
                            : new TestException("LOCATOR_NOT_FOUND", message, cause) { Details = details };
                    },
                }
            )
            .ConfigureAwait(false);
        await DispatchAtAsync(new Point(origin.X + offset.X, origin.Y + offset.Y), action, deadline, token)
            .ConfigureAwait(false);
    }

    /// <summary>One pointer dispatch within a deadline, its engine error translated to the public taxonomy. Upstream's <c>dispatchAt</c>.</summary>
    private async Task DispatchAtAsync(Point point, PointerAction action, Deadline deadline, CancellationToken token)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        var kind = EngineActionKinds.Of(action);
        var at = string.Create(
            CultureInfo.InvariantCulture,
            $"({Math.Round(point.X, MidpointRounding.AwayFromZero)}, {Math.Round(point.Y, MidpointRounding.AwayFromZero)})"
        );
        void Record(StepEventStatus status, string? code) =>
            Steps?.RecordEvent(
                new StepEvent
                {
                    Kind = StepEventKind.Engine,
                    Name = kind,
                    StartedAt = startedAt,
                    DurationMs = (long)Stopwatch.GetElapsedTime(stamp).TotalMilliseconds,
                    Status = status,
                    Code = code,
                    Detail = kind + " at " + at,
                }
            );

        if (deadline.Expired(Clock.GetUtcNow()))
        {
            var late = LateAction(kind, at);
            Record(StepEventStatus.Failed, late.Code);
            throw late;
        }

        try
        {
            await SessionPerformAtAsync(point, action, deadline, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var failure = token.IsCancellationRequested ? ex : TranslateLocatorError(ex);
            Record(
                token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed,
                ErrorSerializer.Classify(failure).Code
            );
            if (ReferenceEquals(failure, ex))
            {
                throw;
            }

            throw failure;
        }

        Record(StepEventStatus.Passed, null);
    }

    /// <summary>The action kind an event names: the method's name but for <c>selectOption</c>, which the engine contract calls <c>select</c>.</summary>
    private static string KindOf(LocatorAction action) => action is LocatorAction.Select ? "select" : ApiName(action);

    /// <summary>
    /// Records one performed action as an <c>engine</c> event of the running step (upstream's <c>recordAction</c>): the node it landed on as
    /// the resolve read it, how long the node took to appear, and how many times it went stale under the action.
    /// </summary>
    private void RecordAction(
        SemanticNode node,
        LocatorAction action,
        DateTimeOffset startedAt,
        long started,
        long waitedMs,
        int stale,
        StepEventStatus status,
        string? code
    )
    {
        if (Steps is null)
        {
            return;
        }

        string? Field(string? value)
        {
            var text = value is null ? "" : Redact(TextRules.Normalize(value));
            return text.Length == 0 ? null : text;
        }

        var target = RecordedTarget.TargetLabel(
            new RecordedTarget
            {
                Role = node.Role,
                Name = Field(node.Name),
                Text = Field(node.Text),
                Placeholder = Field(node.AttributeOf("placeholder")),
                TestId = Field(node.TestId),
            }
        );
        var kind = KindOf(action);
        List<string> notes = [];
        if (waitedMs > 0)
        {
            notes.Add("appeared after " + waitedMs.ToString(CultureInfo.InvariantCulture) + "ms");
        }

        if (stale > 0)
        {
            notes.Add("went stale " + stale.ToString(CultureInfo.InvariantCulture) + "x");
        }

        Steps.RecordEvent(
            new StepEvent
            {
                Kind = StepEventKind.Engine,
                Name = kind,
                StartedAt = startedAt,
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Status = status,
                Code = code,
                Detail = kind + " " + target + (notes.Count == 0 ? "" : " (" + string.Join(", ", notes) + ")"),
            }
        );
    }

    /// <summary>
    /// <c>LOCATOR_AMBIGUOUS</c> for a resolve of many nodes, with the per-step counts of a filtered locator (upstream's <c>singleOf</c>).
    /// </summary>
    internal async Task<TestException> AmbiguousAsync(LocatorExpression expression, int count, CancellationToken token)
    {
        var lines = await CandidateCountsAsync(expression, token).ConfigureAwait(false);
        return new(
            "LOCATOR_AMBIGUOUS",
            expression.Describe() + " matched " + count.ToString(CultureInfo.InvariantCulture) + " nodes." + lines
        )
        {
            Details = LocatorDetails(expression) with { Matches = count },
        };
    }

    /// <summary>
    /// <c>LOCATOR_NOT_FOUND</c>, with the per-step counts of a filtered locator (upstream's <c>notFound</c> in <c>resolveCounted</c>).
    /// <paramref name="waited"/> is the wait as it happened, read before the diagnostic pass runs: the prefix resolves are not part of it.
    /// </summary>
    private async Task<TestException> NotFoundAsync(
        LocatorExpression expression,
        TimeSpan waited,
        CancellationToken token,
        Exception? cause = null
    )
    {
        var message =
            expression.Describe()
            + " matched 0 nodes."
            + await CandidateCountsAsync(expression, token).ConfigureAwait(false);
        var details = LocatorDetails(expression, waited);
        return cause is null
            ? new TestException("LOCATOR_NOT_FOUND", message) { Details = details }
            : new TestException("LOCATOR_NOT_FOUND", message, cause) { Details = details };
    }

    /// <summary>
    /// The diagnostic on a filtered locator's failure: one immediate resolve per prefix of its chain, innermost first, and how many
    /// candidates each left, as the message's per-step lines (upstream's <c>candidateCounts</c>). One deadline bounds the whole walk, so even
    /// on a session whose locate ignores its budget, a chain whose walk cannot finish lists only the prefixes it read and says so, or lists
    /// nothing when a read failed outright. The counts are read after the failure, so a page that changed since can disagree with the failing
    /// attempt, and the message says so.
    /// </summary>
    private async Task<string> CandidateCountsAsync(LocatorExpression expression, CancellationToken token)
    {
        var spine = expression.FilterSpine();
        if (spine.Count == 0)
        {
            return "";
        }

        var deadline = Deadline.Min(
            new Deadline(StepCountBudget, Clock),
            BudgetDeadline ?? new Deadline(DateTimeOffset.MaxValue)
        );
        var reads = new ReadWindow(deadline, Clock);
        List<string> lines = [];
        try
        {
            foreach (var prefix in spine)
            {
                if (deadline.Expired(Clock.GetUtcNow()))
                {
                    break;
                }

                var nodes = await ResolveNowAsync(prefix, token, reads).ConfigureAwait(false);
                lines.Add(prefix.Describe() + ": " + nodes.Count.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (Exception)
        {
            // A diagnostic read failure must not fail the failure: partial counts must not read as a completed walk, so return nothing.
            token.ThrowIfCancellationRequested();
            return "";
        }

        if (lines.Count == 0)
        {
            return "";
        }

        var stopped =
            lines.Count < spine.Count
                ? ", walk stopped after "
                    + lines.Count.ToString(CultureInfo.InvariantCulture)
                    + " of "
                    + spine.Count.ToString(CultureInfo.InvariantCulture)
                    + " steps"
                : "";
        return "\ncandidates left after each filter step"
            + stopped
            + ", counted after the failure:\n"
            + string.Join('\n', lines);
    }
}
