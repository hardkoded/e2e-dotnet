// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Engine;

/// <summary>
/// Upstream's <c>NodeRef</c> (<c>engine/contract.ts</c>): a node's stable id and the revision of the observation or resolution that minted it.
/// </summary>
public sealed record NodeRef(string Id, string Revision);

/// <summary>Upstream's <c>EngineCleanupContext</c>: the signal and the cleanup budget the harness hands an engine's cleanup hooks.</summary>
public sealed record EngineCleanupContext(CancellationToken Signal, TimeSpan Timeout);

/// <summary>
/// Upstream's <c>EngineAttemptContext</c> (<c>engine/index.ts</c>): what an engine's <c>StartAttempt</c> is given once per attempt, with the
/// callbacks that carry what the engine learns back to the harness. Over the engine protocol each callback is a message to the client.
/// </summary>
public sealed class EngineAttemptContext
{
    /// <summary>The attempt's id. Upstream's <c>attemptId</c>.</summary>
    public required string AttemptId { get; init; }

    /// <summary>The attempt's artifact directory. Upstream's <c>artifactsDir</c>.</summary>
    public required string ArtifactsDir { get; init; }

    /// <summary>Aborts when the attempt is abandoned. Upstream's <c>signal</c>.</summary>
    public CancellationToken Signal { get; init; }

    /// <summary>The plaintext of a secret the engine holds, resolved on the client. Upstream's <c>resolveSecret</c>.</summary>
    public required Func<Secret, ResolveSecretOptions?, Task<string>> ResolveSecret { get; init; }

    /// <summary>Takes one line of what the app did on its own. Upstream's <c>appLog</c>.</summary>
    public required Action<AppLogEntry> AppLog { get; init; }

    /// <summary>Takes one line saying where the app went on its own. Upstream's <c>navigation</c>.</summary>
    public required Action<string> Navigation { get; init; }

    /// <summary>Takes the facts the engine learns about what the attempt runs on. Upstream's <c>environment</c>.</summary>
    public required Action<IReadOnlyDictionary<string, string>> Environment { get; init; }

    /// <summary>Takes the screen an action left; null when the attempt keeps no trace. Upstream's <c>screen</c>.</summary>
    public Action<EngineSnapshot>? Screen { get; init; }
}

/// <summary>
/// Upstream's <c>ObservationPixels</c>: an encoded image of the screen with every secure field covered, and its geometry. Named apart from
/// the port's <see cref="EngineScreenshot"/>, which carries no geometry.
/// </summary>
public sealed record ObservationPixels(byte[] Data, string MediaType, int Width, int Height, double Scale);

/// <summary>Upstream's <c>PixelsWithheld</c> (<c>engine/surface.ts</c>): why pixels a caller asked for are not part of an observation.</summary>
public enum PixelsWithheld
{
    /// <summary>The masked regions are fewer than the secure nodes, so the image cannot be proven redacted (upstream's <c>MASKING_UNPROVEN</c>).</summary>
    MaskingUnproven,
}

/// <summary>
/// How much of a node's box is inside the viewport, after clipping by the viewport and by every ancestor that clips its content (a scroll
/// container, <c>overflow: hidden</c>, a frame), as <see cref="EngineSpec.ViewportIntersection"/> measures it. It says nothing about another
/// node covering the box. Upstream's <c>ViewportIntersection</c> (<c>engine/contract.ts</c>).
/// </summary>
/// <param name="Intersects">Whether a positive area of the box is inside; false for a node with no box or an empty one.</param>
/// <param name="Ratio">
/// The inside area over the whole box's area, from 0 to 1. Null where the platform reports only the visible part of a box, so the whole is
/// unknown.
/// </param>
public sealed record ViewportIntersection(bool Intersects, double? Ratio = null);

