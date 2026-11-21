// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Internal;

/// <summary>Changing the running test's timeout from inside it: <c>test.setTimeout(timeout)</c> (upstream's <c>internal/test-timeout.ts</c>).</summary>
internal static class TestTimeout
{
    /// <summary>
    /// Gives the running test a timeout of <paramref name="timeout"/>, counted from the start of its attempt, the way the configured timeout is
    /// (upstream's <c>setRunningTestTimeout</c>). <paramref name="late"/> is set once the test body returned: what runs after it is teardown, on the
    /// cleanup timeout. A timeout that is not a positive whole number of milliseconds, or above 2147483647, a hosted session (it runs no test), a
    /// teardown, and code the test left running after its timeout fired or its attempt ended are test errors, <c>INVALID_ARGUMENT</c>.
    /// </summary>
    internal static void SetRunningTestTimeout(E2ESession session, TimeSpan timeout, bool late = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        var milliseconds = timeout.TotalMilliseconds;
        if (timeout <= TimeSpan.Zero || timeout.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "SetTimeout() timeout must be a positive whole number of milliseconds, got "
                    + milliseconds.ToString(CultureInfo.InvariantCulture)
            );
        }

        if (milliseconds > Time.MaxTimerMs)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "SetTimeout() timeout must be at most "
                    + Time.MaxTimerMs.ToString(CultureInfo.InvariantCulture)
                    + " milliseconds, got "
                    + milliseconds.ToString(CultureInfo.InvariantCulture)
            );
        }

        if (session.IsHosted)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "SetTimeout() changes an e2e test's timeout, and a hosted session runs none: your test runner owns the test timeout, and a session's Timeout option sets the session deadline"
            );
        }

        if (late || session.InTeardown)
        {
            throw LateTimeoutError();
        }

        if (!session.TrySetTimeout(timeout))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "SetTimeout() cannot change the timeout from code left running after its test ended: await the work the test starts"
            );
        }
    }

    /// <summary>The error for a timeout change from teardown, which runs on the cleanup timeout (upstream's <c>lateTimeoutError</c>).</summary>
    internal static TestException LateTimeoutError(string from = "an afterEach hook or a fixture teardown") =>
        new(
            "INVALID_ARGUMENT",
            $"SetTimeout() cannot change the timeout from {from}: teardown runs on CleanupTimeout, not the test timeout"
        );
}
