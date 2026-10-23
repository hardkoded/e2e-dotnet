// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.EngineTargets;
using E2E.Tests.ReportJson;

namespace E2E.Tests.StandaloneHost;

/// <summary>
/// .NET-only: upstream tests its standalone host through <c>openHost</c> (<c>open-host-lifecycle</c>, <c>engine-target</c>), and has no
/// <c>it</c> for the end of the process, for the environment prepare hands the worker, or for a caller who cancels while a hook that
/// ignores its token is still leasing. A hosted device bills until it is released, so each way an attempt can end must finish the engine once.
/// </summary>
public sealed class StandaloneHostTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly ReportDirectory _report = new();

    public void Dispose() => _report.Dispose();

    private E2ESessionOptions Options(ToyEngine engine, string? projectRoot = null) => new()
    {
        ResultsDirectory = _report.Results,
        ProjectRoot = projectRoot ?? _report.Output,
        TargetName = "kiosk",
        Engine = engine,
        CacheEnabled = false,
        TestTitle = "standalone",
    };

    [Fact]
    public async Task Prepares_one_slot_and_hands_the_worker_what_prepare_left_beside_the_run_environment()
    {
        var toy = new ToyEngine();
        var host = await Standalone.OpenWorkerAsync(new StandaloneHostOptions
        {
            Engine = toy,
            TargetName = "kiosk",
            ProjectRoot = _report.Output,
            Env = new Dictionary<string, string?> { ["TOY_CACHE"] = "/run/cache" },
        });
        var attempt = await host.OpenAttemptAsync(Options(toy));
        attempt.Session.Complete();
        Assert.Empty(await attempt.CloseAsync());
        Assert.Empty(await host.CloseAsync());

        Assert.Equal(["prepare", "init", "dispose", "finish"], toy.Lifecycle);
        Assert.Equal(1, toy.Prepared!.Slots);
        Assert.Equal("kiosk", toy.Prepared.TargetName);
        Assert.Equal(_report.Output, toy.Prepared.ProjectRoot);
        Assert.Equal(toy.Prepared.RunId, toy.Finished!.RunId);

        // What prepare left reaches the worker; the run's own variable wins.
        var env = toy.Started!.Env!;
        Assert.Equal("sim-a,sim-b", env["TOY_POOL"]);
        Assert.Equal("/run/cache", env["TOY_CACHE"]);
    }

    [Fact]
    public async Task Finishes_the_engine_once_when_the_attempt_closes_twice()
    {
        var toy = new ToyEngine();
        var attempt = await Standalone.OpenAttemptAsync(Options(toy));
        attempt.Session.Complete();
        await attempt.CloseAsync();
        await attempt.CloseAsync();
        await attempt.DisposeAsync();
        Assert.Equal(["prepare", "init", "dispose", "finish"], toy.Lifecycle);
    }

    [Fact]
    public async Task Finishes_the_engine_when_the_attempt_fails_to_start_and_keeps_the_failure_of_the_start()
    {
        var toy = new ToyEngine { OnStart = (_, _) => throw new EngineException("ENVIRONMENT_UNAVAILABLE", "the app would not launch") };
        var failure = await Assert.ThrowsAsync<EngineException>(() => Standalone.OpenAttemptAsync(Options(toy)));
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", failure.Code);
        Assert.Equal(["prepare", "init", "finish"], toy.Lifecycle);
    }

    [Fact]
    public async Task Adds_a_finish_that_fails_to_the_failure_of_the_start()
    {
        var toy = new ToyEngine
        {
            OnStart = (_, _) => throw new EngineException("ENVIRONMENT_UNAVAILABLE", "the app would not launch"),
            OnFinish = _ => throw new InvalidOperationException("stop failed"),
        };
        var failure = await Assert.ThrowsAsync<EngineException>(() => Standalone.OpenAttemptAsync(Options(toy)));
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", failure.Code);
        Assert.Contains("the app would not launch", failure.Message, StringComparison.Ordinal);
        Assert.Contains("stop failed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_finish_that_fails_when_the_attempt_is_disposed_instead_of_losing_it()
    {
        var toy = new ToyEngine { OnFinish = _ => throw new InvalidOperationException("stop failed") };
        var attempt = await Standalone.OpenAttemptAsync(Options(toy));
        attempt.Session.Complete();
        var failure = await Assert.ThrowsAsync<EngineException>(async () => await attempt.DisposeAsync());
        Assert.Equal(EngineErrorCodes.EngineFailure, failure.Code);
        Assert.Contains("stop failed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_a_caller_who_cancels_a_cancel_without_waiting_for_a_prepare_that_ignores_its_token()
    {
        var leasing = new TaskCompletionSource();
        var grant = new TaskCompletionSource();
        var toy = new ToyEngine
        {
            OnPrepare = async _ =>
            {
                leasing.SetResult();
                await grant.Task;
            },
        };
        using var request = new CancellationTokenSource();
        var opening = Standalone.OpenAttemptAsync(Options(toy), request.Token);
        await leasing.Task.WaitAsync(Patience);
        await request.CancelAsync();

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => opening.WaitAsync(Patience));
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);

        // The hook was registered before it ran, so what it grants once it answers is finished, not left behind.
        Assert.Equal(["prepare", "finish"], toy.Lifecycle);
        grant.SetResult();
    }

    [Fact]
    public async Task Surfaces_a_cancel_of_the_start_as_a_cancel_and_finishes_the_engine()
    {
        var starting = new TaskCompletionSource();
        var toy = new ToyEngine
        {
            OnStart = (_, token) =>
            {
                starting.SetResult();
                return Task.Delay(Timeout.Infinite, token);
            },
        };
        using var request = new CancellationTokenSource();
        var opening = Standalone.OpenAttemptAsync(Options(toy), request.Token);
        await starting.Task.WaitAsync(Patience);
        await request.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        Assert.Equal(["prepare", "init", "finish"], toy.Lifecycle);
    }

    [Fact]
    public async Task Refuses_a_second_attempt_while_one_is_live_and_opens_the_next_once_it_closed()
    {
        var toy = new ToyEngine();
        var host = await Standalone.OpenWorkerAsync(new StandaloneHostOptions { Engine = toy, TargetName = "kiosk", ProjectRoot = _report.Output });
        var first = await host.OpenAttemptAsync(Options(toy));
        var refused = await Assert.ThrowsAsync<EngineException>(() => host.OpenAttemptAsync(Options(toy)));
        Assert.Equal("WORKER_PROTOCOL", refused.Code);

        first.Session.Complete();
        Assert.Empty(await first.CloseAsync());
        var second = await host.OpenAttemptAsync(Options(toy));
        second.Session.Complete();
        Assert.Empty(await second.CloseAsync());

        // One prepare served both attempts, and the host's close is the one finish.
        Assert.Empty(await host.CloseAsync());
        Assert.Equal(["prepare", "init", "dispose", "init", "dispose", "finish"], toy.Lifecycle);
    }

    [Fact]
    public async Task Releases_at_the_end_of_the_process_once_and_not_again_when_the_host_closes()
    {
        var toy = new ToyEngine();
        var host = await Standalone.OpenWorkerAsync(new StandaloneHostOptions { Engine = toy, TargetName = "kiosk", ProjectRoot = _report.Output });
        host.ReleaseAtExit();
        Assert.Equal(["prepare", "finish"], toy.Lifecycle);
        Assert.Empty(await host.CloseAsync());
        Assert.Equal(["prepare", "finish"], toy.Lifecycle);
    }
}