/// <summary>
/// Upstream's <c>SemanticNode.states</c>: the boolean states the platform reports. A null flag is absent: the platform does not say.
/// <see cref="Readonly"/> is present only on a node that has a read-only state. <see cref="Empty"/> is whether the node holds nothing, reported on
/// the nodes <c>Locate</c> returns, never in an observation, and never on a secure field.
/// </summary>
public sealed record EngineNodeStates(
    bool? Secure = null,
    bool? Hidden = null,
    bool? Checked = null,
    bool? Disabled = null,
    bool? Selected = null,
    bool? Expanded = null,
    bool? Pressed = null,
    bool? Focused = null,
    bool? Readonly = null,
    bool? Empty = null
);

/// <summary>
/// Upstream's <c>SemanticNode</c> (<c>engine/contract.ts</c>): one node of an engine's tree. Named <c>EngineNode</c> because the port's IEngine
/// already has a <see cref="SemanticNode"/>, which carries the port's own observation fields.
/// </summary>
/// <param name="Ref">Node reference, valid against the observation it came from.</param>
/// <param name="Role">Semantic role.</param>
/// <param name="Name">Accessible name, cut at <see cref="ObservationLimits.Name"/>.</param>
/// <param name="Children">Child nodes.</param>
/// <param name="States">Boolean states the platform reports.</param>
/// <param name="Text">Visible text, cut at <see cref="ObservationLimits.Text"/>.</param>
/// <param name="Value">Current input value; never reported for a secure field.</param>
/// <param name="Selection">Text selected inside the node; absent when the selection is collapsed, and never reported for a secure field.</param>
/// <param name="TestId">The node's test id, when the platform gives it one. The <c>testId</c> query resolves against this field.</param>
/// <param name="InputPurpose">What a field takes: <c>username</c>, <c>password</c>, <c>one-time-code</c>, <c>generic-secret</c>, or <c>none</c>.</param>
/// <param name="Level">Heading level of a heading node; absent elsewhere.</param>
/// <param name="Attributes">Platform attributes, what <c>getAttribute</c> reads. The <c>placeholder</c> query reads <c>placeholder</c> from here.</param>
/// <param name="Rect">The node's box in the top-level viewport's CSS pixels.</param>
/// <param name="Origin">The point an offset into the node is measured from, in <c>Rect</c>'s space, when it is not its top-left corner.</param>
/// <param name="Selector">Platform selector that addresses this node within its own document, when the platform has one.</param>
/// <param name="FramePath">Enclosing nested-document chain as selectors of each boundary node, outermost first.</param>
public sealed record EngineNode(
    NodeRef Ref,
    string? Role = null,
    string? Name = null,
    IReadOnlyList<EngineNode>? Children = null,
    EngineNodeStates? States = null,
    string? Text = null,
    string? Value = null,
    string? Selection = null,
    string? TestId = null,
    string? InputPurpose = null,
    int? Level = null,
    IReadOnlyDictionary<string, string>? Attributes = null,
    BoundingBox? Rect = null,
    Point? Origin = null,
    string? Selector = null,
    IReadOnlyList<string>? FramePath = null
);

/// <summary>
/// Upstream's <c>EngineTabs</c> (<c>engine/index.ts</c>): the tabs a surface has open, as a snapshot reports them.
/// </summary>
/// <param name="Count">How many tabs are open: 2 or more.</param>
/// <param name="Active">The zero-based position, in the order the tabs opened, of the tab the snapshot's root reads.</param>
public sealed record EngineTabs(int Count, int Active)
{
    /// <summary>
    /// Upstream's <c>validTabs</c> (<c>engine/session.ts</c>): true for a tab count an engine reported well, two or more tabs with the active one
    /// among them. A malformed one is dropped, never shown.
    /// </summary>
    internal static bool IsValid(EngineTabs? tabs) => tabs is { Count: >= 2, Active: >= 0 } && tabs.Active < tabs.Count;
}

/// <summary>
/// Upstream's <c>EngineSnapshot</c> (<c>engine/index.ts</c>): what an engine's <c>observe</c> returns. A snapshot with
/// <see cref="TreeUnavailable"/> carries only the root reference and the pixels.
/// </summary>
public sealed record EngineSnapshot(EngineNode Root, ViewportSize Viewport)
{
    /// <summary>Where the surface was when captured, when the platform has a location.</summary>
    public string? Location { get; init; }

