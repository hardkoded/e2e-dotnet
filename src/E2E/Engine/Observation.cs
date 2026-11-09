// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>Limits from the engine contract. Engines cut names and text before they reach the model.</summary>
public static class ObservationLimits
{
    public const int Name = 256;
    public const int Text = 512;

    /// <summary>Most nodes one web observation lists, across every frame of the page.</summary>
    public const int Nodes = 3000;
}

/// <summary>Boolean states a platform can report for one node.</summary>
public sealed class NodeStates
{
    public bool Checked { get; init { field = value; CheckedReported = true; } }

    /// <summary>
    /// The platform reported a checked state for the node, true or false. Setting <see cref="Checked"/> reports it; a node with none
    /// (a button, a heading) leaves it false, and <c>IsChecked</c> fails on it unless its role carries a checked state. List it after
    /// <see cref="Checked"/> in an initializer to override that (.NET-only; upstream reads the state as nullable).
    /// </summary>
    public bool CheckedReported { get; init; }

    public bool Disabled { get; init; }

    public bool Expanded { get; init; }

    /// <summary>The node has no <c>aria-expanded</c> of true or false. A role query for <c>Expanded = false</c> skips it. Web only (.NET-only; upstream reads the state as nullable).</summary>
    public bool ExpandedAbsent { get; init; }

    /// <summary>The node is a checkbox or a button in the mixed state. A role query for <c>Checked</c> or <c>Pressed</c> of either value skips it. Web only (.NET-only; upstream reads the state as nullable).</summary>
    public bool Mixed { get; init; }

    /// <summary>The node renders a box with no width or no height. It is not visible, though role queries and the agent still read it (.NET-only; upstream puts this in its <c>hidden</c> state). Web only.</summary>
    public bool EmptyBox { get; init; }

    public bool Focused { get; init; }

    /// <summary>The node does not render: <c>display: none</c>, <c>visibility</c>, skipped content, a closed <c>details</c>. This is what <c>isVisible</c> reads.</summary>
    public bool Hidden { get; init; }

    /// <summary>The node, or an ancestor, is <c>aria-hidden</c>. It may still paint, so it can be visible, but role queries and the agent skip it.</summary>
    public bool AriaHidden { get; init; }

    public bool Secure { get; init; }

    public bool Selected { get; init; }

    public bool Pressed { get; init; }
}

/// <summary>A viewport-relative rectangle in CSS pixels.</summary>
public sealed record BoundingBox(double X, double Y, double Width, double Height);

/// <summary>Upstream's <c>TraceViewport</c>: the size of the viewport a capture was taken in, in CSS pixels.</summary>
public sealed record TraceViewport(int Width, int Height);

/// <summary>One node of an observation tree. <see cref="Ref"/> is valid only for the observation it came from.</summary>
public sealed class SemanticNode
{
    public required string Ref { get; init; }

    public string? Role { get; init; }

    /// <summary>The accessible name, by the accname rules Playwright follows, or null when the node has none.</summary>
    public string? Name { get; init; }

    /// <summary>The rendered text, which locator reads and text queries use.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// The text the node owns, read as a line: its inline words stay in place, but its listed children's text is left out. The snapshot shows it.
    /// Null when the node has no split: <see cref="Text"/> then reads whole.
    /// </summary>
    public string? OwnText { get; init; }

    /// <summary>
    /// The inline elements read in <see cref="Text"/> that have no node of their own, such as the <c>&lt;span&gt;</c> in
    /// <c>Ticket A &lt;span&gt;urgent&lt;/span&gt;</c>. A text query resolves them under this node, so it still finds the
    /// word, as Playwright's text engine does. They are not in <see cref="Children"/>, so the agent does not read them twice.
    /// </summary>
    internal IReadOnlyList<SemanticNode> InlineNodes { get; init; } = [];

    /// <summary>
    /// The text of the DOM as Playwright's text engine reads it: every descendant's text in place, with no separator between blocks.
    /// Null on a surface with no such text. A web query by text or <c>hasText</c> matches it, in place of <see cref="Name"/> and
    /// <see cref="Text"/>, which read the rendered text and the accessible name.
    /// </summary>
    internal string? DomText { get; init; }

    /// <summary>
    /// What a label query finds the node by on the web: its <c>aria-label</c>, the elements its <c>aria-labelledby</c> names, and its
    /// <c>&lt;label&gt;</c> elements (upstream's <c>labels</c> of a raw node). Null on a surface whose label is its <see cref="Name"/>.
    /// </summary>
    internal IReadOnlyList<string>? Labels { get; init; }

    public string? Value { get; init; }

    /// <summary>The text selected inside the focused field or editing host. Null for a caret, an unfocused field, or a secure one.</summary>
    public string? Selection { get; init; }

    public string? TestId { get; init; }

    public string? Placeholder { get; init; }

    /// <summary>What a field takes: <c>username</c>, <c>password</c>, <c>one-time-code</c>, <c>generic-secret</c>, or <c>none</c>.</summary>
    public string? InputPurpose { get; init; }

    public int? Level { get; init; }

    public NodeStates States { get; init; } = new();

    /// <summary>
    /// Platform attributes by name, what <c>getAttribute</c> and <c>toHaveAttribute</c> read.
    /// A secure field never reports its value.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The whole <c>href</c> attribute. <see cref="Attributes"/> carries it reduced to origin and path, as upstream's tree does,
    /// but upstream reads <c>getAttribute</c> from a separate node read that keeps the whole value; here that read comes from this tree.
    /// </summary>
    internal string? WholeHref { get; init; }

