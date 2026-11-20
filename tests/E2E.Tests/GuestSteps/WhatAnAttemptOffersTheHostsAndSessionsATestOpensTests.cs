// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.GuestSteps;

/// <summary>Upstream: packages/e2e/tests/unit/guest-steps.test.ts, describe('what an attempt offers the hosts and sessions a test opens').</summary>
public sealed class WhatAnAttemptOffersTheHostsAndSessionsATestOpensTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath("/projects/shop");

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-guests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (StepRecorder Steps, AttemptGuestSessions Guests) Attempt()
    {
        var steps = new StepRecorder("test-attempt", new StepRecorderOptions { ProjectRoot = Root });
        var sink = new Sink(_dir);
        return (
            steps,
            new AttemptGuestSessions(
                steps,
                () => sink,
                new AttemptGuestOptions
                {
                    Keep = null,
                    VideoPrefix = "",
                    CleanupTimeout = TimeSpan.FromSeconds(1),
                }
            )
        );
    }

    private sealed class Sink(string dir) : IArtifactSink
    {
        public string Dir { get; } = dir;

        public string Register(
            E2E.Reports.ArtifactKind kind,
            string relativePath,
            ArtifactRegistration? registration = null
        ) => "artifact";

        public string Link(string url, ArtifactLink link) => "artifact";
    }

    /// <summary>A session, and what the attempt did to it.</summary>
    private sealed class FakeSession(bool busy = false) : IGuestSession
    {
        public int Closed;
        public int Abandoned;

        public string Root => GuestRoot;

        public bool Busy() => busy && Volatile.Read(ref Closed) == 0;

        public Exception? AbandonRunning()
        {
            Interlocked.Increment(ref Abandoned);
            return null;
        }

        public Task CloseAsync()
        {
            Interlocked.Increment(ref Closed);
            return Task.CompletedTask;
        }

        private static string GuestRoot => Path.GetFullPath("/projects/shop");
    }

    private sealed class FakeHost : IGuestHost
    {
        public int Closed;
        public int Released;

        public Task CloseAsync()
        {
            Interlocked.Increment(ref Closed);
            return Task.CompletedTask;
        }

        public void Release() => Interlocked.Increment(ref Released);
    }

    [Fact]
    public async Task Closes_the_sessions_and_hosts_the_test_opened_once_each_and_releases_a_host_only_at_the_attempts_own_end()
    {
        var (_, guests) = Attempt();
        var idle = new FakeSession();
        var running = new FakeSession(busy: true);
        var host = new FakeHost();
        guests.Offer.Session().Take(idle);
        guests.Offer.Session().Take(running);
        guests.Offer.Host().Take(host);

        // The body is over: only the session a step still runs on is closed before teardown.
        await guests.QuiesceAsync();
        Assert.Equal([0, 1, 0, 0], [idle.Closed, running.Closed, host.Closed, host.Released]);

        Assert.Empty(await guests.SettleAsync(AttemptStatus.Passed));
        Assert.Equal([1, 1, 1, 0], [idle.Closed, running.Closed, host.Closed, host.Released]);

        // The host's secrets stay shared until the attempt's artifacts have settled.
        guests.Release();
        Assert.Equal(1, host.Released);
    }

    [Fact]
    public async Task Fails_the_test_for_an_opening_the_body_left_in_flight_and_expects_the_rejection_of_the_opening_it_cancels()
    {
        var (_, guests) = Attempt();
        var opening = guests.Offer.Session();
        var launching = guests.Offer.Host();

        var errors = await guests.AbandonRunningAsync();

        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Equal("STEP_NOT_AWAITED", ((E2EException)error).Code));
        Assert.Contains("host.NewSessionAsync()", errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("launch()", errors[1].Message, StringComparison.Ordinal);
        Assert.Equal([true, true], [opening.Signal.IsCancellationRequested, launching.Signal.IsCancellationRequested]);
        var cancelled = new InvalidOperationException("opening the e2e session was cancelled");
        guests.Offer.GaveUp(cancelled);
        Assert.True(AbandonedWork.IsRejection(cancelled));
    }

    [Fact]
    public async Task Cancels_an_opening_still_in_flight_when_the_body_ends_and_takes_nothing_once_the_attempt_settles()
    {
        var (_, guests) = Attempt();
        var inFlight = guests.Offer.Session();
        Assert.False(inFlight.Signal.IsCancellationRequested);
        await guests.QuiesceAsync();
        Assert.True(inFlight.Signal.IsCancellationRequested);

        // Opened in the end after all: nobody takes it, so it is its opener's to close.
        Assert.Null(inFlight.Take(new FakeSession()));

        // A teardown hook may still open one, until the attempt settles.
        var inTeardown = new FakeSession();
        Assert.NotNull(guests.Offer.Session().Take(inTeardown));
        await guests.SettleAsync(AttemptStatus.Passed);
        Assert.Equal(1, inTeardown.Closed);
        var late = guests.Offer.Host();
        Assert.True(late.Signal.IsCancellationRequested);
        Assert.False(late.Take(new FakeHost()));
    }

    [Fact]
    public async Task Records_the_steps_of_a_session_opened_outside_every_test_and_leaves_that_session_alone()
    {
        var (steps, guests) = Attempt();
        var visitor = new FakeSession(busy: true);
        var visiting = new StepRecorder(
            "visitor",
            new StepRecorderOptions { Mirror = guests.Offer.Visit(visitor.Root).Progress }
        );
        var finish = new TaskCompletionSource();
        var running = visiting.RunAsync(StepKind.Locator, "locator.waitFor", "Slow", () => finish.Task);

        Assert.Empty(await guests.AbandonRunningAsync());
        await guests.SettleAsync(AttemptStatus.Passed);

        // Its step is told as cut; the session itself was neither blamed nor closed, and goes on.
        Assert.Equal(("launch-1", StepStatus.Cancelled), (steps.All()[0].Actor, steps.All()[0].Status));
        Assert.Equal([0, 0], [visitor.Closed, visitor.Abandoned]);
        finish.SetResult();
        await running;
    }
}
