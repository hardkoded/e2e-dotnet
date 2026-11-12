// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>Date and clock-time patterns, as upstream's <c>internal/dates.ts</c>.</summary>
/// <remarks>
/// ECMAScript semantics, as upstream's regular expressions have: <c>\b</c>, <c>\d</c> and <c>\s</c> see ASCII only.
/// </remarks>
internal static class Dates
{
    private const RegexOptions Ecma = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;

    // A month by name or abbreviation, with or without a trailing period.
    private const string Month = @"(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?";

    // A day of the month (1-31), with an optional English ordinal suffix (st, nd, rd, th).
    private const string Day = @"\d{1,2}(?:st|nd|rd|th)?";

    /// <summary>An ISO 8601 calendar date (<c>YYYY-MM-DD</c>).</summary>
    public static readonly Regex IsoDate = new(@"\b\d{4}-\d{2}-\d{2}\b", Ecma);

    /// <summary>A clock time (<c>HH:MM</c> or <c>HH:MM:SS</c>).</summary>
    public static readonly Regex ClockTime = new(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", Ecma);

    /// <summary>A month followed by day with optional year (e.g. <c>October 26th</c>, <c>Sep 8, 2026</c>).</summary>
    public static readonly Regex MonthDayDate = new(@"\b" + Month + @"\s+" + Day + @"(?:,?\s+\d{4})?\b", Ecma | RegexOptions.IgnoreCase);

    /// <summary>A day followed by month with optional year (e.g. <c>3rd October</c>, <c>26th Oct 2026</c>).</summary>
    public static readonly Regex DayMonthDate = new(@"\b" + Day + @"\s+" + Month + @"(?:,?\s+\d{4})?\b", Ecma | RegexOptions.IgnoreCase);
}
