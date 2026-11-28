// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>Lifecycle callbacks fired while starting services (upstream's <c>ServiceHooks</c>, <c>run/services.ts</c>).</summary>
internal sealed class ServiceHooks
{
    /// <summary>A service is about to start. The handler can cancel the run, and then no service code runs.</summary>
    public Action<string>? Starting { get; init; }

    /// <summary>A service yielded, after this long.</summary>
    public Action<string, TimeSpan>? Ready { get; init; }

    /// <summary>Stopping an already-started service failed after another service failed to start.</summary>
    public Action<Exception>? StopFailed { get; init; }
}

/// <summary>Started services for an active run (upstream's <c>RunServices</c>, <c>run/services.ts</c>).</summary>
internal sealed class RunServices : IAppProcesses
{
    /// <summary>Empty <see cref="RunServices"/> used when no services were started (upstream's <c>NO_SERVICES</c>).</summary>
    public static RunServices None { get; } = new([], []);

    private readonly List<StartedService> _started;

    internal RunServices(List<StartedService> started, Dictionary<string, object> values)
    {
        _started = started;
        Values = new ServiceValues(values);
    }

    /// <summary>The yielded values by service name.</summary>
    public ServiceValues Values { get; }

    /// <inheritdoc />
    public bool Running => true;

    /// <summary>
    /// Stops all started services in reverse order. Any failure is passed to <paramref name="onFailure"/> without preventing the remaining
    /// services from stopping. Without a forced stop, the shutdown timeouts bound each wait.
    /// </summary>
    public async Task StopAsync(Action<Exception> onFailure) =>
        await StopAsync(onFailure, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Like <see cref="StopAsync(Action{Exception})"/>, with <paramref name="force"/> the second Ctrl+C: it cancels every service's signal.</summary>
    public async Task StopAsync(Action<Exception> onFailure, CancellationToken force)
    {
        ArgumentNullException.ThrowIfNull(onFailure);
        StartedService[] stopping;
        lock (_started)
        {
            stopping = [.. _started];
            _started.Clear();
        }

        Array.Reverse(stopping);

        // A forced stop cancels every service's signal right away, since one service's stop code can wait on another that stops after it.
        void AbortAll()
        {
            foreach (var service in stopping)
            {
                service.Cancel();
            }
        }

        if (force.IsCancellationRequested)
        {
            AbortAll();
        }

        using var registration = force.Register(AbortAll);
        foreach (var service in stopping)
        {
            try
            {
                await StopServiceAsync(service, force).ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not OperationCanceledException || !force.IsCancellationRequested)
            {
                onFailure(cause);
            }
            finally
            {
                service.Dispose();
            }
        }
    }

    /// <summary>
    /// Starts <paramref name="services"/> in order and keeps each yielded value (upstream's <c>startServices</c>). When
    /// <paramref name="signal"/> is cancelled, the remaining services do not start. If a service fails to start, the ones already created are
    /// stopped before the error is rethrown. When <paramref name="force"/> is cancelled (a second Ctrl+C), the active service's stop signal
    /// is cancelled along with the signals of the remaining services.
    /// </summary>
    public static async Task<RunServices> StartAsync(
        IReadOnlyList<(string Name, Service Service)> services,
        CancellationToken signal,
        CancellationToken force,
        ServiceHooks hooks
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(hooks);
        var started = new List<StartedService>();
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        var run = new RunServices(started, values);
        try
        {
            foreach (var (name, service) in services)
            {
                if (signal.IsCancellationRequested)
                {
                    break;
                }

                hooks.Starting?.Invoke(name);

                // A handler of the starting event can cancel the run.
                if (signal.IsCancellationRequested)
                {
                    break;
                }

                var startedAt = DateTime.UtcNow;
                var value = await StartServiceAsync(name, service, signal, started).ConfigureAwait(false);
                if (value is null)
                {
                    break;
                }

                values[name] = value;
                hooks.Ready?.Invoke(name, DateTime.UtcNow - startedAt);
            }

            return run;
        }
        catch
        {
            await run.StopAsync(failure => hooks.StopFailed?.Invoke(failure), force).ConfigureAwait(false);
            throw;
        }
    }

    // Runs one service's iterator up to its first yield. The service is added to `started` as soon as the iterator is created, so cleanup
    // still resumes it if it yields after the runner stops waiting. Returns the yielded value, or null when `signal` was cancelled first.
    private static async Task<object?> StartServiceAsync(
        string name,
        Service service,
        CancellationToken signal,
        List<StartedService> started
    )
    {
        var limit = service.StartupTimeout ?? DefaultStartupTimeout;
        var controller = new CancellationTokenSource();
        IAsyncEnumerator<object> enumerator;
        try
        {
            enumerator = service.Start(new ServiceStartContext(controller.Token)).GetAsyncEnumerator(controller.Token);
        }
        catch (Exception cause)
        {
            controller.Dispose();
            throw StartFailure(name, cause);
        }

        var first = Task.Run(async () =>
        {
            try
            {
                return await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (Exception cause)
            {
                throw StartFailure(name, cause);
            }
        });
        var entry = new StartedService(
            name,
            enumerator,
            controller,
            first,
            service.ShutdownTimeout ?? DefaultShutdownTimeout
        );
        lock (started)
        {
            started.Add(entry);
        }

        var timedOut = false;
        using var timer = new Timer(
            _ =>
            {
                timedOut = true;
                entry.Cancel();
            },
            null,
            limit,
            Timeout.InfiniteTimeSpan
        );
        using var cancel = signal.Register(entry.Cancel);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var abortRegistration = controller.Token.Register(() => aborted.TrySetResult());
        var result = await Task.WhenAny(first, aborted.Task).ConfigureAwait(false);
        if (result != first)
        {
            if (signal.IsCancellationRequested)
            {
                return null;
            }

            throw timedOut
                ? new EngineException(
                    "SERVICE_START_FAILED",
                    "service \"" + name + "\" didn't yield within " + Seconds(limit) + " seconds"
                )
                : new EngineException("SERVICE_START_FAILED", "service \"" + name + "\" was cancelled while starting");
        }

        if (!await first.ConfigureAwait(false))
        {
            throw new EngineException(
                "SERVICE_START_FAILED",
                "service \""
                    + name
                    + "\" finished without yielding. Yield a value once between the startup code and the stop code"
            );
        }

        return enumerator.Current
            ?? throw new ConfigurationException(
                "INVALID_SERVICE_VALUE",
                "service \"" + name + "\" yielded null. Yield an object, like new { Url = url }"
            );
    }

    // Resumes one service's iterator after its yield, so the code there runs, and waits for it to finish within the service's shutdown timeout
    // (upstream's `stopService`).
    private static async Task StopServiceAsync(StartedService service, CancellationToken force)
    {
        var limit = service.ShutdownTimeout;
        var resumed = false;
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var forced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var forceRegistration = force.Register(() =>
        {
            service.Cancel();
            forced.TrySetResult();
        });
        using var timer = new Timer(
            _ =>
            {
                service.Cancel();
                expired.TrySetResult();
            },
            null,
            limit,
            Timeout.InfiniteTimeSpan
        );

        async Task<T> UntilStopped<T>(Task<T> task)
        {
            Task finished;
            try
            {
                finished = await Task.WhenAny(task, expired.Task).ConfigureAwait(false);
                if (finished == task)
                {
                    return await task.ConfigureAwait(false);
                }
            }
            catch (Exception cause)
            {
                throw new EngineException(
                    "SERVICE_STOP_FAILED",
                    "service \"" + service.Name + "\" failed to stop: " + cause.Message,
                    cause
                );
            }

            _ = task.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            throw new EngineException(
                "SERVICE_STOP_FAILED",
                "service \"" + service.Name + "\" didn't stop within " + Seconds(limit) + " seconds"
            );
        }

        try
        {
            var outcome = service.First.ContinueWith(
                static t => t.Status == TaskStatus.RanToCompletion && t.Result,
                TaskScheduler.Default
            );

            // A service that has not yielded has no stop code to run yet, so running out of time here is not a stop failure: the cleanup in
            // `finally` still runs it if the service yields later.
            bool yielded;
            if (service.First.IsCompleted)
            {
                yielded = await UntilStopped(outcome).ConfigureAwait(false);
            }
            else
            {
                var winner = await Task.WhenAny(outcome, forced.Task, expired.Task).ConfigureAwait(false);
                yielded = winner == outcome && await outcome.ConfigureAwait(false);
            }

            if (!yielded)
            {
                return;
            }

            resumed = true;
            var more = await UntilStopped(service.Enumerator.MoveNextAsync().AsTask()).ConfigureAwait(false);
            if (more)
            {
                await UntilStopped(
                        service.Enumerator.DisposeAsync().AsTask().ContinueWith(_ => true, TaskScheduler.Default)
                    )
                    .ConfigureAwait(false);
                throw new EngineException(
                    "SERVICE_STOP_FAILED",
                    "service \""
                        + service.Name
                        + "\" yielded a second time. Yield only once, and put stop code after the yield"
                );
            }
        }
        finally
        {
            // The runner stopped waiting before the service yielded. If it yields later, its stop code still runs, so whatever it started is
            // released.
            if (!resumed)
            {
                _ = service.First.ContinueWith(
                    async t =>
                    {
                        try
                        {
                            if (t.Status == TaskStatus.RanToCompletion && t.Result)
                            {
                                if (await service.Enumerator.MoveNextAsync().ConfigureAwait(false))
                                {
                                    await service.Enumerator.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                        }
                        catch (Exception cause)
                        {
                            // Nobody is left to report to: the run stopped waiting for this service.
                            System.Diagnostics.Debug.WriteLine(
                                "service " + service.Name + " failed to stop late: " + cause.Message
                            );
                        }
                    },
                    TaskScheduler.Default
                );
            }
        }
    }

    private static EngineException StartFailure(string name, Exception cause) =>
        new("SERVICE_START_FAILED", "service \"" + name + "\" failed to start: " + cause.Message, cause);

    private static string Seconds(TimeSpan span) =>
        span.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(60);

    internal static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Tracks a service iterator that has been created so it can be resumed on stop (upstream's <c>StartedService</c>).</summary>
    internal sealed class StartedService(
        string name,
        IAsyncEnumerator<object> enumerator,
        CancellationTokenSource controller,
        Task<bool> first,
        TimeSpan shutdownTimeout
    ) : IDisposable
    {
        public string Name { get; } = name;

        public IAsyncEnumerator<object> Enumerator { get; } = enumerator;

        /// <summary>The result of the first <c>MoveNextAsync</c>. Stays pending if startup was cancelled before yielding.</summary>
        public Task<bool> First { get; } = first;

        public TimeSpan ShutdownTimeout { get; } = shutdownTimeout;

        public void Cancel()
        {
            try
            {
                controller.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Stopped already: there is nothing left to cancel.
                return;
            }
        }

        public void Dispose()
        {
            // The first MoveNext may still be running: the controller outlives it only until it settles.
            _ = First.ContinueWith(_ => controller.Dispose(), TaskScheduler.Default);
        }
    }
}
