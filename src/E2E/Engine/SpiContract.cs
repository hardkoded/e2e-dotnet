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
/// Upstream's <c>ObservationPixels</c>: an encoded image of the screen with every secure field covered, and its geometry. Named apart from
/// the port's <see cref="EngineScreenshot"/>, which carries no geometry.
/// </summary>
public sealed record ObservationPixels(byte[] Data, string MediaType, int Width, int Height, double Scale);

/// <summary>Upstream's <c>SemanticNode.states</c>: the flags of a node the harness reads.</summary>
public sealed record EngineNodeStates(bool? Secure = null, bool? Hidden = null);

/// <summary>
/// Upstream's <c>SemanticNode</c> (<c>engine/contract.ts</c>): one node of an engine's tree. Named <c>EngineNode</c> because the port's IEngine
/// already has a <see cref="SemanticNode"/>, which carries the port's own observation fields.
/// </summary>
public sealed record EngineNode(NodeRef Ref, string? Role = null, string? Name = null, IReadOnlyList<EngineNode>? Children = null, EngineNodeStates? States = null);

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

    /// <summary>How many regions the engine masked over the secure nodes it saw.</summary>
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

    /// <summary>The screen's pixels, when the capture has them.</summary>
    public ObservationPixels? Pixels { get; init; }
}

/// <summary>Upstream's <c>Observation.redaction</c>: the counts a capture's pixels are judged by.</summary>
public sealed record ObservationRedaction(int SecureNodeCount, int MaskedRegionCount);
