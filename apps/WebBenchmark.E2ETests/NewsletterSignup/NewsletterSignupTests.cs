// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.NewsletterSignup;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/newsletter-signup.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class NewsletterSignupTests : BenchmarkTest
{
    [Test]
    public async Task Subscribe_renders_no_feedback_and_logs_a_TypeError_planted_bug()
    {
        await App.OpenAsync("/e/newsletter-signup");
        var consoleErrors = await CaptureConsoleErrorsAsync();
        var email = Screen.GetByPlaceholder("you@example.com");
        await email.FillAsync("maria.novak@example.com");
        await Screen.GetByRole("button", "Subscribe").TapAsync();

        await Expect.That(Screen.GetByTestId("success-message")).Not.ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("error-message")).Not.ToBeVisibleAsync();
        await Expect.That(email).ToHaveValueAsync("maria.novak@example.com");
        await Expect.Poll(async () => string.Join('\n', await consoleErrors())).ToMatchAsync(
            Does.Contain("Cannot read properties of undefined (reading 'subscribe')"));
    }

    /// <summary>
    /// Hooks <c>console.error</c> in the page and returns a reader of what the app has logged since. The planted bug's only trace is a
    /// console error, and the engine has no <c>browser.onConsole</c> yet, so the page keeps the list itself (upstream's
    /// <c>captureConsoleErrors</c>).
    /// </summary>
    private async Task<Func<Task<string[]>>> CaptureConsoleErrorsAsync()
    {
        await Browser.EvaluateAsync<object>("""
            () => {
              const errors = [];
              window.e2eConsoleErrors = errors;
              const original = console.error;
              console.error = (...args) => {
                errors.push(args.map(String).join(' '));
                original(...args);
              };
              return null;
            }
            """);
        return async () => await Browser.EvaluateAsync<string[]>("() => window.e2eConsoleErrors ?? []") ?? [];
    }
}
