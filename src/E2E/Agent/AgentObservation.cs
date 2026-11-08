// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using E2E.Cache;
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

    /// <summary>
    /// Whether a capture is already the settled screen, so the stability proof is not spent on it: asked once, of the
    /// capture past the change wait. The caller owns the evidence (a scroll to a text compares the page with the one
    /// before it); a capture it declines gets the stability check.
    /// </summary>
    public Func<T, bool>? SettledWhen { get; init; }

    public TimeSpan? Poll { get; init; }
}

/// <summary>Whether requested pixels became model input: the image sized for the model, or why they did not.</summary>
internal readonly record struct PixelsOutcome(EngineScreenshot? Pixels, VisionDegradation? Withheld);

/// <summary>Upstream's <c>kind</c> of an <c>AgentObservation</c>: a semantic tree, or pixels alone because no tree could be read.</summary>
internal enum PreparedKind
{
    Semantic,
    Pixels,
}

/// <summary>What <see cref="AgentObservation.PrepareObservation"/> needs besides the observation (upstream's options object).</summary>
internal sealed class PrepareObservationOptions
{
    /// <summary>What takes the secrets out of every string; the engine cuts a name or text at its limit, which the redactor accounts for.</summary>
    public required Redactor Redactor { get; init; }

    /// <summary>Most bytes the text may take, the truncation marker included.</summary>
    public required int MaxBytes { get; init; }

    /// <summary>Grants pixel-only evidence; false when this consumer cannot use it.</summary>
    public bool PixelsAllowed { get; init; }

    /// <summary>The app base URL's origin: link targets on it render as paths, which navigation resolves against it.</summary>
    public string? AppOrigin { get; init; }
}

/// <summary>
/// One raw observation as the model and the agent read it (upstream's <c>AgentObservation</c>, a type this port names for what it
/// is, since <see cref="AgentObservation"/> is the static class of the observation functions): redacted, size-bounded, and indexed.
/// </summary>
internal sealed record PreparedObservation
{
    public required PreparedKind Kind { get; init; }

    /// <summary>The route of the screen, redacted.</summary>
    public required string Path { get; init; }

    public required string Revision { get; init; }

    /// <summary>Redacted, size-bounded serialization sent to the model.</summary>
    public required string Text { get; init; }

    /// <summary>The UTF-8 size of <see cref="Text"/>.</summary>
    public required int Bytes { get; init; }

    public TraceViewport? Viewport { get; init; }

    /// <summary>Every redacted node by ref. Empty for pixels alone.</summary>
    public IReadOnlyDictionary<string, SemanticNode> Nodes { get; init; } = new Dictionary<string, SemanticNode>();

    /// <summary>The ref of each node's parent. Empty for pixels alone.</summary>
    public IReadOnlyDictionary<string, string> Parents { get; init; } = new Dictionary<string, string>();

    /// <summary>The redacted roots (upstream has one <c>tree</c> root; the port's observation lists roots).</summary>
    public IReadOnlyList<SemanticNode> Tree { get; init; } = [];

    /// <summary>Whether the byte budget or the engine cut the listing.</summary>
    public bool Truncated { get; init; }

    public bool? KeyboardVisible { get; init; }

    /// <summary>The screenshot sized for the model, when it is permitted and its masking is proven.</summary>
    public EngineScreenshot? Pixels { get; init; }

    /// <summary>Why pixels the observation carried are not part of it (upstream's <c>pixelsWithheld</c>).</summary>
    public VisionDegradation? PixelsWithheld { get; init; }
}

/// <summary>An observation as the settle loop reads it: its shape, whether it is a screen in transition, and the loop itself.</summary>
internal static partial class AgentObservation
{
    /// <summary>Poll interval for shape-stability settling.</summary>
    private static readonly TimeSpan SettlePoll = TimeSpan.FromMilliseconds(75);

    private static readonly Regex Reference = new(@"^( *)#\S+ ", RegexOptions.CultureInvariant | RegexOptions.Multiline);

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
    public static string Shape(Observation observation) =>
        Shape(observation.TreeUnavailable ? "" : SnapshotText.Render(observation, Redactor.None)) + "\n" + observation.ScrollPosition
        + (observation.Pixels is { } pixels ? "\n<pixels " + PixelDigest(pixels.Png) + ">" : "");

