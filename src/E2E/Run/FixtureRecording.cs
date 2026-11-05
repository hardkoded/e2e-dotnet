// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>What runs inside the step of a fixture call (upstream's <c>run/fixture-recording.ts</c>).</summary>
internal static class FixtureRecording
{
    /// <summary>
    /// Runs one operation and maps an <see cref="EngineException"/> it rejects with onto the runner taxonomy, as every other engine
    /// surface is mapped (upstream's <c>translated</c>): a fixture that lost its session throws <c>INVALID_STATE</c>, which a test reads
    /// as <c>APP_NOT_OPEN</c>. The suffix names the operation and its label, so a timeout says what it waited for. Anything else is
    /// rethrown as the call threw it.
    /// </summary>
    internal static async Task<T> Translated<T>(string api, string label, Func<Task<T>> body)
    {
        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (EngineException cause)
        {
            throw ErrorSerializer.TranslateEngineError(cause, " in " + (label == "" ? api : api + " " + label));
        }
    }

    /// <inheritdoc cref="Translated{T}(string, string, Func{Task{T}})"/>
    internal static Task Translated(string api, string label, Func<Task> body) =>
        Translated(api, label, async () =>
        {
            await body().ConfigureAwait(false);
            return true;
        });
}
