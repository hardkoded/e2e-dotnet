// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PlaywrightClockInstallOptions = Microsoft.Playwright.ClockInstallOptions;

namespace E2E.Engine;

/// <summary>
/// A point in time for <c>Browser.Clock</c> (upstream's <c>ClockTime</c>): epoch milliseconds, a string a
/// <c>Date</c> parses, or a <see cref="DateTimeOffset"/>. A fraction of a millisecond is dropped.
/// </summary>
public readonly struct ClockTime
{
    private readonly double? _number;
    private readonly string? _text;
    private readonly DateTimeOffset? _date;

    private ClockTime(double? number, string? text, DateTimeOffset? date)
    {
        _number = number;
        _text = text;
        _date = date;
    }

    /// <summary>Epoch milliseconds.</summary>
    public static implicit operator ClockTime(long milliseconds) => new(milliseconds, null, null);

    /// <summary>Epoch milliseconds.</summary>
    public static implicit operator ClockTime(double milliseconds) => new(milliseconds, null, null);

    /// <summary>A date string, read as a JavaScript <c>Date</c> reads it: a date alone is UTC, a time without an offset is local.</summary>
    public static implicit operator ClockTime(string text) => new(null, text, null);

    /// <summary>An instant.</summary>
    public static implicit operator ClockTime(DateTimeOffset date) => new(null, null, date);

    /// <summary>An instant; a <see cref="DateTimeKind.Unspecified"/> value is local time.</summary>
    public static implicit operator ClockTime(DateTime date)
    {
        try
        {
            return new(null, null, new DateTimeOffset(date));
        }
        catch (ArgumentOutOfRangeException)
        {
            // A local time whose UTC instant is outside the range: refused later as no time at all.
            return default;
        }
    }

    internal double? Number => _number;

    internal string? Text => _text;

    internal DateTimeOffset? Date => _date;
}

/// <summary>
/// A span of time for <c>Browser.Clock</c> (upstream's <c>ClockTicks</c>): milliseconds, or an <c>'ss'</c>, <c>'mm:ss'</c>, or
/// <c>'hh:mm:ss'</c> string. A fraction of a millisecond is dropped.
/// </summary>
public readonly struct ClockTicks
{
    private readonly double? _number;
    private readonly string? _text;

    private ClockTicks(double? number, string? text)
    {
        _number = number;
        _text = text;
    }

    /// <summary>Milliseconds.</summary>
    public static implicit operator ClockTicks(long milliseconds) => new(milliseconds, null);

    /// <summary>Milliseconds.</summary>
    public static implicit operator ClockTicks(double milliseconds) => new(milliseconds, null);

    /// <summary>An <c>'ss'</c>, <c>'mm:ss'</c>, or <c>'hh:mm:ss'</c> string with every field under 60.</summary>
    public static implicit operator ClockTicks(string text) => new(null, text);

    internal double? Number => _number;

    internal string? Text => _text;
}

/// <summary>The options of <c>Browser.Clock.InstallAsync</c>.</summary>
public sealed record BrowserClockInstallOptions
{
    /// <summary>The time the fake clock starts at; the runner's now when left out.</summary>
    public ClockTime? Time { get; init; }
}

/// <summary>The clock calls a test makes (upstream's <c>ClockCall['method']</c>).</summary>
internal enum ClockMethod
{
    Install,
    PauseAt,
    SetFixedTime,
    SetSystemTime,
    FastForward,
    RunFor,
    Resume,
}

/// <summary>One clock call with its argument resolved to milliseconds, as a replacement context replays it; a call that failed after Playwright logged it counts too (upstream's <c>ClockCall</c>).</summary>
internal readonly record struct ClockCall(ClockMethod Method, long Ms = 0)
{
    /// <summary>The method as the API names it, as in <c>browser.clock.pauseAt</c>.</summary>
    internal string Name =>
        Method switch
        {
            ClockMethod.Install => "install",
            ClockMethod.PauseAt => "pauseAt",
            ClockMethod.SetFixedTime => "setFixedTime",
            ClockMethod.SetSystemTime => "setSystemTime",
            ClockMethod.FastForward => "fastForward",
            ClockMethod.RunFor => "runFor",
            _ => "resume",
        };
}

/// <summary>
/// <c>browser.clock</c>: Playwright's page clock, driven through the attempt's <c>BrowserContext.clock</c> (upstream's
/// <c>web/src/clock.ts</c>). What the engine adds around it: argument checks with Playwright's grammar, the step labels, the
/// log of calls a replacement context replays, and the classification of the errors Playwright throws.
/// </summary>
internal static partial class WebClock
{
    /// <summary>The range a <see cref="DateTimeOffset"/> holds, which a <see cref="DateTime"/> handed to Playwright must stay in.</summary>
    private static readonly long s_minMs = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();