    /// <summary>Roles the model can act on by name: the lines that mark a screen as one the tree can drive at all (upstream's <c>INTERACTIVE_ROLES</c>).</summary>
    internal static readonly HashSet<string> InteractiveRoles = new(
        ["button", "link", "textbox", "searchbox", "combobox", "listbox", "checkbox", "radio", "switch", "tab", "menuitem", "menuitemcheckbox", "menuitemradio", "option", "slider", "spinbutton", "treeitem"],
        StringComparer.Ordinal);

    /// <summary>
    /// How many listed nodes the model could act on, read off the rendered <c>#id role</c> lines (upstream's <c>interactiveNodeCount</c>). A screen
    /// with none is one the tree cannot describe (a canvas, a game, a native surface with no semantics), and the model needs pixels from the first turn.
    /// </summary>
    public static int InteractiveNodeCount(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
        {
            if (ListedRole().Match(line) is { Success: true } match && InteractiveRoles.Contains(match.Groups[1].Value))
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"^\s*#\S+ (\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex ListedRole();

    /// <summary>The shape of a screen already rendered as text.</summary>
    public static string Shape(string text) => MaskClocks(Focus.Replace(Reference.Replace(text, "$1"), ""));

    private const string ClockMask = "<time>";

    /// <summary>
    /// The text with its clock-like values masked, so two looks at a screen that has not moved read the same however the
    /// clock ticked. <see cref="Shape(Observation)"/> and <see cref="Reading.ReadPage"/> both compare screens through it.
    /// </summary>
    public static string MaskClocks(string text) => Clock.Replace(text, ClockMask);

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
    /// Whether requested pixels become model input, decided once per observation for every call that asks. A capture the engine
    /// could not make is denied first; then the proof of masking: every secure node the engine reports (<see cref="Observation.SecureNodeCount"/>, else the tree lists) must be covered by a masked region, or
    /// the engine masked less than it saw and the image cannot be proven redacted. The tree still goes out. The image is
    /// sized for the model here, once, not per capture.
    /// </summary>
    public static PixelsOutcome PixelsForModel(Observation observation, EngineScreenshot? screenshot)
    {
        if (screenshot is null)
        {
            return new PixelsOutcome(null, VisionDegradation.UnsupportedCapability);
        }

        // The larger of the engine's count and the tree's, so a reported count never weakens the proof (.NET-only; upstream trusts the engine's count, and a missing count is zero).
        var secure = Math.Max(observation.SecureNodeCount ?? 0, LocatorResolver.Walk(observation.Roots).Count(node => node.States.Secure));
        return screenshot.MaskedRegionCount is not { } masked || masked < secure
            ? new PixelsOutcome(null, VisionDegradation.MaskingUnproven)
            : new PixelsOutcome(Pixels.SizeForModel(screenshot), null);
    }

    /// <summary>
    /// Turns one raw engine observation into redacted model input (upstream's <c>prepareObservation</c>). The text is the screen
    /// as the model reads it, cut at <see cref="PrepareObservationOptions.MaxBytes"/>; the node index and the tree hold every
    /// node with every string field redacted (<see cref="RedactNode"/>). A screen with no tree is answered with pixels alone, and
    /// only when they are permitted and proven masked (<see cref="PixelsForModel"/>). Pixels the masking of which cannot be
    /// proven are withheld from a semantic observation, and its tree still goes out.
    /// </summary>
    public static PreparedObservation PrepareObservation(Observation observation, PrepareObservationOptions options)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(options);
        var path = options.Redactor.Redact(observation.Route);
        var pixels = observation.Pixels is null ? new PixelsOutcome(null, null) : PixelsForModel(observation, observation.Pixels);
        if (observation.TreeUnavailable)
        {
            if (!options.PixelsAllowed || pixels.Pixels is null)
            {
                throw new TestException("UNSUPPORTED_CAPABILITY", "semantic capture is unavailable and no permitted, proven-masked screenshot can replace it");
            }

            return new PreparedObservation
            {
                Kind = PreparedKind.Pixels,
                Path = path,
                Revision = observation.Revision,
                Text = ObservationFeed.UnavailableTreeMarker,
                Bytes = Encoding.UTF8.GetByteCount(ObservationFeed.UnavailableTreeMarker),
                Viewport = observation.Viewport,
                Pixels = pixels.Pixels,
            };
        }

        var tree = observation.Roots.Select(root => RedactNode(root, options.Redactor)).ToList();
        var nodes = new Dictionary<string, SemanticNode>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in tree)
        {
            IndexNodes(root, nodes, parents);
        }

        var text = SnapshotText.Render(observation, options.Redactor, options.AppOrigin, options.MaxBytes, out var cut);
        return new PreparedObservation
        {
            Kind = PreparedKind.Semantic,
            Path = path,
            Revision = observation.Revision,
            Text = text,
            Bytes = Encoding.UTF8.GetByteCount(text),
            Viewport = observation.Viewport,
            Nodes = nodes,
            Parents = parents,
            Tree = tree,
            Truncated = cut || observation.Truncated,
            KeyboardVisible = observation.KeyboardVisible,
            Pixels = pixels.Pixels,
            PixelsWithheld = pixels.Withheld,
        };
    }

