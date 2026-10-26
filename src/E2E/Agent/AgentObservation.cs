// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The shape the screen had when the preceding action was resolved, and the deadline for the screen to leave it: the
/// action's settle policy (<see cref="SettlePolicy"/>) decides the window, armed once the action commits.
/// </summary>
internal sealed record PendingChange(string Shape, DateTimeOffset Deadline);

/// <summary>The waits of one settle, decided by the caller; the loop itself keeps no defaults for them.</summary>
internal sealed class SettleOptions<T>
{
    /// <summary>
    /// The pre-action shape to leave first, when an action is pending. The settle waits, bounded by its window, for
    /// the shape to differ from it, so an action's result is read after its effect rather than before.
    /// </summary>
    public PendingChange? ChangedFrom { get; init; }

    /// <summary>
    /// Whether a capture is a screen in transition rather than a screen: an empty document between two pages, say.
    /// After an action, such a capture satisfies neither the change wait nor the stability check.
    /// </summary>
    public Func<T, bool>? Transitional { get; init; }

    /// <summary>
    /// How long the loop proves the new shape holds still: captures a poll apart until two agree, or this runs out.
    /// Zero reads the first capture past the change wait as it is.
    /// </summary>
    public TimeSpan StableWait { get; init; }

    public TimeSpan? Poll { get; init; }
}

/// <summary>Whether requested pixels became model input: the image sized for the model, or why they did not.</summary>
internal readonly record struct PixelsOutcome(EngineScreenshot? Pixels, VisionDegradation? Withheld);

/// <summary>An observation as the settle loop reads it: its shape, whether it is a screen in transition, and the loop itself.</summary>
internal static class AgentObservation
{
    /// <summary>Poll interval for shape-stability settling.</summary>
    private static readonly TimeSpan SettlePoll = TimeSpan.FromMilliseconds(75);

    private static readonly Regex Reference = new(@" \[ref=[^\]]*\]", RegexOptions.CultureInvariant);

    private static readonly Regex Focus = new(@" \[focused\]", RegexOptions.CultureInvariant);

