// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E;

/// <summary>
/// The screens an attempt hands its trace (upstream's <c>screens</c> of <c>TargetSession</c>, in <c>engine/session.ts</c>): every
/// screen an engine reports after an action, and every observation a test or the agent takes. An attempt that keeps no trace takes
/// none, so nothing is captured for it. The sink is the step recorder's, routed for as long as the attempt owns the session.
/// </summary>
internal sealed class SessionScreens(bool traced)
{
    private volatile Action<Observation>? _sink;

    /// <summary>Whether the attempt keeps a trace, which decides whether screens are taken at all.</summary>
    public bool Traced { get; } = traced;

    /// <summary>Hands one screen to the trace; a sink that throws never fails what the session was doing.</summary>
    public void Push(Observation screen)
    {
        if (!Traced)
        {
            return;
        }

        try
        {
            _sink?.Invoke(screen);
        }
        catch (Exception)
        {
            // The trace is a reader; it never fails an action.
        }
    }

    /// <summary>Routes the screens to <paramref name="sink"/>, or to none.</summary>
    public void Route(Action<Observation>? sink) => _sink = sink;
}
