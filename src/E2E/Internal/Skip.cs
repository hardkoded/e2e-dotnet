// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>
/// The skip of a running test from its body (upstream's <c>RuntimeSkip</c>): a verdict, not a failure. Upstream throws it and the attempt runner
/// recognizes it by brand; the host here ends the test with its own skip, and the session keeps this value for the report.
/// </summary>
/// <param name="Reason">The skip reason; <c>skipped</c> when none, or a blank one, is given.</param>
/// <param name="Cause">The skip cause: <c>explicit</c> or <c>fixme</c>.</param>
internal sealed record RuntimeSkip(string Reason, string Cause)
{
    /// <summary>A runtime skip with <paramref name="cause"/>; a missing or blank <paramref name="reason"/> is <c>skipped</c>.</summary>
    internal static RuntimeSkip Create(string? reason, string cause) =>
        new(string.IsNullOrWhiteSpace(reason) ? "skipped" : reason, cause);
}

/// <summary>Skipping the running test from its body: <c>test.fixme(condition, reason)</c> (upstream's <c>internal/skip.ts</c>).</summary>
internal static class Skip
{
    /// <summary>The skip cause a runtime skip reports: <c>fixme</c> for that cause, and <c>explicit</c> for any other value, or none (upstream's <c>runtimeSkipCause</c>).</summary>
    internal static string RuntimeSkipCause(object? cause) => cause is "fixme" ? "fixme" : "explicit";

    /// <summary>The skip reason of a fixme test: <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> when a reason that is not blank is given (upstream's <c>fixmeReason</c>).</summary>
    internal static string FixmeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "fixme" : $"fixme: {reason}";

    /// <summary>
    /// Marks the running test fixme when <paramref name="condition"/> holds and returns the runtime skip the host then skips with; null when
    /// the condition does not hold (upstream's <c>skipRunningTest</c> for <c>test.fixme</c>). Outside a body there is no running test. A setup test
    /// is a test error, <c>INVALID_ARGUMENT</c>, because the sessions it declares are owed to their consumers.
    /// </summary>
    internal static RuntimeSkip? FixmeRunningTest(E2ESession? session, bool condition, string? reason)
    {
        if (session is null)
        {
            throw new InvalidOperationException(
                "Fixme(condition, reason) skips the running test and must be called inside a test body; to mark a test fixme at collection, skip it with the host's own attribute"
            );
        }

        if (!condition)
        {
            return null;
        }

        if (session.IsSetup)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "Fixme() cannot skip a setup test: the sessions it declares are owed to their consumers"
            );
        }

        return session.RuntimeSkip = RuntimeSkip.Create(FixmeReason(reason), "fixme");
    }
}
