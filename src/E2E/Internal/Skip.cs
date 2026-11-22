// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Skipping the running test from its body: <c>test.fixme(condition, reason)</c> (upstream's <c>internal/skip.ts</c>).</summary>
internal static class Skip
{
    /// <summary>The skip reason of a fixme test: <c>fixme</c>, or <c>fixme: &lt;reason&gt;</c> when a reason that is not blank is given (upstream's <c>fixmeReason</c>).</summary>
    internal static string FixmeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "fixme" : $"fixme: {reason}";

    /// <summary>
    /// Marks the running test fixme when <paramref name="condition"/> holds and returns the skip reason the host then skips with; null when
    /// the condition does not hold (upstream's <c>skipRunningTest</c> for <c>test.fixme</c>). Outside a body there is no running test. A setup test
    /// is a test error, <c>INVALID_ARGUMENT</c>, because the sessions it declares are owed to their consumers.
    /// </summary>
    internal static string? FixmeRunningTest(E2ESession? session, bool condition, string? reason)
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

        return session.FixmeReason = FixmeReason(reason);
    }
}