    /// <summary>The attribute that carries <see cref="TestId"/>. The tree reports the id as a field, so <see cref="Attributes"/> leaves it out.</summary>
    internal string? TestIdAttribute { get; init; }

    /// <summary>Reads one attribute, the test-id attribute included.</summary>
    internal string? AttributeOf(string name) =>
        name == "href" && WholeHref is not null ? WholeHref : Attributes.TryGetValue(name, out var value) ? value : name == TestIdAttribute ? TestId : null;

    /// <summary>Whether the node is a frame element, whose children are its own document's nodes. A query under another node stops at it.</summary>
    internal bool Frame { get; init; }

    /// <summary>The node's box in viewport CSS pixels, when the platform measures one.</summary>
    public BoundingBox? Rect { get; init; }

    /// <summary>
    /// The point an offset into the node is measured from, in <see cref="Rect"/>'s space, when it is not <see cref="Rect"/>'s top-left corner: a
    /// document element's padding box, inside its border, or the corner of a box a frame clipped <see cref="Rect"/> from.
    /// <c>tap({ position })</c> adds the offset to it.
    /// </summary>
    public Point? Origin { get; init; }

    /// <summary>
    /// Platform selector that addresses this node within its own document, when the platform has one. It is structural, not semantic: it
    /// survives the content changes that rename a node, and a runner may store it to re-find the node cheaply on a later run. It is never
    /// part of the node's identity, so whatever it resolves to is still checked before it is used. It should be anchored on an attribute
    /// naming the node or one of its ancestors, and absent rather than positional all the way to the document root.
    /// </summary>
    public string? Selector { get; init; }

    /// <summary>The enclosing nested-document chain as selectors of each boundary node, outermost first. Null for a node in the main document.</summary>
    public IReadOnlyList<string>? FramePath { get; init; }

    public IReadOnlyList<SemanticNode> Children { get; init; } = [];
}

/// <summary>A redacted semantic snapshot of the current screen.</summary>
public sealed class Observation
{
    public required string Route { get; init; }

    /// <summary>
    /// Where the surface was when captured (upstream's <c>location</c>): the page's address on the web. Null on a platform with no
    /// location. <see cref="Route"/> is the same address without its origin (.NET-only; upstream derives that path from this).
    /// </summary>
    public string? Location { get; init; }

    public required IReadOnlyList<SemanticNode> Roots { get; init; }

    /// <summary>True when the engine left nodes out to stay within its node budget. Scrolling brings them in.</summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Whether an on-screen keyboard is showing, as the engine measured it apart from the tree: false when it measured none,
    /// null only when it did not measure it.
    /// </summary>
    public bool? KeyboardVisible { get; init; }

    /// <summary>
    /// Where the engine's viewport and scrolled elements stand, when it can tell. Paging
    /// compares it to see whether a scroll moved anything the tree does not show.
    /// </summary>
    internal string? ScrollPosition { get; init; }

    /// <summary>
    /// Upstream's <c>ObservedScreen.viewport</c>: the viewport the capture was taken in. Null when the engine did not measure it: the
    /// web engine does, and a scroll on a list then records the share of it the list covered.
    /// </summary>
    public TraceViewport? Viewport { get; init; }

    /// <summary>
    /// Which capture of the attempt this is (upstream's <c>revision</c>: <c>b1</c>, <c>b2</c>, and so on). The harness stamps it, not the
    /// engine, as the observation reaches the attempt's agent; it reads empty before then. A step's report names the revision of the
    /// screen it last read.
    /// </summary>
    public string Revision { get; internal set; } = "";

    /// <summary>
    /// The screen's pixels, with every secure field covered, when <see cref="ObserveOptions.Pixels"/> asked for them and the engine could
    /// take them (upstream's <c>EngineSnapshot.pixels</c> and <c>maskedRegionCount</c>, as <see cref="EngineScreenshot"/>). Null when it did
    /// not ask, or when the image could not be taken or proven redacted: the observation is then the tree alone.
    /// </summary>
    public EngineScreenshot? Pixels { get; init; }

    /// <summary>
    /// The engine read no semantic tree, and <see cref="Pixels"/> are the only evidence of the screen (upstream's <c>kind: 'pixels'</c>
    /// observation, which an engine answers when <see cref="ObserveOptions.PixelFallback"/> allows it). <see cref="Roots"/> is empty then,
    /// the node refs of earlier observations name nothing, and a step that saw such a screen records no replay.
    /// </summary>
    public bool TreeUnavailable { get; init; }

    /// <summary>
    /// How many secure nodes the engine saw (upstream's <c>redaction.secureNodeCount</c>), which <see cref="EngineScreenshot.MaskedRegionCount"/> of
    /// <see cref="Pixels"/> must cover for the image to be proven redacted. A pixel-only observation (<see cref="TreeUnavailable"/>) has no
    /// tree to count, so the engine reports it. Null when the engine does not: the secure nodes of <see cref="Roots"/> are counted instead.
    /// </summary>
    public int? SecureNodeCount { get; init; }
}

/// <summary>What an observation takes besides the tree. Upstream's <c>EngineObserveOptions</c>.</summary>
public sealed record ObserveOptions
{
    /// <summary>Take the screen's pixels with the tree (<see cref="Observation.Pixels"/>).</summary>
    public bool Pixels { get; init; }

    /// <summary>Take the pixels in a state fit for comparing against a stored one: the status bar normalized.</summary>
    public bool Comparable { get; init; }

    /// <summary>
    /// Let the engine answer a screen whose semantic tree it could not read with a pixel-only observation
    /// (<see cref="Observation.TreeUnavailable"/>) instead of an error. The agent asks; a locator or an assertion does not.
    /// </summary>
    public bool PixelFallback { get; init; }
}