    private static void IndexNodes(SemanticNode node, Dictionary<string, SemanticNode> into, Dictionary<string, string> parents)
    {
        into[node.Ref] = node;
        foreach (var child in node.Children)
        {
            parents[child.Ref] = node.Ref;
            IndexNodes(child, into, parents);
        }
    }

    /// <summary>
    /// The node and its subtree with every secret taken out of every string field, before any consumer reads it: the model's text,
    /// the executor's tree, cache descriptors and anchors, failure evidence (upstream's <c>redactNode</c>). A secure node keeps no
    /// value or selection, and a selection that may show part of a secret is dropped, as a secure field's is.
    /// </summary>
    public static SemanticNode RedactNode(SemanticNode node, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(node);
        return RedactFields(node, redactor, node.Children.Select(child => RedactNode(child, redactor)).ToList());
    }

    // The fields of one node, each redacted the way it is shown, linked to the given children. The ref and the closed-set fields are the
    // engine's own and pass as they are; a field the port reads that upstream has no counterpart for (own text, labels, the whole href) is redacted too.
    private static SemanticNode RedactFields(SemanticNode node, Redactor redactor, IReadOnlyList<SemanticNode> children)
    {
        string? Text(string? value, int? limit = null) => value is null ? null : RedactText(value, redactor, limit);
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in node.Attributes)
        {
            attributes[RedactText(key, redactor)] = RedactText(value, redactor);
        }

