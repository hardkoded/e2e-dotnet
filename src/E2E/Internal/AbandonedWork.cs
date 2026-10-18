// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace E2E.Internal;

/// <summary>
/// Work a test left running when its phase returned: steps nobody awaited (upstream's <c>internal/abandoned.ts</c>). Such a step is
/// recorded as <c>STEP_NOT_AWAITED</c> already, so what it fails with afterwards is not a fault of its own.
/// </summary>
internal static class AbandonedWork
{
    private static readonly ConditionalWeakTable<Exception, object> Rejections = new();

    /// <summary>Records <paramref name="cause"/> as the rejection of work already failed as not awaited.</summary>
    public static void MarkRejection(Exception cause) => Rejections.TryAdd(cause, cause);

    /// <summary>Whether <paramref name="cause"/> is abandoned work's: already recorded, and reaching the process only because nobody awaited the task.</summary>
    public static bool IsRejection(Exception cause) => Rejections.TryGetValue(cause, out _);
}
