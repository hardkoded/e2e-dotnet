// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// What evidence a judgment (<see cref="Agent.AssertAsync"/>, <see cref="Agent.WaitForAsync"/>,
/// <see cref="Agent.ExtractAsync{T}"/>) is given: the semantic tree, a masked screenshot of the current screen, or both.
/// <para>
/// <see cref="Only"/> exists because a tree sent beside pixels is a cheaper path to an answer, and a model takes it:
/// asked whether a form is covered by an overlay, it can read from the tree that the form is present and named and
/// answer yes, while the pixels show the overlay. For a judgment about what the screen presents, the tree is a
/// distractor, so the mode that means it removes it. It also costs fewer input tokens than <see cref="On"/>.
/// </para>
/// <para>
/// In every mode that sends pixels and the tree, pixel evidence degrades away rather than failing the call when it
/// cannot be proven redacted. <see cref="Only"/> has nothing to degrade to, so it fails with <c>POLICY_DENIED</c>
/// instead of answering the wrong question from the tree.
/// </para>
/// <para>
/// <see cref="Agent.ActAsync"/> takes no vision mode: the act model works from the tree.
/// </para>
/// </summary>
public enum VisionMode
{
    /// <summary>The tree. The default.</summary>
    Off,

    /// <summary>The tree and a masked screenshot, on every call.</summary>
    On,

    /// <summary>The masked screenshot, and not the tree.</summary>
    Only,
}