        return new SemanticNode
        {
            Ref = node.Ref,
            Role = Text(node.Role),
            Name = Text(node.Name, ObservationLimits.Name),
            Text = Text(node.Text, ObservationLimits.Text),
            OwnText = Text(node.OwnText, ObservationLimits.Text),
            InlineNodes = node.InlineNodes.Select(inline => RedactNode(inline, redactor)).ToList(),
            DomText = Text(node.DomText),
            Labels = node.Labels?.Select(label => RedactText(label, redactor)).ToList(),
            Value = node.States.Secure ? null : Text(node.Value, ObservationLimits.Text),
            Selection = SnapshotText.Selection(node, redactor),
            TestId = Text(node.TestId),
            Placeholder = Text(node.Placeholder),
            InputPurpose = node.InputPurpose,
            Level = node.Level,
            States = node.States,
            Attributes = attributes,
            WholeHref = Text(node.WholeHref),
            TestIdAttribute = Text(node.TestIdAttribute),
            Frame = node.Frame,
            Rect = node.Rect,
            Origin = node.Origin,
            Children = children,
        };
    }

    /// <summary>
    /// An earlier capture's nodes redacted again through the redactor as it is now, each on its own fields, then linked to its
    /// redacted-again children, so the tree keeps its shape (which nodes are leaves) without a subtree redacted once per ancestor. A
    /// secret a provider resolved after the capture masks text the capture still shows, so the two captures read alike wherever the
    /// screen did not change (upstream's <c>redactNodesAgain</c>).
    /// </summary>
    public static IReadOnlyDictionary<string, SemanticNode> RedactNodesAgain(IReadOnlyDictionary<string, SemanticNode> nodes, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var again = new Dictionary<string, SemanticNode>(StringComparer.Ordinal);
        SemanticNode Redo(string reference)
        {
            if (again.TryGetValue(reference, out var done))
            {
                return done;
            }

            var node = nodes[reference];
            var children = node.Children.Where(child => nodes.ContainsKey(child.Ref)).Select(child => Redo(child.Ref)).ToList();
            return again[reference] = RedactFields(node, redactor, children);
        }

        foreach (var reference in nodes.Keys)
        {
            Redo(reference);
        }

        return again;
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
                Role = node.Role is null ? null : RedactText(node.Role, redactor),
                Name = node.Name is null ? null : RedactText(node.Name, redactor, ObservationLimits.Name),
                Text = node.Text is null ? null : RedactText(node.Text, redactor, ObservationLimits.Text),
                Value = node.Value is null || node.States.Secure ? null : RedactText(node.Value, redactor, ObservationLimits.Text),
                Selection = node.States.Secure ? null : SnapshotText.Selection(node, redactor),
                InputPurpose = node.InputPurpose,
                States = node.States,
                Attributes = node.Attributes.Count == 0 ? null : TreeAttributes(node.Attributes, redactor, appOrigin),
                Rect = node.Rect,
                Children = children,
            });
        }

        return projected;
    }

    // The attributes of an executor node: the key is page text too, so it is redacted like the value (a later key that reads the
    // same replaces an earlier one, as upstream's Object.fromEntries does).
    private static Dictionary<string, string> TreeAttributes(IReadOnlyDictionary<string, string> attributes, Redactor redactor, string? appOrigin)
    {
        var projected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in attributes)
        {
            projected[RedactText(key, redactor)] = string.Equals(key, "href", StringComparison.Ordinal) ? TreeHref(value, redactor, appOrigin) : RedactText(value, redactor);
        }

        return projected;
    }

    // A link target as an executor tree carries it: absolute, and bounded exactly as the line bounds it.
    private static string TreeHref(string href, Redactor redactor, string? appOrigin)
    {
        var redacted = RedactText(href, redactor);
        return appOrigin is null || CacheRoute.AppNavigation(redacted, appOrigin) == redacted
            ? SnapshotText.Href(href, redactor, null)
            : appOrigin + SnapshotText.Bound(CacheRoute.AppNavigation(redacted, appOrigin));
    }

    /// <summary>SHA-256 over the image bytes: equal frames encode to equal bytes, and no 32-bit collision can read two screens as one.</summary>
    public static string PixelDigest(ReadOnlySpan<byte> bytes) => Ids.Sha256Hex(bytes);

    /// <summary>
    /// Whether an observation shows a screen in transition: no node, or only an empty document root, with no screenshot to describe content
    /// absent from the tree. A canvas can have an empty tree while its pixels show an action's effect, and a screen with no tree at all is
    /// pixels alone.
    /// </summary>
    public static bool IsTransitional(Observation observation)
    {
        if (observation.TreeUnavailable || observation.Pixels is not null)
        {
            return false;
        }

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
    /// change wait runs out: the caller reports it unchanged rather than guessing. Evidence with no comparable shape
    /// (<paramref name="shapeOf"/> returns null) is returned at once: another capture cannot establish stability while
    /// semantics are unavailable.
    /// </para>
    /// </summary>
    public static async Task<T> SettleAsync<T>(
        Func<CancellationToken, Task<T>> capture,
        Func<T, string?> shapeOf,
        SettleOptions<T> options,
        TimeProvider time,
        CancellationToken token)
    {
        var poll = options.Poll ?? SettlePoll;
        var transitional = options.Transitional ?? (_ => false);
        var value = await capture(token).ConfigureAwait(false);
        var shape = shapeOf(value);
        if (shape is null)
        {
            return value;
        }

        if (options.ChangedFrom is { } changedFrom)
        {
            while ((string.Equals(shape, changedFrom.Shape, StringComparison.Ordinal) || transitional(value))
                && time.GetUtcNow() < changedFrom.Deadline)
            {
                await Task.Delay(poll, time, token).ConfigureAwait(false);
                value = await capture(token).ConfigureAwait(false);
                shape = shapeOf(value);
                if (shape is null)
                {
                    return value;
                }
            }
        }

        if (options.SettledWhen?.Invoke(value) == true)
        {
            return value;
        }

        var deadline = time.GetUtcNow() + options.StableWait;
        while (time.GetUtcNow() < deadline)
        {
            await Task.Delay(poll, time, token).ConfigureAwait(false);
            value = await capture(token).ConfigureAwait(false);
            var next = shapeOf(value);
            if (next is null)
            {
                return value;
            }

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
