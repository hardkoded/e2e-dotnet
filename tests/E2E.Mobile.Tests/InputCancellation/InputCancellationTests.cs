// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.InputCancellation;

public sealed class InputCancellationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    // Upstream's three cases run the SDK client over a transport; the fake client is the boundary here, and it holds the press until the test lets it go.
    [Theory]
    [InlineData("caller abort")]
    [InlineData("input deadline")]
    [InlineData("device fixture abort")]
    public async Task Cancels_a_pending_press_at_the_SDK_boundary_after_reason(string reason)
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 0 });
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var finished = new TaskCompletionSource();
        var delivered = 0;
        using var controller = new CancellationTokenSource();
        if (reason == "device fixture abort")
        {
            var snapshot = FakeDeviceClient.SettingsSnapshotWith(
                N("@e30", index: 10, parentIndex: 0, depth: 1, type: "static-text", label: "Speed up your typing by sliding your finger across the letters to compose a word.", rect: new Rect(20, 600, 350, 60)),
                N("@e31", index: 11, parentIndex: 0, depth: 1, type: "button", label: "Continue", rect: new Rect(95, 700, 200, 44)));
            snapshot["keyboard"] = new JsonObject { ["kind"] = "visible", ["frame"] = new JsonObject { ["x"] = 0, ["y"] = 583, ["width"] = 390, ["height"] = 261 } };
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => snapshot.DeepClone());
            h.Fake.Respond(DeviceMethods.CommandKeyboard, _ => throw new AgentDeviceError("UNSUPPORTED_OPERATION", "The keyboard has no dismiss key"));
        }

        var session = await OpenAttempt(h, actionTimeout: reason == "input deadline" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(30));
        try
        {
            var about = reason == "device fixture abort" ? null : await Observed(session, "About");
            h.Fake.RespondAsync(DeviceMethods.InteractionsPress, async (_, token) =>
            {
                started.TrySetResult();
                await release.Task;
                if (!token.IsCancellationRequested)
                {
                    Interlocked.Increment(ref delivered);
                }

                finished.TrySetResult();
                return new JsonObject();
            });
            var pressing = about is null
                ? session.DismissKeyboardAsync(controller.Token)
                : session.PerformAsync(about, new LocatorAction.Tap(), OperationContext.Test, controller.Token);
            Task rejected = reason == "input deadline"
                ? Assert.ThrowsAsync<EngineException>(() => pressing).ContinueWith(
                    task => Assert.Equal("ACTION_MAY_HAVE_COMMITTED", task.Result.Code),
                    TaskScheduler.Default)
                : Assert.ThrowsAsync<EngineException>(() => pressing).ContinueWith(
                    task => Assert.Equal("CANCELLED", task.Result.Code),
                    TaskScheduler.Default);
            await Task.WhenAny(started.Task, rejected).WaitAsync(Bound);
            Assert.True(started.Task.IsCompleted, $"the press was never sent: {(rejected.IsFaulted ? (pressing.Exception?.GetBaseException().Message ?? "") : "")}");
            if (reason != "input deadline")
            {
                await controller.CancelAsync();
            }

            await rejected.WaitAsync(Bound);

            // The external request resumes after the operation has already ended.
            release.SetResult();
            await finished.Task.WaitAsync(Bound);
            Assert.Equal(0, delivered);
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
