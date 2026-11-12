// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace E2E.Internal;

/// <summary>
/// Work a test left running when its phase returned: steps nobody awaited (upstream's <c>internal/abandoned.ts</c>). Such a step is
/// recorded as <c>STEP_NOT_AWAITED</c> already, so what it fails with afterwards is not a fault of its own. Also work a reporter's
/// <c>OnRunFinishedAsync</c> left running once its timeout passed or the run was forced to stop.
/// </summary>
internal static class AbandonedWork
{
    private static readonly ConditionalWeakTable<Exception, object> Rejections = new();
    private static volatile bool _workLeftBehind;

    /// <summary>
    /// Whether the process stopped waiting for work that may still hold it open: a reporter that was given up on. Upstream's <c>leftWorkBehind</c>;
    /// a host that must exit once its output is out reads it instead of waiting for that work to settle.
    /// </summary>
    public static bool LeftWorkBehind => _workLeftBehind;

    /// <summary>Records that the process stopped waiting for work that may ignore its token. Upstream's <c>leaveWorkBehind</c>.</summary>
    public static void LeaveWorkBehind() => _workLeftBehind = true;

    /// <summary>.NET-only: clears the flag, which upstream leaves set for the rest of the process. The tests of one process share it, so each starts from false.</summary>
    internal static void Reset() => _workLeftBehind = false;

    /// <summary>Records <paramref name="cause"/> as the rejection of work already failed as not awaited.</summary>
    public static void MarkRejection(Exception cause) => Rejections.TryAdd(cause, cause);

    /// <summary>Whether <paramref name="cause"/> is abandoned work's: already recorded, and reaching the process only because nobody awaited the task.</summary>
    public static bool IsRejection(Exception cause) => Rejections.TryGetValue(cause, out _);
}
