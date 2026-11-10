// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.DatePicker;

/// <summary>Ported from <c>describe('date picker')</c> in upstream's <c>apps/web-benchmark/tests/date-picker.e2e.ts</c>.</summary>
public sealed class DatePickerTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheDatePicker() => await App.OpenAsync("/e/date-picker");

    [Test]
    public async Task Navigates_three_months_ahead_and_books_the_required_date()
    {
        var dateInput = Screen.GetByPlaceholder("Select date");
        await dateInput.TapAsync();
        await Expect.That(Screen.GetByTestId("month-label")).ToHaveTextAsync("June 2026");

        var nextMonth = Screen.GetByRole("button", "›");
        await nextMonth.TapAsync();
        await nextMonth.TapAsync();
        await nextMonth.TapAsync();
        await Expect.That(Screen.GetByTestId("month-label")).ToHaveTextAsync("September 2026");

        await Screen.GetByRole("button", "17").TapAsync();
        await Expect.That(Screen.GetByTestId("month-label")).ToHaveCountAsync(0);
        await Expect.That(dateInput).ToHaveValueAsync("2026-09-17");

        await Screen.GetByRole("button", "Confirm booking").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Appointment booked for September 17, 2026");
    }

    [Test]
    public async Task Confirming_without_the_exact_date_shows_the_error()
    {
        await Screen.GetByRole("button", "Confirm booking").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Pick the exact requested date");

        await Screen.GetByPlaceholder("Select date").TapAsync();
        await Screen.GetByRole("button", "17").TapAsync();
        await Expect.That(Screen.GetByPlaceholder("Select date")).ToHaveValueAsync("2026-06-17");
        await Screen.GetByRole("button", "Confirm booking").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Pick the exact requested date");
    }
}
