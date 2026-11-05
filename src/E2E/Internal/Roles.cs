// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Internal;

/// <summary>The role sets the runner reads a node against: what takes typed text, what carries a value, what has a checked state, as upstream's <c>internal/roles.ts</c>.</summary>
internal static class Roles
{
    /// <summary>Roles that take typed text: what a secret may fill.</summary>
    private static readonly HashSet<string> EditableRoles = new(["textbox", "searchbox", "combobox"], StringComparer.Ordinal);

    /// <summary>
    /// Roles whose node carries a value: the editable roles plus the controls a platform reports a value for without taking typed text
    /// (a range input, a number stepper, a multiple select and its options).
    /// </summary>
    private static readonly HashSet<string> ValueRoles = new([.. EditableRoles, "spinbutton", "slider", "listbox", "option"], StringComparer.Ordinal);

    /// <summary>Roles with a checked state, the ARIA roles that take <c>aria-checked</c>, as Playwright lists them.</summary>
    private static readonly HashSet<string> CheckedRoles = new(["checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem"], StringComparer.Ordinal);

    /// <summary>True for a node of an editable role.</summary>
    public static bool IsEditable(SemanticNode node) => EditableRoles.Contains(node.Role ?? "");

    /// <summary>
    /// True for a node of a role that carries a value, so a value the platform did not report reads as empty. Any other node has no value
    /// to read.
    /// </summary>
    public static bool IsValueControl(SemanticNode node) => ValueRoles.Contains(node.Role ?? "");

    /// <summary>
    /// True for a node that has a checked state: one the platform reported (checked or mixed), or a role that carries one, since a node of
    /// that role with no state reported is unchecked. A button or a heading has none. A state the platform
    /// reported is <c>CheckedReported</c> (.NET-only; upstream reads <c>states.checked !== undefined</c>).
    /// </summary>
    public static bool IsCheckable(SemanticNode node) => node.States.CheckedReported || node.States.Mixed || CheckedRoles.Contains(node.Role ?? "");
}
