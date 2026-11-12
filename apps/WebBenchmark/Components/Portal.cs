// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components;

namespace WebBenchmark.Components;

/// <summary>
/// Marks a scenario component that renders through <see cref="Portal"/>: <c>App.razor</c> mounts <see cref="PortalHost"/> only on its
/// page (.NET-only).
/// </summary>
public interface IUsesPortal;

/// <summary>
/// What a scenario shows through <c>createPortal(..., document.body)</c> upstream (.NET-only: Blazor has no portal, so a scenario hands
/// its content to this circuit-wide service and <see cref="PortalHost"/>, a root component directly under <c>&lt;body&gt;</c>, renders it
/// there, detached from the scenario's own subtree as a React portal is).
/// </summary>
public sealed class Portal
{
    /// <summary>The content to render under <c>&lt;body&gt;</c>, or null for none.</summary>
    public RenderFragment? Content { get; private set; }

    /// <summary>Raised when <see cref="Content"/> is replaced or must be rendered again.</summary>
    public event Action? Changed;

    /// <summary>Replaces <see cref="Content"/> and asks the host to render it.</summary>
    public void Show(RenderFragment? content)
    {
        Content = content;
        Changed?.Invoke();
    }

    /// <summary>Asks the host to render <see cref="Content"/> again, after the scenario's state it reads has changed.</summary>
    public void Refresh() => Changed?.Invoke();
}
