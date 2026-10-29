// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Mobile.Tests.Engine;

// .NET-only: upstream's client has no abort signal or HTTP cancellation deadline.
public sealed class TrackedClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blocks_later_attempts_after_an_abort_without_daemon_confirmation(bool wrapsCancellation)
    {
        var h = Harness.Create();
        var time = new FakeTimeProvider();
        var inflight = h.Engine.InflightOf("ios-simulator", 0, null);
        var client = new TrackedClient(h.Fake, inflight, time);
        var daemon = new TaskCompletionSource<JsonNode?>();
        var received = new TaskCompletionSource<CancellationToken>();
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, async (_, token) =>
        {
            received.SetResult(token);
            try
            {
                return await daemon.Task.WaitAsync(token);
            }
            catch (OperationCanceledException) when (wrapsCancellation)
            {
                throw new HttpRequestException("request aborted before the daemon answered");
            }
        });
        using var caller = new CancellationTokenSource();
        var pending = client.CallAsync(DeviceMethods.InteractionsPress, null, caller.Token);
        // Session commands can register the same task again. That must not lose its abort tracking.
        inflight.Track(pending);
        var sent = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await caller.CancelAsync();
        time.Advance(E2EDefaults.LaunchTimeout - TimeSpan.FromMilliseconds(1));
        Assert.False(sent.IsCancellationRequested);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(sent.IsCancellationRequested);

        if (wrapsCancellation)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        var before = h.Fake.Calls.Count;
        var failure = await Assert.ThrowsAsync<EngineException>(() => h.Boot().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.False(failure.Retryable);
        // Completing the daemon's work cannot confirm completion through the already aborted request.
        daemon.SetResult(new JsonObject());
        var retry = await Assert.ThrowsAsync<EngineException>(() => h.Boot().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("ENGINE_FAILURE", retry.Code);
        Assert.Equal(before, h.Fake.Calls.Count);
        Assert.Empty(h.Sessions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_request_that_fails_after_the_caller_left_blocks_later_attempts_only_without_a_daemon_answer(bool transportFailure)
    {
        var h = Harness.Create();
        var time = new FakeTimeProvider();
        var inflight = h.Engine.InflightOf("ios-simulator", 0, null);
        var client = new TrackedClient(h.Fake, inflight, time);
        var daemon = new TaskCompletionSource<JsonNode?>();
        var received = new TaskCompletionSource();
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, async (_, _) =>
        {
            received.SetResult();
            return await daemon.Task;
        });
        using var caller = new CancellationTokenSource();
        var pending = client.CallAsync(DeviceMethods.InteractionsPress, null, caller.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await caller.CancelAsync();
        // The failure lands before the abort deadline.
        daemon.SetException(transportFailure
            ? new AgentDeviceError("COMMAND_FAILED", "Daemon request failed: connection reset", inner: new HttpRequestException("reset"))
            : new AgentDeviceError("COMMAND_FAILED", "element not found"));
        await Assert.ThrowsAsync<AgentDeviceError>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));

        if (transportFailure)
        {
            var failure = await Assert.ThrowsAsync<EngineException>(() => h.Boot().WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("ENGINE_FAILURE", failure.Code);
            Assert.Empty(h.Sessions);
        }
        else
        {
            await using var session = await h.Boot().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Single(h.Sessions);
        }
    }

    [Fact]
    public async Task Allows_the_next_attempt_when_a_client_ignores_the_abort_and_confirms_completion()
    {
        var h = Harness.Create();
        var time = new FakeTimeProvider();
        var inflight = h.Engine.InflightOf("ios-simulator", 0, null);
        var client = new TrackedClient(h.Fake, inflight, time);
        var daemon = new TaskCompletionSource<JsonNode?>();
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, (_, _) => daemon.Task);
        using var caller = new CancellationTokenSource();
        var pending = client.CallAsync(DeviceMethods.InteractionsPress, null, caller.Token);
        await caller.CancelAsync();
        time.Advance(E2EDefaults.LaunchTimeout);
        Assert.False(pending.IsCompleted);
        daemon.SetResult(new JsonObject());
        await pending.WaitAsync(TimeSpan.FromSeconds(10));

        await using var session = await h.Boot().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(h.Sessions);
    }
}
