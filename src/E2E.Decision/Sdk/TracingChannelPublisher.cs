// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's tracing channel publisher (<c>telemetry/tracing-channel-publisher.ts</c>).</summary>
internal static class TracingChannelPublisher
{
    /// <summary>The error a subscriber sees when the call does not record its results (<c>new Error('AI SDK operation failed')</c>).</summary>
    private const string OperationFailed = "AI SDK operation failed";

    /// <summary>
    /// <c>runWithTracingChannelSpan</c>: runs <paramref name="execute"/> inside the AI SDK telemetry tracing channel when it has
    /// subscribers; with none, it is a direct pass-through. The call's own result or error is returned or thrown, even if tracing itself
    /// throws, and <paramref name="execute"/> never runs a second time.
    /// </summary>
    internal static async Task<T> RunWithTracingChannelSpanAsync<T>(
        TelemetryTracingChannelMessage message,
        Func<Task<T>> execute,
        bool recordResult = true
    )
    {
        var tracingChannel = DiagnosticsChannel.GetTracingChannel(TelemetryTracingChannel.AiSdkTelemetryTracingChannel);
        if (!tracingChannel.HasSubscribers)
        {
            return await execute().ConfigureAwait(false);
        }

        Task<T>? executeTask = null;
        try
        {
            await tracingChannel
                .TracePromiseAsync(
                    async () =>
                    {
                        // The call's own task is kept apart from the copy the channel publishes.
                        executeTask = RunAsync(execute);
                        if (!recordResult)
                        {
                            try
                            {
                                await executeTask.ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                throw new InvalidOperationException(OperationFailed);
                            }

                            return default(T);
                        }

                        // tracePromise publishes the error on the channel. Keep the application's error separately from the
                        // redacted telemetry copy. A decide result holds no provider metadata, so it is published as it is.
                        try
                        {
                            return await executeTask.ConfigureAwait(false);
                        }
                        catch (Exception error)
                        {
                            var redacted = RedactTelemetryCredentials.Redact(error);
                            if (ReferenceEquals(redacted, error))
                            {
                                throw;
                            }

                            throw redacted ?? new InvalidOperationException(OperationFailed);
                        }
                    },
                    message
                )
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Tracing threw, or the call failed: the call's own outcome wins, and the call never runs twice.
            return await (executeTask ?? execute()).ConfigureAwait(false);
        }

        return await (executeTask ?? execute()).ConfigureAwait(false);
    }

    private static Task<T> RunAsync<T>(Func<Task<T>> execute)
    {
        try
        {
            return execute();
        }
        catch (Exception error)
        {
            return Task.FromException<T>(error);
        }
    }
}
