// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>What the in-page reader's callers share (upstream's <c>web/src/read-node.ts</c>; its wire types have no port: the page script returns its own DTOs).</summary>
internal static class ReadNode
{
    /// <summary>
    /// The one definition of a secure field (upstream's <c>SECURE_FIELD_SELECTOR</c>): the fields the page script marks secure. It is a
    /// selector because it is applied from two sides that cannot share code, the in-page reader and the screenshot masker. Upstream's
    /// value is <c>input[type="password" i]</c>; the port's reader also treats <c>autocomplete=current-password</c> as secure.
    /// </summary>
    public const string SecureFieldSelector = "input[type=password i], input[autocomplete=current-password]";
}
