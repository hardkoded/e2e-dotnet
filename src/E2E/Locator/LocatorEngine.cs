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
    public Func<LocatorExpression, TimeSpan, CancellationToken, Task<IReadOnlyList<SemanticNode>>>? Locate { get; init; }

    /// <summary>One action on a node (upstream's <c>session.perform</c>), with the context that says who acts.</summary>
    public required Func<SemanticNode, LocatorAction, OperationContext, CancellationToken, Task> Perform { get; init; }

    /// <summary>One pointer action at a viewport point (upstream's <c>session.performAt</c>); null when the engine has none.</summary>
    public Func<Point, PointerAction, OperationContext, CancellationToken, Task>? PerformAt { get; init; }

    /// <summary>The running phase's token (upstream's <c>budget.signal</c>), read per operation.</summary>
    public required Func<CancellationToken> Cancellation { get; init; }

    /// <summary>How long an action waits for its node.</summary>
    public required TimeSpan ActionTimeout { get; init; }

    /// <summary>How long an assertion waits.</summary>
    public required TimeSpan AssertionTimeout { get; init; }

    /// <summary>The clock every deadline reads.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;
}

/// <summary>Per-attempt locator execution engine: runner-owned query polling, strictness, and action retry (upstream's <c>LocatorEngine</c>, <c>locator/engine.ts</c>).</summary>
internal sealed class LocatorEngine(LocatorEngineOptions options)
{
    /// <summary>How long an action waits for its node.</summary>
    internal TimeSpan ActionTimeout { get; } = options.ActionTimeout;

    /// <summary>How long an assertion waits (upstream's <c>assertionTimeout</c>).</summary>
    internal TimeSpan AssertionTimeout { get; } = options.AssertionTimeout;

    internal TimeProvider Clock { get; } = options.Clock;

    internal TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(50);

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
    /// over <see cref="ObserveAsync(CancellationToken)"/> (.NET-only), until it implements <see cref="IEngineSession.LocateAsync"/>.
    /// </summary>
    internal bool LocatesInEngine => options.Locate is not null;

    /// <summary>Canonical visibility predicate over a semantic node snapshot (upstream's <c>isNodeVisible</c>).</summary>
    internal static bool IsNodeVisible(SemanticNode? node) =>
        node is { States: { Hidden: false, EmptyBox: false } };

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
        expression.ExpressionHints() with { Locator = expression.Describe(), WaitedMs = waited is { } elapsed ? (long)elapsed.TotalMilliseconds : null };

    /// <summary>
    /// Translates an engine error into the runner-owned public taxonomy (upstream's <c>translateLocatorError</c>), naming
    /// <paramref name="expression"/> in the message when one is given. A missing or ambiguous frame also carries the locator's details.
    /// </summary>
    internal static Exception TranslateLocatorError(Exception cause, LocatorExpression? expression = null)
    {
        var translated = ErrorSerializer.TranslateEngineError(cause, expression is null ? "" : ": " + expression.Describe());
        return expression is not null && translated is TestException { Code: "LOCATOR_NOT_FOUND" or "LOCATOR_AMBIGUOUS" } frame && cause is EngineException { Code: EngineErrorCodes.FrameNotFound or EngineErrorCodes.FrameAmbiguous }
            ? new TestException(frame.Code, frame.Message, frame.InnerException!) { Details = LocatorDetails(expression) }
            : translated;
    }

