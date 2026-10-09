// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>When <see cref="Browser.GotoAsync"/> counts a navigation as done.</summary>
public enum GotoWaitUntil
{
    /// <summary>The <c>load</c> event fired. The default.</summary>
    Load,

    /// <summary>The <c>DOMContentLoaded</c> event fired.</summary>
    DomContentLoaded,

    /// <summary>The network was idle for at least 500 ms.</summary>
    NetworkIdle,
}

/// <summary>Options of <see cref="Browser.GotoAsync"/>.</summary>
public sealed record GotoOptions
{
    /// <summary>When the navigation counts as done. Defaults to <see cref="GotoWaitUntil.Load"/>.</summary>
    public GotoWaitUntil WaitUntil { get; init; } = GotoWaitUntil.Load;

    /// <summary>The navigation budget. Defaults to the action timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}
