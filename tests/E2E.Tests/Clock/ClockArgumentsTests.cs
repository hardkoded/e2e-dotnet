// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Clock;

/// <summary>
/// Upstream's <c>describe('clock arguments')</c> (<c>packages/web/tests/unit/clock.test.ts</c>, tester-army/e2e#2256): the clock's argument grammar,
/// Playwright's own, and the step labels, which run on raw arguments before any check. Playwright for .NET takes whole milliseconds, and a time as a
/// <see cref="DateTime"/>, so a fraction of a millisecond is dropped and a time outside years 1 to 9999 is refused; there is no <c>null</c> or object
/// argument to refuse, and a <see cref="DateTimeOffset"/> is never invalid.
/// </summary>
public sealed class ClockArgumentsTests
{
    private static void Refused(Action read)
    {
        var error = Assert.IsType<TestException>(Record.Exception(read));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    [Fact]
    public void Partial_Reads_ticks_as_Playwright_does_milliseconds_or_ss_mm_ss_hh_mm_ss_with_fields_under_60()
    {
        Assert.Equal(0, WebClock.ClockTicksValue("api", 0L));
        Assert.Equal(5_000, WebClock.ClockTicksValue("api", "5"));
        Assert.Equal(90_000, WebClock.ClockTicksValue("api", "01:30"));
        Assert.Equal(7_201_000, WebClock.ClockTicksValue("api", "02:00:01"));
        foreach (
            var refused in new ClockTicks[]
            {
                -1L,
                double.NaN,
                double.PositiveInfinity,
                "",
                "1:30",
                "00:60",
                "60",
                "01:02:03:04",
                "1h",
                "5\n",
                default,
            }
        )
        {
            Refused(() => WebClock.ClockTicksValue("api", refused));
        }
    }

    [Fact]
    public void Partial_Reads_a_time_as_epoch_milliseconds_a_string_Date_parses_or_a_valid_Date()
    {
        var at = DateTimeOffset
            .Parse("2024-02-02T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
            .ToUnixTimeMilliseconds();
        Assert.Equal(at, WebClock.ClockTimeValue("api", at));
        Assert.Equal(-1, WebClock.ClockTimeValue("api", -1L));
        Assert.Equal(at, WebClock.ClockTimeValue("api", "2024-02-02T08:00:00Z"));
        Assert.Equal(at, WebClock.ClockTimeValue("api", DateTimeOffset.FromUnixTimeMilliseconds(at)));

        // A date alone is UTC, as a JavaScript Date reads it.
        Assert.Equal(
            DateTimeOffset
                .Parse("2024-02-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
                .ToUnixTimeMilliseconds(),
            WebClock.ClockTimeValue("api", "2024-02-02")
        );

        // So are the reduced ISO date forms.
        Assert.Equal(
            DateTimeOffset
                .Parse("2024-02-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
                .ToUnixTimeMilliseconds(),
            WebClock.ClockTimeValue("api", "2024-02")
        );
        Assert.Equal(
            DateTimeOffset
                .Parse("2024-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
                .ToUnixTimeMilliseconds(),
            WebClock.ClockTimeValue("api", "2024")
        );

        // 1e16 is past the range a Date holds, which Playwright refuses as an invalid date.
        foreach (var refused in new ClockTime[] { "", " ", "soon", double.NaN, double.PositiveInfinity, 1e16, default })
        {
            Refused(() => WebClock.ClockTimeValue("api", refused));
        }
    }

    [Fact]
    public void Partial_Labels_a_valid_argument_and_leaves_an_invalid_one_blank_instead_of_throwing()
    {
        Assert.Equal("1970-01-01T00:00:00.000Z", WebClock.ClockTimeLabel(0L));
        Assert.Equal("250ms", WebClock.ClockTicksLabel(250L));
        Assert.Equal("01:30", WebClock.ClockTicksLabel("01:30"));
        Assert.Equal(
            "2024-02-02T08:00:00.000Z",
            WebClock.ClockInstallLabel(new BrowserClockInstallOptions { Time = "2024-02-02T08:00:00Z" })
        );
        Assert.Equal("", WebClock.ClockInstallLabel(null));
        Assert.Equal("", WebClock.ClockTimeLabel(1e16));
        Assert.Equal("", WebClock.ClockTimeLabel("soon"));
        Assert.Equal("", WebClock.ClockTicksLabel("soon"));
        Assert.Equal("1969-12-31T23:59:59.999Z", WebClock.ClockTimeLabel(-1L));
        Assert.Equal("", WebClock.ClockTicksLabel(-1L));
    }
}
