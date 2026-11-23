// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// A worker-scoped <c>test.extend()</c> fixture (<c>FixtureDefinition</c> with <c>scope: 'worker'</c>, <c>collect/registry.ts</c>), as a closure: the function sets
/// up what it provides, hands it over with <c>use</c>, and awaits what <c>use</c> returns before it tears down. It is set up inside the first test that
/// names it, outside the test's attempt, and torn down when the run ends. What it hands over is <see cref="Value"/> (.NET-only: a test has no fixtures
/// object to destructure).
/// </summary>
internal sealed class FixtureDefinition(string name, Func<Func<object?, Task>, Task> fn)
{
    /// <summary>The fixture's name.</summary>
    public string Name { get; } = name;

    /// <summary>The function that sets the fixture up and tears it down around its call to <c>use</c>.</summary>
    public Func<Func<object?, Task>, Task> Fn { get; } = fn;

    /// <summary>What the fixture handed to <c>use</c>, once it is set up.</summary>
    public object? Value { get; internal set; }
}

/// <summary>
/// The worker-scoped fixtures of one run, one instance per definition (upstream's <c>WorkerFixturePool</c>, <c>run/fixture-pool.ts</c>): set up the
/// first time a test names one, outside the attempt (<c>outlivesTest</c>), and torn down in reverse order when the run ends.
/// </summary>
internal sealed class WorkerFixturePool
{
    private readonly List<(
        FixtureDefinition Fixture,
        TaskCompletionSource Release,
        Task Run,
        PollLineage Polls
    )> _running = [];

    /// <summary>Sets up the fixtures not yet set up. A failure of one is the failure of the test that named it, and the fixture is not torn down.</summary>
    public async Task AcquireAsync(IReadOnlyList<FixtureDefinition> fixtures, CancellationToken cancellationToken)
    {
        foreach (var fixture in fixtures)
        {
            if (_running.Any(each => ReferenceEquals(each.Fixture, fixture)))
            {
                continue;
            }

            var provided = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var used = false;
            // The function's polls are its user's until use returns, then its teardown's.
            var polls = PollScopes.PollLineage(outlivesTest: true);
            ConfigurationException? misuse = null;
            Task Use(object? value)
            {
                if (used)
                {
                    misuse = new ConfigurationException(
                        "TEST_SETUP_FAILED",
                        $"fixture \"{fixture.Name}\" called use() twice; a fixture hands one value per instance"
                    );
                    return Task.FromException(misuse);
                }

                used = true;
                fixture.Value = value;
                provided.TrySetResult();
                return release.Task;
            }

            // On the thread pool and with no attempt in its flow: a host the fixture launches belongs to the worker.
            var run = Task.Run(async () =>
            {
                try
                {
                    await polls.Run(() => fixture.Fn(Use)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    provided.TrySetException(ex);
                    throw;
                }

                if (!used)
                {
                    provided.TrySetException(
                        new ConfigurationException(
                            "TEST_SETUP_FAILED",
                            $"fixture \"{fixture.Name}\" returned without calling use(); a fixture awaits use(value) to hand its value over"
                        )
                    );
                }

                if (misuse is not null)
                {
                    throw misuse;
                }
            });
            // A fixture whose setup failed is never torn down, so nothing else would observe its failure.
            _ = run.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
            // Kept from now on: a run cancelled while the setup is pending still releases it, and a late use() is handed over to the teardown.
            var entry = (fixture, release, run, polls);
            _running.Add(entry);
            try
            {
                await provided.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (provided.Task.IsFaulted)
            {
                // A setup that failed is never torn down.
                _running.Remove(entry);
                throw;
            }
        }
    }

    /// <summary>Tears the fixtures down in reverse order of their setup and returns what failed.</summary>
    public async Task<IReadOnlyList<Exception>> CloseAsync(TimeSpan timeout)
    {
        var failures = new List<Exception>();
        for (var index = _running.Count - 1; index >= 0; index--)
        {
            var (_, release, run, polls) = _running[index];
            // The rest of the function after use is its teardown's.
            polls.HandOver();
            release.TrySetResult();
            try
            {
                await run.WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        _running.Clear();
        return failures;
    }
}
