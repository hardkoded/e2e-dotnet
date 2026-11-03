// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

// The point verbs of upstream's action dispatcher (agent/action-dispatcher.ts): tapAt, hoverAt, dragAt, and hitTest.
internal sealed partial class ActionDispatcher
{
    public Task<PointHit> HitTestAsync(Point point, CancellationToken cancellationToken = default)
    {
        Geometry.RequireFinitePoint(point, "hitTest");
        // Resolved in queue order, so a batched turn reads each point against the screen its earlier actions left.
        return _accounting.Queue.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = RequireLatest();
            var clamped = Geometry.ClampToViewport(point, ViewportOf(observation));
            // The control is reported whatever the engine can do with it: the verb the executor calls next is gated on its own action kind.
            var hit = PointTap.HitTest(observation, clamped);
            return Task.FromResult(new PointHit(
                clamped,
                hit.Control is null ? null : new ExecutorTarget(hit.Control),
                hit.Under is null ? null : new ExecutorTarget(hit.Under),
                PointTap.DescribePointHit(clamped, hit.Control, hit.Under, Prose())));
        });
    }

    public Task<PointTapResult> TapAtAsync(Point point, CancellationToken cancellationToken = default) => PointVerbAsync("tapAt", point, cancellationToken);

    public Task<PointTapResult> HoverAtAsync(Point point, CancellationToken cancellationToken = default) => PointVerbAsync("hoverAt", point, cancellationToken);

    // Upstream's pointVerb: taps or hovers one viewport point in queue order against the newest observation. The engine gets the bare point,
    // where the model aimed, and the trace keeps it inside the node under it. An engine that takes no bare point gets the listed, enabled
    // control containing it, acted on by its id. The point is clamped to the viewport first, so one placed off the edge lands on the edge
    // rather than failing the engine.
    private Task<PointTapResult> PointVerbAsync(string verb, Point point, CancellationToken cancellationToken)
    {
        Geometry.RequireFinitePoint(point, verb);
        var nodeVerb = PointTap.PointVerbs[verb].Node;
        return _accounting.Queue.RunAsync(async () =>
        {
            var observation = RequireLatest();
            var viewport = ViewportOf(observation);
            var clamped = Geometry.ClampToViewport(point, viewport);
            var hit = PointTap.HitTest(observation, clamped);
            // A tap by id lands on the control's center, not where the model aimed: only an engine without the bare point gets it, and
            // only when it takes the node verb.
            var bare = TakesBarePoint(verb);
            var control = hit.Control is not null && !bare && TakesNodeVerb(nodeVerb) ? hit.Control : null;
            var summary = PointTap.DescribePointAction(verb, clamped, hit.Control, hit.Under, control is not null, Prose());
            if (control is not null)
            {
                var target = new ExecutorTarget(control);
                await RunNowAsync(nodeVerb, token => ByIdAsync(control, nodeVerb, token), cancellationToken).ConfigureAwait(false);
                return new PointTapResult(clamped, target, summary);
            }

            if (!bare)
            {
                throw new TestException(
                    "UNSUPPORTED_CAPABILITY",
                    "the point (" + Num(clamped.X) + ", " + Num(clamped.Y) + ") is on nothing the screen lists and this engine " + nodeVerb + "s listed nodes only; " + nodeVerb + " a node by id instead");
            }

            var recorded = new RecordedAction { Kind = verb, Point = clamped, Viewport = viewport, Under = Place(observation, hit.Under, clamped) };
            await RunNowAsync(
                verb,
                async token =>
                {
                    await _scope.Session.PerformAtAsync(clamped, nodeVerb == "tap" ? new PointerAction.Tap() : new PointerAction.Hover(), OperationContext.Agent, token).ConfigureAwait(false);
                    await CommitAsync(recorded).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return new PointTapResult(clamped, hit.Control is null ? null : new ExecutorTarget(hit.Control), summary);
        });
    }

    public Task<PointDragResult> DragAtAsync(Point from, Point to, CancellationToken cancellationToken = default)
    {
        Geometry.RequireFinitePoint(from, "dragAt");
        Geometry.RequireFinitePoint(to, "dragAt");
        // Upstream's pointDrag: drags between two points in queue order against the newest observation, the way a point verb taps one. The
        // engine gets the bare points through its dragTo pointer kind, where the model aimed, and the trace keeps each inside the node
        // under it. Both points are clamped to the viewport first.
        return _accounting.Queue.RunAsync(async () =>
        {
            var observation = RequireLatest();
            var viewport = ViewportOf(observation);
            var start = Geometry.ClampToViewport(from, viewport);
            var end = Geometry.ClampToViewport(to, viewport);
            var fromHit = PointTap.HitTest(observation, start);
            var toHit = PointTap.HitTest(observation, end);
            // A drag by id runs between the controls' centers, not where the model aimed: only an engine without the bare drag gets it, and
            // only between two different controls, since a node dragged onto itself does nothing.
            var byId = !TakesBarePoint("dragAt") && TakesNodeVerb("drag") && fromHit.Control is not null && toHit.Control is not null && fromHit.Control.Ref != toHit.Control.Ref;
            var summary = PointTap.DescribePointDrag(start, end, fromHit.Control, toHit.Control, fromHit.Under, toHit.Under, byId, Prose());
            if (byId)
            {
                var source = new ExecutorTarget(fromHit.Control!);
                var destination = new ExecutorTarget(toHit.Control!);
                await RunNowAsync("dragTo", token => DragOntoAsync(source, destination, token), cancellationToken).ConfigureAwait(false);
                return new PointDragResult(start, end, source, destination, summary);
            }

            if (!TakesBarePoint("dragAt"))
            {
                throw new TestException(
                    "UNSUPPORTED_CAPABILITY",
                    "the drag from (" + Num(start.X) + ", " + Num(start.Y) + ") to (" + Num(end.X) + ", " + Num(end.Y)
                    + ") does not join two different controls the screen lists and this engine drags listed nodes only; drag a node onto another by id instead");
            }

            var recorded = new RecordedAction
            {
                Kind = "dragAt",
                Viewport = viewport,
                From = new RecordedPointEnd { Point = start, Under = Place(observation, fromHit.Under, start) },
                To = new RecordedPointEnd { Point = end, Under = Place(observation, toHit.Under, end) },
            };
            await RunNowAsync(
                "dragAt",
                async token =>
                {
                    await _scope.Session.PerformAtAsync(start, new PointerAction.DragTo(end), OperationContext.Agent, token).ConfigureAwait(false);
                    await CommitAsync(recorded).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return new PointDragResult(
                start,
                end,
                fromHit.Control is null ? null : new ExecutorTarget(fromHit.Control),
                toHit.Control is null ? null : new ExecutorTarget(toHit.Control),
                summary);
        });
    }

    // The tap or hover of a listed control by its id, for an engine without the bare point: the node verb of upstream's pointVerb.
    private async Task ByIdAsync(SemanticNode control, string nodeVerb, CancellationToken token)
    {
        if (nodeVerb == "hover")
        {
            var hovered = await TargetedAsync(control, new LocatorAction.Hover(), token).ConfigureAwait(false);
            await CommitAsync("hover", hovered).ConfigureAwait(false);
            return;
        }

        var node = await TargetedAsync(control, ClickAction("tap", null), token).ConfigureAwait(false);
        await CommitAsync(Target(node, "tap", WithinOf(node))).ConfigureAwait(false);
    }

    // Upstream's `verbs.has(nodeVerb)`: whether the engine takes the node verb a point verb falls back to. The tap is always there; the hover and the
    // drag are the pointer verbs the engine declares by locator action kind.
    private bool TakesNodeVerb(string nodeVerb) => nodeVerb == "tap" || _scope.ActionKinds?.Contains(nodeVerb == "drag" ? "dragTo" : nodeVerb) == true;

    // Whether the engine declared the pointer kind a point verb needs: upstream's `verbs.has('tapAt')`, `hoverAt`, and `dragAt`.
    private bool TakesBarePoint(string verb)
    {
        var kind = verb switch
        {
            "tapAt" => "tap",
            "hoverAt" => "hover",
            _ => "dragTo",
        };
        return _scope.Session.PointerActionKinds.Contains(kind);
    }

    // Upstream's requireLatest: the newest observation, which an action addresses; before the first look there is nothing to address.
    private Observation RequireLatest() =>
        _feed.Latest ?? throw new TestException("LOCATOR_NOT_FOUND", "no observation has been captured yet; observe before acting");

    // The viewport a point is placed in. An engine that reports none cannot place one.
    private static TraceViewport ViewportOf(Observation observation) =>
        observation.Viewport ?? throw new TestException("UNSUPPORTED_CAPABILITY", "this engine reports no viewport size, so a point cannot be placed on it");

    // How point results name nodes: by the line of the screen the model holds.
    private PointTap.NodeProse Prose() => node => SnapshotText.FormatNode(node, 0, _scope.Redactor, _scope.AppOrigin).TrimStart();

    // Upstream's traceHandle with withinOf: the listed node whose box contained the point, with where that node sat and the point's place
    // inside its box, so replay can follow the node when the layout shifts. Nothing for a screen without a tree, or for a node a replay could
    // not re-find. The port's tree has no single root that stands for the page (it lists the page's top-level nodes), so upstream's skip of
    // the root has nothing to skip.
    private RecordedPointPlace? Place(Observation observation, SemanticNode? under, Point point)
    {
        if (under is null || observation.TreeUnavailable || under.Rect is not { Width: > 0, Height: > 0 } box)
        {
            return null;
        }

        var target = TraceRecorder.DescribeTarget(under, _scope.Redactor, WithinOf(under));
        if (target.Name is null && target.TestId is null)
        {
            return null;
        }

        return new RecordedPointPlace { Target = target, Fx = Fraction((point.X - box.X) / box.Width), Fy = Fraction((point.Y - box.Y) / box.Height) };
    }

    // A place inside a box as a fraction of its side, clamped to the box and rounded so the entry stays small.
    private static double Fraction(double value) => Math.Round(Math.Clamp(value, 0, 1) * 10_000, MidpointRounding.AwayFromZero) / 10_000;

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