    /// <summary>Whether an on-screen keyboard is showing, when the engine measured it.</summary>
    public bool? KeyboardVisible { get; init; }

    /// <summary>
    /// The tabs the surface has open, when it has more than one, such as a browser with a tab the app opened. The harness tells the model which
    /// tab it reads and how many there are, and the agent switches between them through <see cref="EngineSessionSpec.SwitchTab"/> and closes
    /// them through <see cref="EngineSessionSpec.CloseTab"/>. Null with one tab.
    /// </summary>
    public EngineTabs? Tabs { get; init; }

    /// <summary>True when the engine left nodes out of the tree to stay within its budget.</summary>
    public bool Truncated { get; init; }

    /// <summary>The semantic tree could not be read; <see cref="Pixels"/> are the only evidence of the screen.</summary>
    public bool TreeUnavailable { get; init; }

    /// <summary>The screen's pixels, when the engine took them.</summary>
    public ObservationPixels? Pixels { get; init; }

    /// <summary>
    /// How many regions the engine masked over the secure nodes it saw; the harness checks it covers every secure node. An engine asked for
    /// pixels (<see cref="ObserveOptions.Pixels"/>) that cannot mask every secure node on screen omits <see cref="Pixels"/> and still
    /// reports the regions it could mask, fewer than the secure nodes, so the session marks the observation
    /// <see cref="PixelsWithheld.MaskingUnproven"/> instead of a missing screenshot.
    /// </summary>
    public int? MaskedRegionCount { get; init; }
}

/// <summary>
/// Upstream's <c>Observation</c> (<c>engine/surface.ts</c>) as the session returns it: a semantic capture with a <see cref="Tree"/>, or a
/// pixel-only capture with <see cref="Pixels"/> and no tree.
/// </summary>
public sealed record SessionObservation
{
    /// <summary>The capture's kind: <c>semantic</c>, or <c>pixels</c> for a pixel-only capture.</summary>
    public required string Kind { get; init; }

    /// <summary>The viewport action reference, valid for this capture.</summary>
    public required NodeRef Root { get; init; }

    /// <summary>The capture's revision, <c>b1</c>, <c>b2</c>, and so on.</summary>
    public required string Revision { get; init; }

    /// <summary>When the capture was taken, as an ISO timestamp.</summary>
    public required string CapturedAt { get; init; }

    /// <summary>Where the surface was when captured, when the platform has a location.</summary>
    public string? Location { get; init; }

    /// <summary>The open tabs, when more than one is (<see cref="EngineSnapshot.Tabs"/>).</summary>
    public EngineTabs? Tabs { get; init; }

    /// <summary>The viewport the capture was taken in.</summary>
    public required ViewportSize Viewport { get; init; }

    /// <summary>What the engine masked against the secure nodes it saw.</summary>
    public required ObservationRedaction Redaction { get; init; }

    /// <summary>The semantic tree, with every node's ref stamped with this revision; null for a pixel-only capture.</summary>
    public EngineNode? Tree { get; init; }

    /// <summary>True when the tree was cut short to stay within the engine's budget.</summary>
    public bool Truncated { get; init; }

    /// <summary>Whether an on-screen keyboard is showing, when the engine measured it.</summary>
    public bool? KeyboardVisible { get; init; }

    /// <summary>The screenshot, only when its masked regions cover every secure node.</summary>
    public ObservationPixels? Pixels { get; init; }

    /// <summary>
    /// Why pixels the engine answered with are not here: its masked regions (<see cref="ObservationRedaction.MaskedRegionCount"/>) are fewer than the
    /// secure nodes, whether the engine sent the pixels or withheld them itself. Null when nothing was withheld.
    /// </summary>
    public PixelsWithheld? PixelsWithheld { get; init; }
}

/// <summary>Upstream's <c>Observation.redaction</c>: the counts the session judges a capture's pixels by; fewer masked regions than secure nodes withholds the pixels and keeps the tree.</summary>
public sealed record ObservationRedaction(int SecureNodeCount, int MaskedRegionCount);