    /// <summary><c>12:05</c>, <c>0:59</c>, <c>23:59:59</c>: a value that changes on its own once a second or minute.</summary>
    private static readonly Regex Clock = new(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> EmptyRoots = new(["document", "screen", "window"], StringComparer.Ordinal);

    /// <summary>
    /// The shape two looks are compared by: the screen as the model reads it, without the per-observation node
    /// references, without focus, which moves without the page changing, and with clock-like values read as one
    /// placeholder, which tick without the page changing. The scroll position counts too: upstream's screenshot shows a
    /// scroll the tree does not, and the port's observations carry no screenshot.
    /// </summary>
    public static string Shape(Observation observation) => Shape(SnapshotText.Render(observation, Redactor.None)) + "\n" + observation.ScrollPosition;

    /// <summary>The shape of a screen already rendered as text.</summary>
    public static string Shape(string text) => Clock.Replace(Focus.Replace(Reference.Replace(text, ""), ""), "<time>");

    /// <summary>
    /// One observed string with every secret taken out, as upstream's <c>redactText</c> (agent/observation.ts) does,
    /// in the form it is written and in the one-line form a line, a descriptor, or an anchor shows it. A field exactly
    /// as long as <paramref name="limit"/>, and so possibly cut there, passes through <see cref="Redactor.RedactCut"/>:
    /// a secret the cut stopped partway through leaves a leading part at the end that no whole value matches. Any
    /// other field is whole, and passes through <see cref="Redactor.Redact"/>. A field whose collapsed form still
    /// holds a secret (the whitespace inside a value widened) is kept collapsed and redacted, so no later collapse
    /// brings the value back. Unlike upstream, which redacts every field of the tree once when it builds the
    /// observation, the port redacts where a field is shown or recorded: the engine reads a node's name, role, and
    /// test id as the page wrote them (to tell twins apart), so the agent keeps the raw nodes.
    /// </summary>
    public static string RedactText(string text, Redactor redactor, int? limit = null)
    {
        string Pass(string value) => text.Length == limit ? redactor.RedactCut(value) : redactor.Redact(value);
        var redacted = Pass(text);
        var collapsed = TextRules.Collapse(redacted);
        if (string.Equals(collapsed, redacted, StringComparison.Ordinal))
        {
            return redacted;
        }

        var read = Pass(collapsed);
        return string.Equals(read, collapsed, StringComparison.Ordinal) ? redacted : read;
    }

    /// <summary>
    /// Whether requested pixels become model input, decided once per observation for every call that asks. A tainted
    /// attempt (a secret was filled, which the app may show anywhere on screen) is denied first; then a capture the engine
    /// could not make; then the proof of masking: every secure node the tree lists must be covered by a masked region, or
    /// the engine masked less than it saw and the image cannot be proven redacted. The tree still goes out. The image is
    /// sized for the model here, once, not per capture.
    /// </summary>
    public static PixelsOutcome PixelsForModel(Observation observation, EngineScreenshot? screenshot, bool tainted)
    {
        if (tainted)
        {
            return new PixelsOutcome(null, VisionDegradation.PixelTainted);
        }

        if (screenshot is null)
        {
            return new PixelsOutcome(null, VisionDegradation.UnsupportedCapability);
        }

        var secure = LocatorResolver.Walk(observation.Roots).Count(node => node.States.Secure);
        return screenshot.MaskedRegionCount is not { } masked || masked < secure
            ? new PixelsOutcome(null, VisionDegradation.MaskingUnproven)
            : new PixelsOutcome(Pixels.SizeForModel(screenshot), null);
    }

    /// <summary>
    /// Upstream's <c>projectTree</c>: the nodes the screen lists as an executor reads them. Every string field is
    /// redacted, a secure node carries no value and no selection at all, and a link target is bounded as a line bounds it:
    /// a target on <paramref name="appOrigin"/> keeps that origin and loses no more of its path than the line does. A node
    /// the screen hides is left out and its children take its place, as the text leaves it out.
    /// </summary>
    public static IReadOnlyList<ExecutorNode> ProjectTree(IReadOnlyList<SemanticNode> nodes, Redactor redactor, string? appOrigin)
    {
        var projected = new List<ExecutorNode>();
        foreach (var node in nodes)
        {
            var children = ProjectTree(node.Children, redactor, appOrigin);
            if (node.States.Hidden || node.States.AriaHidden)
            {
                projected.AddRange(children);
                continue;
            }

            projected.Add(new ExecutorNode
            {
                Id = node.Ref,
                Role = node.Role,
                Name = node.Name is null ? null : RedactText(node.Name, redactor, ObservationLimits.Name),
                Text = node.Text is null ? null : RedactText(node.Text, redactor, ObservationLimits.Text),
                Value = node.Value is null || node.States.Secure ? null : RedactText(node.Value, redactor, ObservationLimits.Text),
                Selection = node.States.Secure ? null : SnapshotText.Selection(node, redactor),
                InputPurpose = node.InputPurpose,
                States = node.States,
                Attributes = node.Attributes.Count == 0
                    ? null
                    : node.Attributes.ToDictionary(pair => pair.Key, pair => string.Equals(pair.Key, "href", StringComparison.Ordinal) ? TreeHref(pair.Value, redactor, appOrigin) : RedactText(pair.Value, redactor), StringComparer.Ordinal),
                Rect = node.Rect,
                Children = children,
            });
        }

        return projected;
    }

    // A link target as an executor tree carries it: absolute, and bounded exactly as the line bounds it.
    private static string TreeHref(string href, Redactor redactor, string? appOrigin) =>
        appOrigin is not null && RedactText(href, redactor).StartsWith(appOrigin + "/", StringComparison.Ordinal)
            ? appOrigin + SnapshotText.Href(href, redactor, appOrigin)
            : SnapshotText.Href(href, redactor, null);

    /// <summary>FNV-1a over the image bytes: fast, and equal frames encode to equal bytes.</summary>
    public static string PixelDigest(ReadOnlySpan<byte> bytes)
    {
        var hash = 0x811c9dc5u;
        foreach (var value in bytes)
        {
            hash = (hash ^ value) * 0x01000193u;
        }

        return hash.ToString("x", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Whether an observation shows a screen in transition: no node, or only an empty document root.</summary>
    public static bool IsTransitional(Observation observation)
    {
        var visible = observation.Roots.Where(root => !(root.States.Hidden || root.States.AriaHidden)).ToList();
        return visible.Count == 0
            || (visible.Count == 1 && visible[0].Role is { } role && EmptyRoots.Contains(role) && visible[0].Children.All(child => child.States.Hidden || child.States.AriaHidden));
    }

    /// <summary>
    /// Captures until the screen shape holds still, bounded by a short ceiling and the step's cancellation. An
    /// observation taken right after an action can be a snapshot the app is still reacting to, and acting or judging
    /// on it repeats actions and passes steps on pre-render state.
    /// <para>
    /// With <see cref="SettleOptions{T}.ChangedFrom"/>, the loop has two phases: first it waits for the shape to leave
    /// the pre-action one (a navigation committing, a route swapping the body, a submit rendering), then it waits for
    /// the new shape to hold still. A screen that never leaves the pre-action shape is returned as it is once the
    /// change wait runs out: the caller reports it unchanged rather than guessing. Upstream also returns at once on a
    /// capture with no comparable shape, a screenshot alone; the port's observations always have one.
    /// </para>
    /// </summary>
    public static async Task<T> SettleAsync<T>(
        Func<CancellationToken, Task<T>> capture,
        Func<T, string> shapeOf,
        SettleOptions<T> options,
        TimeProvider time,
        CancellationToken token)
    {
        var poll = options.Poll ?? SettlePoll;
        var transitional = options.Transitional ?? (_ => false);
        var value = await capture(token).ConfigureAwait(false);
        var shape = shapeOf(value);
        if (options.ChangedFrom is { } changedFrom)
        {
            while ((string.Equals(shape, changedFrom.Shape, StringComparison.Ordinal) || transitional(value))
                && time.GetUtcNow() < changedFrom.Deadline)
            {
                await Task.Delay(poll, time, token).ConfigureAwait(false);
                value = await capture(token).ConfigureAwait(false);
                shape = shapeOf(value);
            }
        }

        var deadline = time.GetUtcNow() + options.StableWait;
        while (time.GetUtcNow() < deadline)
        {
            await Task.Delay(poll, time, token).ConfigureAwait(false);
            value = await capture(token).ConfigureAwait(false);
            var next = shapeOf(value);
            var stable = string.Equals(next, shape, StringComparison.Ordinal);
            shape = next;
            if (stable && (options.ChangedFrom is null || !transitional(value)))
            {
                break;
            }
        }

        return value;
    }
}