    private static readonly long s_maxMs = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>The calls that run the page's timers while they move the clock.</summary>
    private static readonly HashSet<ClockMethod> RunsTimers =
    [
        ClockMethod.RunFor,
        ClockMethod.FastForward,
        ClockMethod.PauseAt,
    ];

    /// <summary>Runs in a frame: the time its page clock reads, or undefined before the clock is installed there (upstream's <c>readClockNow</c>).</summary>
    public static async Task<double?> ReadClockNowAsync(IFrame frame) =>
        await frame.EvaluateAsync<double?>(PageScript.ReadClockNow).ConfigureAwait(false);

    /// <summary><c>pauseAt</c> to a time before the clock's current time: the test's argument, refused.</summary>
    public static TestException PausedInThePast(string label, Exception? cause = null)
    {
        var text = label + ": the time is before the clock's current time";
        return cause is null
            ? new TestException("INVALID_ARGUMENT", text)
            : new TestException("INVALID_ARGUMENT", text, cause);
    }

    /// <summary>
    /// Epoch milliseconds of <paramref name="time"/>, or <c>INVALID_ARGUMENT</c>. A string parses as a JavaScript <c>Date</c> parses it
    /// (a date alone is UTC, a time without an offset is local); an empty string, which Playwright reads as 1970, is refused. A time
    /// outside years 1 to 9999 is refused, since Playwright for .NET passes a time as a <see cref="DateTime"/>.
    /// </summary>
    public static long ClockTimeValue(string api, ClockTime time) =>
        TimeValue(time)
        ?? throw new TestException(
            "INVALID_ARGUMENT",
            api + " takes epoch milliseconds, a date string, or a valid Date, got " + Describe(time)
        );

    /// <summary>
    /// Milliseconds of <paramref name="ticks"/>, or <c>INVALID_ARGUMENT</c>: a finite number of zero or more, or Playwright's
    /// <c>'ss'</c>, <c>'mm:ss'</c>, <c>'hh:mm:ss'</c> with every field under 60.
    /// </summary>
    public static long ClockTicksValue(string api, ClockTicks ticks) =>
        TicksValue(ticks)
        ?? throw new TestException(
            "INVALID_ARGUMENT",
            api + " takes milliseconds of zero or more, or 'ss', 'mm:ss', or 'hh:mm:ss', got " + Describe(ticks)
        );

    /// <summary>The step label of a time argument, read defensively since the label runs before validation (upstream's <c>clockTimeLabel</c>).</summary>
    public static string ClockTimeLabel(ClockTime? time) =>
        time is { } given && TimeValue(given) is { } ms
            ? DateTimeOffset
                .FromUnixTimeMilliseconds(ms)
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
            : "";

    /// <summary>The step label of a ticks argument: milliseconds, or the string as given (upstream's <c>clockTicksLabel</c>).</summary>
    public static string ClockTicksLabel(ClockTicks ticks)
    {
        if (TicksValue(ticks) is null)
        {
            return "";
        }

        return ticks.Number is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "ms" : ticks.Text!;
    }

    /// <summary>The step label of <c>install</c>: its start time, if it names one (upstream's <c>clockInstallLabel</c>).</summary>
    public static string ClockInstallLabel(BrowserClockInstallOptions? options) => ClockTimeLabel(options?.Time);

    /// <summary>Runs one logged call on a context's clock (upstream's <c>applyClockCall</c>).</summary>
    public static Task ApplyClockCallAsync(IClock clock, ClockCall call)
    {
        switch (call.Method)
        {
            case ClockMethod.Resume:
                return clock.ResumeAsync();
            case ClockMethod.Install:
                return clock.InstallAsync(new PlaywrightClockInstallOptions { TimeDate = Instant(call.Ms) });
            case ClockMethod.FastForward:
                return clock.FastForwardAsync(call.Ms);
            case ClockMethod.RunFor:
                return clock.RunForAsync(call.Ms);
            case ClockMethod.PauseAt:
                return clock.PauseAtAsync(Instant(call.Ms));
            case ClockMethod.SetFixedTime:
                return clock.SetFixedTimeAsync(Instant(call.Ms));
            default:
                return clock.SetSystemTimeAsync(Instant(call.Ms));
        }
    }

