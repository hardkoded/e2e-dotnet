// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Naming a group of calls from inside the running test: <c>test.step(title, body)</c> (upstream's <c>internal/test-step.ts</c>).</summary>
internal static class TestStep
{
    /// <summary>
    /// Runs <paramref name="body"/> as one named step of the running test and returns what it returns (upstream's <c>runTestStep</c>). Where no report
    /// takes steps (a worker fixture, a hosted session), <paramref name="body"/> just runs. Outside a test the call throws
    /// <see cref="InvalidOperationException"/> where upstream has <c>COLLECTION_ERROR</c>, and from work a test left running it is cancelled.
    /// </summary>
    internal static async Task<T> RunAsync<T>(string title, Func<Task<T>> body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "Step() title must be a non-empty string, got " + (title is null ? "null" : "a blank one")
            );
        }

        if (body is null)
        {
            throw new TestException("INVALID_ARGUMENT", "Step() takes a function after the title, got null");
        }

        // Work a test left running (a body past its timeout) is no step of the test that runs now.
        if (PollScopes.CurrentPollScope()?.Over == true && !PollScopes.OutlivesTest())
        {
            var error = new TestException("CANCELLED", "Step() was called after its test ended");
            AbandonedWork.MarkRejection(error);
            throw error;
        }

        // A worker fixture runs on an attempt whose steps no report keeps.
        if (PollScopes.OutlivesTest())
        {
            return await body().ConfigureAwait(false);
        }

        var attempt = Attempts.CurrentAttempt()
            ?? throw new InvalidOperationException(
                "Step(title, body) must be called inside a test body, a hook, or a fixture"
            );
        if (attempt.Step is not { } step)
        {
            return await body().ConfigureAwait(false);
        }

        return (T)(await step(title, async () => await body().ConfigureAwait(false)).ConfigureAwait(false))!;
    }

    /// <summary><see cref="RunAsync{T}(string, Func{Task{T}})"/> for a body that returns nothing.</summary>
    internal static Task RunAsync(string title, Func<Task> body) =>
        RunAsync<bool>(
            title,
            body is null
                ? null!
                : (Func<Task<bool>>)(
                    async () =>
                    {
                        await body().ConfigureAwait(false);
                        return true;
                    }
                )
        );
}