    /// <summary>The method a test called, as upstream names the action: tap, doubleTap, fill, selectOption.</summary>
    internal static string ApiName(LocatorAction action) => action switch
    {
        LocatorAction.Tap => "tap",
        LocatorAction.DoubleTap => "doubleTap",
        LocatorAction.Fill => "fill",
        LocatorAction.Press => "press",
        LocatorAction.PressSequentially => "pressSequentially",
        LocatorAction.Select => "selectOption",
        LocatorAction.SetInputFiles => "setInputFiles",
        LocatorAction.Check => "check",
        LocatorAction.Uncheck => "uncheck",
        LocatorAction.Clear => "clear",
        LocatorAction.Focus => "focus",
        LocatorAction.ScrollIntoView => "scrollIntoView",
        LocatorAction.Swipe => "swipe",
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
    internal CancellationToken Token(CancellationToken cancellationToken) => Time.Link(cancellationToken, options.Cancellation());

    /// <summary>The reads of one wait on <paramref name="deadline"/>: the last may run past it, up to the test timeout (upstream's <c>reads</c>).</summary>
    internal ReadWindow Reads(Deadline deadline) => new(deadline, Clock, BudgetDeadline ?? new Deadline(DateTimeOffset.MaxValue));

    /// <summary>
    /// The timeout of one action-family operation: <paramref name="timeout"/> when given, the action timeout otherwise. Upstream's
    /// <c>deadline(timeoutMs)</c> builds the deadline itself; <see cref="Deadline"/> is also this type's name here (.NET-only name). A
    /// negative <paramref name="timeout"/> is <c>INVALID_ARGUMENT</c>. Zero sets no deadline of its own: the budget is what is left of the
    /// running phase's deadline.
    /// </summary>
    internal TimeSpan TimeoutOf(TimeSpan? timeout) => Time.StepBudget(timeout, "the step", ActionTimeout, BudgetDeadline, Clock);

    /// <summary>
    /// Deadline for one action-family operation (upstream's <c>deadline</c>; .NET-only name, as <c>Deadline</c> is the type). Zero sets no
    /// deadline of its own, so the running phase's alone bounds it.
    /// </summary>
    internal Deadline DeadlineFor(TimeSpan? timeout = null) => Time.StepDeadline(timeout, "the step", ActionTimeout, BudgetDeadline, Clock);

    /// <summary>Deadline for one assertion on a matcher's own <paramref name="timeout"/>, the assertion timeout by default; <paramref name="api"/> names the call in a refusal.</summary>
    internal Deadline AssertionDeadlineFor(TimeSpan? timeout, string api) => Time.StepDeadline(timeout, api, AssertionTimeout, BudgetDeadline, Clock);

    /// <summary>One observation of the screen, an unopened app reported as <c>APP_NOT_OPEN</c>.</summary>
    internal async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        return await AppNotOpen.GuardAsync(() => options.Observe(cancellationToken == default ? options.Cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    /// <summary><see cref="ObserveAsync(CancellationToken)"/> within <paramref name="timeout"/>, the budget the engine bounds its reads by (upstream's <c>operation.timeoutMs</c>).</summary>
    internal async Task<Observation> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var token = cancellationToken == default ? options.Cancellation() : cancellationToken;
        return await AppNotOpen.GuardAsync(() => options.ObserveWithin is { } within ? within(timeout, token) : options.Observe(token)).ConfigureAwait(false);
    }

    /// <summary>One immediate engine resolve: the nodes the query matches now, within <paramref name="timeout"/> of the engine's own work. Upstream's <c>session.locate</c>.</summary>
    private async Task<IReadOnlyList<SemanticNode>> SessionLocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return await AppNotOpen.GuardAsync(() => options.Locate!(expression, timeout, cancellationToken == default ? options.Cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Carries out one action on a node (upstream's <c>session.perform</c>, marked with the test origin: the engine may act without settling).</summary>
    internal async Task SessionPerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
    {
        // The session adapter's own guard (upstream's `session.ts`): an action built per attempt, such as a drag, is checked when it is sent.
        CheckAction(action);
        await AppNotOpen.GuardAsync(() => options.Perform(node, action, OperationContext.Test, cancellationToken == default ? options.Cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Carries out one pointer action at a viewport point, with no node behind it. Upstream's <c>session.performAt</c>.</summary>
    internal async Task SessionPerformAtAsync(Point point, PointerAction action, CancellationToken cancellationToken)
    {
        CheckPointerAction(action);
        var perform = options.PerformAt ?? throw new EngineException(EngineErrorCodes.UnsupportedCapability, "the pointer actions are not supported by this engine.");
        await AppNotOpen.GuardAsync(() => perform(point, action, OperationContext.Test, cancellationToken == default ? options.Cancellation() : cancellationToken)).ConfigureAwait(false);
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
            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                $"the \"{kind}\" action is not available on this target: its engine declares {(declared.Count == 0 ? "no actions" : string.Join(", ", declared))}");
        }
    }

    /// <summary>Refuses a pointer action the engine did not declare: <c>UNSUPPORTED_CAPABILITY</c>. Upstream's <c>checkPointerAction</c>.</summary>
    internal void CheckPointerAction(PointerAction action)
    {
        var kind = EngineActionKinds.Of(action);
        var declared = PointerActionKinds ?? [];
        if (!declared.Contains(kind))
        {
            throw new EngineException(
                EngineErrorCodes.UnsupportedCapability,
                $"the \"{kind}\" action at a point is not available on this target: its engine declares {(declared.Count == 0 ? "no pointer actions" : string.Join(", ", declared))}");
        }
    }

    /// <summary>
    /// One immediate resolve, retrying a retryable engine error within the deadline (upstream's <c>resolveOnce</c>). The engine answers it
    /// when it resolves locators, and a role query of its skips a hidden node unless <paramref name="includeHidden"/> asks for it; any
    /// other engine is resolved over its observation. <paramref name="reads"/> bounds the retries and the budget of each read, and defaults
    /// to a window of the action timeout. With <see cref="MissingFrame.Empty"/> a retryable <c>FRAME_NOT_FOUND</c> is zero matches instead:
    /// nothing under a frame that is not in the document matches, and a read that promised not to wait must not wait for it.
    /// </summary>
    internal async Task<IReadOnlyList<SemanticNode>> ResolveOnceAsync(LocatorExpression expression, CancellationToken token, bool includeHidden = false, ReadWindow? reads = null, MissingFrame missingFrame = MissingFrame.Wait)
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
        while (true)
        {
            try
            {
                var nodes = await reads.ReadAsync(budget => SessionLocateAsync(query, budget, token)).ConfigureAwait(false);

                // An engine answers `visible` inside scopes, filters, and indices, where only it can. A direct query is held to the same
                // predicate here, so it never resolves to a node its own state calls hidden.
                return expression.Visible && expression.Kind is not ("filter" or "index" or "selector") ? nodes.Where(node => IsNodeVisible(node)).ToList() : nodes;
            }
            catch (EngineException ex) when (retried is not null && !token.IsCancellationRequested && reads.CutOff(ex))
            {
                // A retry the deadline cut off saw nothing new: the wait ends on the failure it was retrying.
                throw TranslateLocatorError(retried, expression);
            }
            catch (EngineException ex) when (missingFrame == MissingFrame.Empty && ex is { Code: EngineErrorCodes.FrameNotFound, Retryable: true })
            {
                return [];
            }
            catch (EngineException ex) when (ex.Retryable && !reads.Deadline.Expired(Clock.GetUtcNow()) && !token.IsCancellationRequested)
            {
                retried = ex;
                await Task.Delay(PollInterval, token).ConfigureAwait(false);
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
    internal Task<IReadOnlyList<SemanticNode>> ResolveNowAsync(LocatorExpression expression, CancellationToken token, ReadWindow? reads = null) =>
        ResolveOnceAsync(expression, token, reads: reads, missingFrame: MissingFrame.Empty);

    /// <summary>
    /// Reads every current match at once, in document order: a missing frame reads as no matches. A locate answers the nodes themselves,
    /// so there is no separate read to go stale between a resolve and a read (upstream's <c>session.read</c>).
    /// </summary>
    internal Task<IReadOnlyList<SemanticNode>> ReadAllNowAsync(LocatorExpression expression, CancellationToken token, ReadWindow? reads = null) =>
        ResolveNowAsync(expression, token, reads);

    /// <summary>
    /// Reads the one current match at once, for <c>isVisible</c> and <c>isHidden</c>: null for zero matches or a missing frame; two matches
    /// are <c>LOCATOR_AMBIGUOUS</c>.
    /// </summary>
    internal Task<SemanticNode?> ReadNowAsync(LocatorExpression expression, CancellationToken token) =>
        TryReadAsync(expression, token, missingFrame: MissingFrame.Empty);

    /// <summary>
    /// Reads one node for polling surfaces; null while zero matches. A poll that waits for absence passes <see cref="MissingFrame.Empty"/>:
    /// nothing under a frame that is not in the document is attached, so the sample is zero matches rather than a wait for the frame.
    /// </summary>
    internal async Task<SemanticNode?> TryReadAsync(LocatorExpression expression, CancellationToken token, ReadWindow? reads = null, MissingFrame missingFrame = MissingFrame.Wait)
    {
        var matches = await ResolveOnceAsync(expression, token, reads: reads, missingFrame: missingFrame).ConfigureAwait(false);
        return matches.Count > 1 ? throw await AmbiguousAsync(expression, matches.Count, token).ConfigureAwait(false) : matches.Count == 1 ? matches[0] : null;
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
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () => Stopwatch.GetElapsedTime(stamp)).ConfigureAwait(false);
            sampled = true;
            if (node is not null)
            {
                return node;
            }

            if (Clock.GetUtcNow() >= deadline)
            {
                throw await NotFoundAsync(expression, Clock.GetUtcNow() - started, token).ConfigureAwait(false);
            }

            await Task.Delay(PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until <paramref name="deadline"/> for the one match. Zero matches poll until the deadline; more than one match fails at once.
    /// Upstream's <c>resolveExactlyOne</c>, which a drag uses for the node it drops on.
    /// </summary>
    internal async Task<SemanticNode> ResolveExactlyOneAsync(LocatorExpression expression, Deadline deadline, CancellationToken token)
    {
        var started = Clock.GetTimestamp();
        var reads = Reads(deadline);
        var sampled = false;
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () => Clock.GetElapsedTime(started)).ConfigureAwait(false);
            sampled = true;
            if (node is not null)
            {
                return node;
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw await NotFoundAsync(expression, Clock.GetElapsedTime(started), token).ConfigureAwait(false);
            }

            await Task.Delay(PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <see cref="TryReadAsync"/> for a wait: a resolve the deadline cut off, after one that matched nothing, is the wait running out
    /// (upstream's <c>resolveCounted</c>), reported as not found rather than as the timeout of the read.
    /// </summary>
    private async Task<SemanticNode?> ResolveCountedAsync(LocatorExpression expression, CancellationToken token, ReadWindow reads, bool sampled, Func<TimeSpan> elapsed)
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
    internal async Task PerformAsync(LocatorExpression expression, Func<Deadline, CancellationToken, Task<LocatorAction>> build, TimeSpan? timeout, CancellationToken token, bool anyState = false)
    {
        var started = Clock.GetUtcNow();
        var deadline = started + TimeoutOf(timeout);
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        // Each resolve counts its own rounds and wait, as upstream's resolveCounted does: a stale retry starts them over.
        var resolveStamp = stamp;
        var rounds = 0;
        var stale = 0;
        var window = new Deadline(deadline);
        var reads = Reads(window);
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled: rounds > 0, () => Stopwatch.GetElapsedTime(resolveStamp)).ConfigureAwait(false);
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
                    await SessionPerformAsync(node, action, token).ConfigureAwait(false);
                }
                catch (EngineException ex) when (ex is { Code: EngineErrorCodes.NodeStale, Retryable: true } && !token.IsCancellationRequested && !window.Expired(Clock.GetUtcNow()))
                {
                    // The node went stale under the action: look again, within the deadline.
                    stale++;
                    rounds = 0;
                    resolveStamp = Stopwatch.GetTimestamp();
                    continue;
                }
                catch (Exception ex)
                {
                    var failure = token.IsCancellationRequested ? ex : TranslateLocatorError(ex, expression);
                    RecordAction(node, action, startedAt, stamp, waitedMs, stale, token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed, ErrorSerializer.Classify(failure).Code);
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
                    : new TestException("NOT_ACTIONABLE", expression.Describe() + (node.States.Hidden ? " is hidden." : " is disabled."));
            }

            await Task.Delay(PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs one action on the one enabled match, waiting for it until <paramref name="deadline"/> a caller shares across steps (upstream's
    /// <c>performUntil</c>). With <paramref name="anyState"/> a hidden or disabled match is acted on too, as upstream's resolve does not look at the state.
    /// </summary>
    internal async Task PerformUntilAsync(LocatorExpression expression, LocatorAction action, Deadline deadline, CancellationToken token, bool anyState = false)
    {
        var started = Clock.GetTimestamp();
        var reads = Reads(deadline);
        var sampled = false;
        while (true)
        {
            var node = await ResolveCountedAsync(expression, token, reads, sampled, () => Clock.GetElapsedTime(started)).ConfigureAwait(false);
            sampled = true;
            if (node is not null && (anyState || node is { States.Disabled: false, States.Hidden: false }))
            {
                if (deadline.Expired(Clock.GetUtcNow()))
                {
                    throw LateAction(KindOf(action), expression.Describe());
                }

                try
                {
                    await SessionPerformAsync(node, action, token).ConfigureAwait(false);
                }
                catch (EngineException ex) when (ex is { Code: EngineErrorCodes.NodeStale, Retryable: true } && !token.IsCancellationRequested && !deadline.Expired(Clock.GetUtcNow()))
                {
                    // The node went stale under the action: look again, within the deadline.
                    continue;
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

                return;
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw node is null
                    ? await NotFoundAsync(expression, Clock.GetElapsedTime(started), token).ConfigureAwait(false)
                    : new TestException("NOT_ACTIONABLE", expression.Describe() + (node.States.Hidden ? " is hidden." : " is disabled."));
            }

            await Task.Delay(PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>Performs one pointer action at a viewport point, with no node behind it. Upstream's <c>performAt</c>.</summary>
    internal async Task PerformAtAsync(Point point, PointerAction action, TimeSpan? timeout, CancellationToken token)
    {
        CheckPointerAction(action);
        await DispatchAtAsync(point, action, new Deadline(TimeoutOf(timeout), Clock), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs one pointer action at an offset from the top-left corner of exactly one match: the node is scrolled into view when the engine
    /// can, then its box is read and the pointer dispatched at the point, with no node behind the gesture. The node has to be visible with a
    /// box within the deadline; one that never is fails as <c>LOCATOR_NOT_FOUND</c>, as <c>WaitForAsync</c> does. Upstream's <c>performWithin</c>.
    /// </summary>
    internal async Task PerformWithinAsync(LocatorExpression expression, Point offset, PointerAction action, TimeSpan? timeout, CancellationToken token)
    {
        CheckPointerAction(action);
        var deadline = new Deadline(TimeoutOf(timeout), Clock);
        var started = Clock.GetTimestamp();
        if (ActionKinds is not { } declared || declared.Contains("scrollIntoView"))
        {
            await PerformUntilAsync(expression, new LocatorAction.ScrollIntoView(), deadline, token, anyState: true).ConfigureAwait(false);
        }

        TestException NotVisible(Exception? cause)
        {
            var message = "locator did not become visible with a box to act within: " + expression.Describe();
            var details = LocatorDetails(expression, Clock.GetElapsedTime(started));
            return cause is null ? new TestException("LOCATOR_NOT_FOUND", message) { Details = details } : new TestException("LOCATOR_NOT_FOUND", message, cause) { Details = details };
        }

        var reads = Reads(deadline);
        var sampled = false;
        while (true)
        {
            SemanticNode? node;
            try
            {
                node = await TryReadAsync(expression, token, reads).ConfigureAwait(false);
            }
            catch (Exception ex) when (sampled && !token.IsCancellationRequested && reads.CutOff(ex))
            {
                throw NotVisible(ex);
            }

            sampled = true;
            if (node is { States.Hidden: false, Rect: { } rect })
            {
                await DispatchAtAsync(new Point(rect.X + offset.X, rect.Y + offset.Y), action, deadline, token).ConfigureAwait(false);
                return;
            }

            if (deadline.Expired(Clock.GetUtcNow()))
            {
                throw NotVisible(null);
            }

            await Task.Delay(PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>One pointer dispatch within a deadline, its engine error translated to the public taxonomy. Upstream's <c>dispatchAt</c>.</summary>
    private async Task DispatchAtAsync(Point point, PointerAction action, Deadline deadline, CancellationToken token)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        var kind = EngineActionKinds.Of(action);
        var at = string.Create(CultureInfo.InvariantCulture, $"({Math.Round(point.X, MidpointRounding.AwayFromZero)}, {Math.Round(point.Y, MidpointRounding.AwayFromZero)})");
        void Record(StepEventStatus status, string? code) => Steps?.RecordEvent(new StepEvent
        {
            Kind = StepEventKind.Engine,
            Name = kind,
            StartedAt = startedAt,
            DurationMs = (long)Stopwatch.GetElapsedTime(stamp).TotalMilliseconds,
            Status = status,
            Code = code,
            Detail = kind + " at " + at,
        });

        if (deadline.Expired(Clock.GetUtcNow()))
        {
            var late = LateAction(kind, at);
            Record(StepEventStatus.Failed, late.Code);
            throw late;
        }

        try
        {
            await SessionPerformAtAsync(point, action, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var failure = token.IsCancellationRequested ? ex : TranslateLocatorError(ex);
            Record(token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed, ErrorSerializer.Classify(failure).Code);
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
    private void RecordAction(SemanticNode node, LocatorAction action, DateTimeOffset startedAt, long started, long waitedMs, int stale, StepEventStatus status, string? code)
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

        var target = RecordedTarget.TargetLabel(new RecordedTarget
        {
            Role = node.Role,
            Name = Field(node.Name),
            Text = Field(node.Text),
            Placeholder = Field(node.AttributeOf("placeholder")),
            TestId = Field(node.TestId),
        });
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

        Steps.RecordEvent(new StepEvent
        {
            Kind = StepEventKind.Engine,
            Name = kind,
            StartedAt = startedAt,
            DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Status = status,
            Code = code,
            Detail = kind + " " + target + (notes.Count == 0 ? "" : " (" + string.Join(", ", notes) + ")"),
        });
    }

    /// <summary>
    /// <c>LOCATOR_AMBIGUOUS</c> for a resolve of many nodes, with the per-step counts of a filtered locator (upstream's <c>singleOf</c>).
    /// </summary>
    internal async Task<TestException> AmbiguousAsync(LocatorExpression expression, int count, CancellationToken token)
    {
        var lines = await CandidateCountsAsync(expression, token).ConfigureAwait(false);
        return new("LOCATOR_AMBIGUOUS", expression.Describe() + " matched " + count.ToString(CultureInfo.InvariantCulture) + " nodes." + lines) { Details = LocatorDetails(expression) with { Matches = count } };
    }

    /// <summary>
    /// <c>LOCATOR_NOT_FOUND</c>, with the per-step counts of a filtered locator (upstream's <c>notFound</c> in <c>resolveCounted</c>).
    /// <paramref name="waited"/> is the wait as it happened, read before the diagnostic pass runs: the prefix resolves are not part of it.
    /// </summary>
    private async Task<TestException> NotFoundAsync(LocatorExpression expression, TimeSpan waited, CancellationToken token, Exception? cause = null)
    {
        var message = expression.Describe() + " matched 0 nodes." + await CandidateCountsAsync(expression, token).ConfigureAwait(false);
        var details = LocatorDetails(expression, waited);
        return cause is null ? new TestException("LOCATOR_NOT_FOUND", message) { Details = details } : new TestException("LOCATOR_NOT_FOUND", message, cause) { Details = details };
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

        var deadline = Deadline.Min(new Deadline(StepCountBudget, Clock), BudgetDeadline ?? new Deadline(DateTimeOffset.MaxValue));
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

        var stopped = lines.Count < spine.Count ? ", walk stopped after " + lines.Count.ToString(CultureInfo.InvariantCulture) + " of " + spine.Count.ToString(CultureInfo.InvariantCulture) + " steps" : "";
        return "\ncandidates left after each filter step" + stopped + ", counted after the failure:\n" + string.Join('\n', lines);
    }
}
