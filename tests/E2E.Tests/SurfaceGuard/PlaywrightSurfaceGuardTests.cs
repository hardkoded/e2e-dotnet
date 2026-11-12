// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.SurfaceGuard;

/// <summary>
/// The surface's single operation entry, exercised without a page (upstream <c>surface-guard.test.ts</c>, <c>PlaywrightSurface.guard</c>):
/// latched errors fail the next step once, and cancellation is honoured before and during a call. The entry is the web session's
/// <c>GuardAsync</c>, reached through <c>WebEngine.Guard</c> and <c>WebEngine.Latch</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PlaywrightSurfaceGuardTests
{
    [Fact]
    public async Task Rethrows_a_latched_error_once_with_its_own_classification_before_running_anything()
    {
        await WithSurfaceAsync(async session =>
        {
            var violation = new TestException("ACTION_FAILED", "route handler returned without deciding");
            WebEngine.Latch(session.Engine, violation);
            var ran = 0;
            Task<string> Step() => WebEngine.Guard(session.Engine, "step", CancellationToken.None, () =>
            {
                ran++;
                return Task.FromResult("ok");
            });

            var failure = await Assert.ThrowsAsync<TestException>(Step);
            Assert.Same(violation, failure);
            Assert.Equal(0, ran);
            Assert.Equal("ok", await Step());
        });
    }

    [Fact]
    public async Task Fails_an_operation_with_the_error_a_handler_latched_while_it_ran_which_caused_it()
    {
        await WithSurfaceAsync(async session =>
        {
            var refused = new TestException("INVALID_ARGUMENT", "route.fulfill has an invalid header name: \"bad name\"");
            var step = WebEngine.Guard<int>(session.Engine, "navigation", CancellationToken.None, () =>
            {
                WebEngine.Latch(session.Engine, refused);
                throw new InvalidOperationException("page.goto: net::ERR_FAILED");
            });
            var failure = await Assert.ThrowsAsync<TestException>(() => step);
            Assert.Same(refused, failure);
            Assert.Equal("ok", await WebEngine.Guard(session.Engine, "step", CancellationToken.None, () => Task.FromResult("ok")));
        });
    }

    [Fact]
    public async Task Refuses_an_already_cancelled_operation_as_CANCELLED()
    {
        await WithSurfaceAsync(async session =>
        {
            using var controller = new CancellationTokenSource();
            await controller.CancelAsync();
            var failure = await Assert.ThrowsAsync<EngineException>(() => WebEngine.Guard(session.Engine, "step", controller.Token, () => Task.FromResult(1)));
            Assert.Equal("CANCELLED", failure.Code);
            Assert.False(failure.Retryable);
        });
    }

    [Fact]
    public async Task Stops_waiting_on_a_call_once_the_operation_aborts_mid_flight()
    {
        await WithSurfaceAsync(async session =>
        {
            using var controller = new CancellationTokenSource();
            var pending = WebEngine.Guard(session.Engine, "step", controller.Token, () => new TaskCompletionSource<int>().Task);
            await controller.CancelAsync();
            var failure = await Assert.ThrowsAsync<EngineException>(() => pending);
            Assert.Equal("CANCELLED", failure.Code);
        });
    }

    /// <summary>.NET-only: upstream's guard has no grace, which exists because the port's work follows the token and undoes itself.</summary>
    [Fact]
    public async Task Gives_work_that_honours_the_token_a_moment_to_undo_itself_before_it_reports_CANCELLED()
    {
        await WithSurfaceAsync(async session =>
        {
            using var controller = new CancellationTokenSource();
            var undone = false;
            var started = new TaskCompletionSource();
            var pending = WebEngine.Guard<int>(session.Engine, "step", controller.Token, async () =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, controller.Token);
                }
                finally
                {
                    await Task.Delay(200);
                    undone = true;
                }

                return 1;
            });
            await started.Task;
            await controller.CancelAsync();
            var failure = await Assert.ThrowsAsync<EngineException>(() => pending);
            Assert.Equal("CANCELLED", failure.Code);
            Assert.True(undone, "the step reported after the work undid itself");
        });
    }

    private static async Task WithSurfaceAsync(Func<E2ESession, Task> body)
    {
        using var site = await TinySite.StartAsync("<!DOCTYPE html><html><body></body></html>");
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, () => body(session));
    }
}
