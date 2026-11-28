// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.EngineTargets;
using E2E.Tests.ReportJson;

namespace E2E.Tests.OpenHostLifecycle;

/// <summary>
/// Upstream's <c>describe('openHost: lifecycle')</c> (<c>tests/integration/open-host-lifecycle.test.ts</c>). The port's host
/// (<c>Standalone.OpenWorkerAsync</c>) is upstream's <c>one-shot</c> worker: it prepares the engine, opens one attempt at a time, and finishes the
/// engine on close. The other <c>it</c> are not ported, because the port has no such feature: they test the hosted kind of upstream's host,
/// which publishes each session for <c>expect.soft</c> and <c>expect.poll</c>, hands out fixtures, and writes a host trace, or they start
/// declared app processes and load a config file, which the port's host leaves to its caller. The <c>it</c> below is partial because the
/// port has no worker <c>dispose</c> hook: the toy's <c>dispose</c> is the end of an attempt's session.
/// </summary>
public sealed class OpenHostLifecycleTests : IDisposable
{
    private readonly ReportDirectory _report = new();

    public void Dispose() => _report.Dispose();

    private E2ESessionOptions Options(ToyEngine engine) =>
        new()
        {
            ResultsDirectory = _report.Results,
            TargetName = "kiosk",
            Engine = engine,
            CacheEnabled = false,
            TestTitle = "cancel opening",
        };

    private StandaloneHostOptions HostOptions(ToyEngine engine) =>
        new()
        {
            Engine = engine,
            TargetName = "kiosk",
            ProjectRoot = _report.Output,
        };

    /// <summary>
    /// Partial: the toy's <c>dispose</c> is the end of an attempt's session, which never started here, so upstream's count of one worker
    /// disposal has nothing to count. Everything else is checked: the cancel, the single finish, and the closed host.
    /// </summary>
    [Fact]
    public async Task Partial_Host_close_cancels_an_opening_session_and_waits_for_disposal_and_finish_exactly_once()
    {
        var starting = new TaskCompletionSource();
        var toy = new ToyEngine
        {
            OnStart = (_, token) =>
            {
                starting.TrySetResult();
                return Task.Delay(Timeout.Infinite, token);
            },
        };
        var opened = await Standalone.OpenWorkerAsync(HostOptions(toy));
        var opening = opened
            .OpenAttemptAsync(Options(toy))
            .ContinueWith(task => task.IsCompletedSuccessfully ? "opened" : "cancelled", TaskScheduler.Default);
        await starting.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var closes = await Task.WhenAll(opened.CloseAsync(), opened.CloseAsync());

        Assert.Equal("cancelled", await opening);
        Assert.Equal(closes[0], closes[1]);
        Assert.Equal(["prepare", "init", "finish"], toy.Lifecycle);
        var closed = await Assert.ThrowsAsync<EngineException>(() => opened.OpenAttemptAsync(Options(toy)));
        Assert.Equal("WORKER_PROTOCOL", closed.Code);

        // The cancelled opening released the host's slot: another host opens at once.
        var other = new ToyEngine();
        var next = await Standalone.OpenAttemptAsync(Options(other));
        next.Session.Complete();
        Assert.Empty(await next.CloseAsync());
    }

    /// <summary>
    /// Upstream's <c>it('retires the host when a launch cancelled after it opened cannot end its attempt')</c>. The other three <c>it</c> of the commit
    /// are not ported here: the failing launch in them is the engine's start, which the port's engine has no separate end for (there is no session to end),
    /// and the last one calls <c>openWorker</c> with a failing fixture policy, which the port has no counterpart for.
    /// </summary>
    [Fact]
    public async Task Retires_the_host_when_a_launch_cancelled_after_it_opened_cannot_end_its_attempt()
    {
        using var controller = new CancellationTokenSource();
        var toy = new ToyEngine
        {
            OnStart = (_, _) =>
            {
                controller.Cancel();
                return Task.CompletedTask;
            },
            OnDispose = () => throw new InvalidOperationException("isolation did not close"),
        };
        var opened = await Standalone.OpenWorkerAsync(HostOptions(toy));
        var cancelled = await Assert.ThrowsAsync<EngineException>(() =>
            opened.OpenAttemptAsync(Options(toy), controller.Token)
        );
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);
        Assert.Equal(["prepare", "init", "dispose", "finish"], toy.Lifecycle);

        var reuse = await Assert.ThrowsAsync<EngineException>(() => opened.OpenAttemptAsync(Options(toy)));
        Assert.Equal("WORKER_PROTOCOL", reuse.Code);
        Assert.Equal(1, toy.Lifecycle.Count(step => step == "init"));
        Assert.Equal(1, toy.Lifecycle.Count(step => step == "finish"));
        var errors = await opened.CloseAsync();
        Assert.Contains("isolation did not close", string.Join("\n", errors.Select(error => error.Message)));
        Assert.Equal(1, toy.Lifecycle.Count(step => step == "finish"));
    }
}
