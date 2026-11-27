// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Desktop.MacOs;

/// <summary>
/// A point on the monotonic clock, as Rust's <c>std::time::Instant</c>: every deadline and budget of the driver is counted on it.
/// .NET-only helper: Rust has the type in its standard library.
/// </summary>
internal readonly record struct Instant(long Ticks) : IComparable<Instant>
{
    public static Instant Now =>
        new((long)(Stopwatch.GetTimestamp() * (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency)));

    public static Instant operator +(Instant instant, TimeSpan span) => new(instant.Ticks + span.Ticks);

    public static bool operator <(Instant left, Instant right) => left.Ticks < right.Ticks;

    public static bool operator >(Instant left, Instant right) => left.Ticks > right.Ticks;

    public static bool operator <=(Instant left, Instant right) => left.Ticks <= right.Ticks;

    public static bool operator >=(Instant left, Instant right) => left.Ticks >= right.Ticks;

    public static Instant Min(Instant left, Instant right) => left.Ticks <= right.Ticks ? left : right;

    /// <summary>The instant a duration from now.</summary>
    public static Instant After(TimeSpan span) => Now + span;

    /// <summary>The time since this instant.</summary>
    public TimeSpan Elapsed => TimeSpan.FromTicks(Math.Max(0, Now.Ticks - Ticks));

    /// <summary>The time from now until this instant, zero when it has passed (<c>saturating_duration_since</c>).</summary>
    public TimeSpan Remaining => TimeSpan.FromTicks(Math.Max(0, Ticks - Now.Ticks));

    /// <summary>This instant moved back by <paramref name="span"/>, or null when that is before the clock's origin (<c>checked_sub</c>).</summary>
    public Instant? CheckedSub(TimeSpan span) => Ticks - span.Ticks < 0 ? null : new Instant(Ticks - span.Ticks);

    public int CompareTo(Instant other) => Ticks.CompareTo(other.Ticks);
}
