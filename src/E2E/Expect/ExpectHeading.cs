// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The caller's label for a check, as it opens a failure message. Upstream's <c>expect/heading.ts</c>.</summary>
internal static class ExpectHeading
{
    /// <summary><paramref name="text"/> after the caller's <paramref name="message"/> and a colon, or <paramref name="text"/> alone when there is no message.</summary>
    public static string WithMessage(string? message, string text) => message is null ? text : message + ": " + text;

    /// <summary>
    /// An ambiguous locator's error opened with the caller's message, as an assertion failure is: it is the commonest way a
    /// check fails at once. The code and details stay; the original is the cause. Any other error is returned as it is.
    /// </summary>
    public static Exception LabelAmbiguous(Exception error, string? message) =>
        message is null || error is not TestException { Code: "LOCATOR_AMBIGUOUS" } ambiguous
            ? error
            : new TestException(ambiguous.Code, WithMessage(message, ambiguous.Message), ambiguous)
            {
                Details = ambiguous.Details,
            };
}