    /// <summary>
    /// Classifies a failed clock call. A timeout, a closed context, a crash, or a lost connection is the engine's
    /// (<see cref="WebErrors.Translate"/>). <c>pauseAt</c> before the clock's current time is the test's argument. Anything else a call
    /// that runs timers reports was thrown by page code one of them ran: <c>EVALUATE_FAILED</c>, as Playwright's own <c>runFor</c>
    /// rejects with it (upstream's <c>translateClockError</c>).
    /// </summary>
    public static Exception TranslateClockError(Exception cause, string label, ClockMethod method)
    {
        // The first line, without Playwright's `clock.runFor: ` prefix; the rest is the stack of its injected clock.
        var first = WebErrors.Message(cause).Split('\n', 2)[0].TrimEnd('\r');
        first = PrefixPattern().Replace(first, "");
        if (cause is E2EException)
        {
            return cause;
        }

        if (cause is TimeoutException || ClosedPattern().IsMatch(first) || !RunsTimers.Contains(method))
        {
            return WebErrors.Translate(cause, label);
        }

        // Only pauseAt moves the clock to a time; runFor and fastForward take ticks, refused below zero.
        if (method == ClockMethod.PauseAt && first == "Error: Cannot fast-forward to the past")
        {
            return PausedInThePast(label, cause);
        }

        return new TestException("EVALUATE_FAILED", label + ": a page timer threw: " + first, cause);
    }

    private static DateTime Instant(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    /// <summary>Epoch milliseconds of a time inside the range a <see cref="DateTimeOffset"/> holds, or null.</summary>
    private static long? TimeValue(ClockTime time)
    {
        if (time.Number is { } number)
        {
            if (!double.IsFinite(number) || number < s_minMs || number > s_maxMs)
            {
                return null;
            }

            return (long)Math.Floor(number);
        }

        if (time.Date is { } date)
        {
            return date.ToUnixTimeMilliseconds();
        }

        if (time.Text is { } text && !string.IsNullOrWhiteSpace(text))
        {
            return ParseDate(text.Trim());
        }

        return null;
    }

    /// <summary>A string as a JavaScript <c>new Date(string)</c> reads it, or null.</summary>
    private static long? ParseDate(string text)
    {
        var styles = DateTimeStyles.AllowWhiteSpaces;

        // A date-only ISO string is UTC in JavaScript; every other form without an offset is local time.
        styles |= DateOnlyPattern().IsMatch(text) ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out var parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : null;
    }

    private static long? TicksValue(ClockTicks ticks)
    {
        if (ticks.Number is { } number)
        {
            // Playwright for .NET takes whole milliseconds, which a long holds.
            return double.IsFinite(number) && number >= 0 && number < long.MaxValue ? (long)Math.Floor(number) : null;
        }

        if (ticks.Text is not { } text || !TicksPattern().IsMatch(text))
        {
            return null;
        }

        var seconds = 0L;
        foreach (var part in text.Split(':'))
        {
            var field = long.Parse(part, CultureInfo.InvariantCulture);
            if (field >= 60)
            {
                return null;
            }

            seconds = (seconds * 60) + field;
        }

        return seconds * 1000;
    }

    /// <summary>How a refused argument reads in its error message (upstream's <c>describe</c>).</summary>
    private static string Describe(ClockTime time) =>
        time.Text is { } text ? JsonSerializer.Serialize(text)
        : time.Number is { } number ? number.ToString("R", CultureInfo.InvariantCulture)
        : time.Date is not null ? "an object"
        : "null";

    private static string Describe(ClockTicks ticks) =>
        ticks.Text is { } text ? JsonSerializer.Serialize(text)
        : ticks.Number is { } number ? number.ToString("R", CultureInfo.InvariantCulture)
        : "null";

    [GeneratedRegex(@"^([0-9][0-9]:){0,2}[0-9][0-9]?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TicksPattern();

    [GeneratedRegex(
        @"^[0-9]{4}-[0-9][0-9]-[0-9][0-9]\z",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex DateOnlyPattern();

    /// <summary>Playwright's own prefix of a clock error, which the page's timer error never has.</summary>
    [GeneratedRegex(
        @"^(?:browserContext\.)?clock\.\w+: ",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex PrefixPattern();

    /// <summary>
    /// A browser, context, page, or protocol session that went away under the call, read at the start of the error's first line,
    /// where Playwright puts its own errors: a page timer's error starts with its name (<c>Error: ...</c>) and never matches,
    /// whatever its message says.
    /// </summary>
    [GeneratedRegex(
        @"^(?:Target page, context or browser has been closed|Target (?:closed|crashed)|Page crashed|Session closed|Connection closed|Browser closed|Browser has been disconnected|browser has disconnected|Protocol error \()",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ClosedPattern();
}
