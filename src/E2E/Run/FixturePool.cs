// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace E2E;

/// <summary>Reads one built-in fixture by name, off whatever attempt runs the code (upstream's <c>BuiltInReader</c>, <c>run/fixture-pool.ts</c>).</summary>
internal delegate object? BuiltInReader(string name);

/// <summary>One set-up fixture's teardown: the rest of its function after <c>use</c> (upstream's <c>FixtureTeardown</c>).</summary>
internal record FixtureTeardown(string Name, Func<Task> Run);

/// <summary>
/// A worker fixture's teardown, which runs when the worker ends, outside any test (upstream's <c>WorkerFixtureTeardown</c>). <see cref="UsesEngine"/> says
/// whether the fixture names an engine-backed built-in, itself or through a fixture it names: its teardown then needs an attempt.
/// </summary>
internal sealed record WorkerFixtureTeardown(string Name, Func<Task> Run, bool UsesEngine) : FixtureTeardown(Name, Run);

/// <summary>
/// What a worker fixture holds for a built-in that needs an attempt: a forwarder to the attempt or suite hook running at the time it is read, never one attempt's
/// object (upstream's <c>forwarder</c> of <c>WorkerFixturePool</c>, a <c>Proxy</c> over the running attempt's fixture). .NET-only: C# has no proxy over an
/// object of any type, so a member is read and a method called by name through <see cref="Get"/> and <see cref="Call"/>, and <see cref="Current"/> is the object itself.
/// </summary>
internal sealed class ForwardedBuiltIn(string name, Func<object> resolve)
{
    /// <summary>The built-in's name.</summary>
    public string Name { get; } = name;

    /// <summary>The object the running attempt holds under <see cref="Name"/> now; throws when no test or suite hook is running.</summary>
    public object Current => resolve();

    /// <summary>Reads the property or field <paramref name="member"/> off <see cref="Current"/>.</summary>
    public object? Get(string member)
    {
        var target = Current;
        var type = target.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        if (type.GetProperty(member, flags) is { } property)
        {
            return property.GetValue(target);
        }

        var field = type.GetField(member, flags) ?? throw new MissingMemberException(type.FullName, member);
        return field.GetValue(target);
    }

    /// <summary>Calls the method <paramref name="member"/> of <see cref="Current"/> with <paramref name="args"/>.</summary>
    public object? Call(string member, params object?[] args)
    {
        var target = Current;
        var type = target.GetType();
        var method = type.GetMethod(
            member,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            args.Select(arg => arg?.GetType() ?? typeof(object)).ToArray(),
            modifiers: null
        ) ?? throw new MissingMethodException(type.FullName, member);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}

/// <summary>
/// One pool for every fixture a piece of test code reads: the built-in ones (<c>agent</c>, <c>app</c>, <c>screen</c>, <c>platform</c>, <c>request</c>, <c>services</c>,
/// <c>session</c>), the target engine's, and the <c>test.extend()</c> chain it was registered through (upstream's <c>run/fixture-pool.ts</c>). Each has a scope, and code
/// sees the fixtures that live at least as long as it does: a test and its per-test hooks see test and worker fixtures, a suite hook and a worker fixture see worker
/// fixtures only. A chain fixture is set up on demand: for code that names it, for a fixture that names it, or because it is <c>auto</c>. The plan
/// (<see cref="FixturePlanner.PlanAttempt"/>, <see cref="FixturePlanner.PlanSuiteHook"/>) decides all of that before anything runs; the pool sets up what it lists and hands
/// each function the fixtures it names.
/// </summary>
internal static class FixturePool
{
    /// <summary>The built-in fixtures of an attempt, read lazily off what the attempt built for it (upstream's <c>attemptBuiltIns</c>); a name it does not hold reads as null.</summary>
    public static BuiltInReader AttemptBuiltIns(IReadOnlyDictionary<string, Func<object?>> fixtures) =>
        name => fixtures.TryGetValue(name, out var read) ? read() : null;

    /// <summary>The built-in fixtures of an attempt that are plain values; a name it does not hold reads as null.</summary>
    public static BuiltInReader AttemptBuiltIns(IReadOnlyDictionary<string, object?> fixtures) =>
        name => fixtures.GetValueOrDefault(name);

    /// <summary>
    /// Returns a built-in fixture reader for code running without an attempt. Only fixtures derived from the target or worker (<c>platform</c> and <c>services</c>) are
    /// available. Reading any other fixture throws the error returned by <paramref name="unavailable"/> (upstream's <c>attemptlessBuiltIns</c>).
    /// </summary>
    public static BuiltInReader AttemptlessBuiltIns(
        BuiltInTarget target,
        IReadOnlyDictionary<string, object?> services,
        Func<string, Exception> unavailable
    )
    {
        var builtIns = BuiltInFixtures.Of(target, services);
        return name => builtIns.NeedsAttempt(name) ? throw unavailable(name) : builtIns.Attemptless(name);
    }

    /// <summary>A fixture's setup as <c>StepRecorder.owner</c> names it (upstream's <c>fixtureSetup</c>).</summary>
    public static string FixtureSetup(string fixture) => $"the fixture \"{fixture}\" setup";

    /// <summary>
    /// Runs one fixture function until it hands its value to <c>use</c>. <paramref name="accept"/> receives the value and the fixture's teardown synchronously, at <c>use</c>; it
    /// returns false when nobody can take the value anymore (the attempt that asked gave up), and the function then runs on detached with <c>use</c> resolved at once. Completes
    /// once the value was handed over; fails with the function's failure, or when it returned without calling <c>use</c>. <paramref name="outlivesTest"/> says the fixture is a worker's:
    /// set up inside the first test that names it, it is no part of that test (upstream's <c>startFixture</c>).
    /// </summary>
    internal static Task StartFixtureAsync(
        FixtureDefinition definition,
        FixtureValues fixtures,
        Func<object?, FixtureTeardown, bool> accept,
        bool outlivesTest = false
    )
    {
        var name = definition.Name;
        var provided = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var used = false;
        ConfigurationException? misuse = null;
        // The function's polls are its user's until use returns, then its teardown's.
        var polls = PollScopes.PollLineage(outlivesTest);

        Task Use(object? value)
        {
            if (used)
            {
                misuse = new ConfigurationException(
                    "TEST_SETUP_FAILED",
                    $"fixture \"{name}\" called use() twice; a fixture hands one value per instance"
                );
                // The violation is reported at teardown whether or not the fixture awaits this.
                return Task.FromException(misuse);
            }

            used = true;
            var teardown = new FixtureTeardown(name, async () =>
            {
                polls.HandOver();
                release.TrySetResult();
                await finished.Task.ConfigureAwait(false);
                if (misuse is not null)
                {
                    throw misuse;
                }
            });
            if (!accept(value, teardown))
            {
                // Nobody can take the value, and holding the function here would keep whatever it allocated alive forever.
                release.TrySetResult();
            }

            provided.TrySetResult();
            return release.Task;
        }

        // On the thread pool and with no attempt in its flow: a host the fixture launches belongs to the worker.
        _ = Task.Run(async () =>
        {
            try
            {
                await polls.Run(() => definition.Fn(fixtures, Use)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                provided.TrySetException(ex);
                finished.TrySetException(ex);
                return;
            }

            if (!used)
            {
                provided.TrySetException(
                    new ConfigurationException(
                        "TEST_SETUP_FAILED",
                        $"fixture \"{name}\" returned without calling use(); a fixture awaits use(value) to hand its value over"
                    )
                );
            }

            finished.TrySetResult();
        });
        // A fixture whose setup failed is never torn down, and one released detached runs on alone, so nothing else would observe its failure; a torn-down one is awaited by its teardown.
        _ = finished.Task.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return provided.Task;
    }

    /// <summary>
    /// Binds one plan to the code about to run with it: a test attempt, or a suite hook. Every function, a fixture's included, receives the fixtures it names: the
    /// value a chain fixture handed over, or a built-in read off the attempt when the function reads it (upstream's <c>bindFixtures</c>).
    /// </summary>
    public static BoundFixtures BindFixtures(BindOptions options) => new(options);

    /// <summary>
    /// Plans the fixtures of one attempt of <paramref name="test"/> on <paramref name="target"/>, resolved as one unit over its body and <paramref name="hooks"/>, before any session
    /// exists: a fixture rule one of them breaks throws here. <c>Open</c> binds the plan to the attempt once its built-in fixtures exist (upstream's <c>planAttemptFixtures</c>).
    /// </summary>
    public static PlannedAttemptFixtures PlanAttemptFixtures(
        BuiltInTarget target,
        RegisteredTest test,
        AttemptHooks hooks
    ) => new(FixturePlanner.PlanAttempt(target, test, [.. hooks.BeforeEach, .. hooks.AfterEach]), test, hooks);
}

/// <summary>The <c>beforeEach</c> and <c>afterEach</c> hooks that apply to one test, each list in the order it runs (upstream's <c>AttemptHooks</c>).</summary>
internal sealed record AttemptHooks(IReadOnlyList<TestHook> BeforeEach, IReadOnlyList<TestHook> AfterEach);

/// <summary>One step of an attempt's cleanup: a label for its timeout, and what to run within its own cleanup budget (upstream's <c>AttemptCleanup</c>, renamed: the report's <c>AttemptCleanup</c> enum owns the name here).</summary>
internal sealed record AttemptCleanupStep(string Label, Func<Task> Run);

/// <summary>What <see cref="FixturePool.PlanAttemptFixtures"/> planned; <see cref="Open"/> binds the plan to the attempt (upstream's returned <c>{ open }</c>).</summary>
internal sealed class PlannedAttemptFixtures(FixturePlan plan, RegisteredTest test, AttemptHooks hooks)
{
    /// <summary>Binds the plan to one attempt, once its <paramref name="builtIns"/> exist.</summary>
    public TestAttemptFixtures Open(BuiltInReader builtIns, WorkerFixturePool pool, CancellationToken signal) =>
        new(FixturePool.BindFixtures(new BindOptions(plan, builtIns, pool, signal)), builtIns, pool, test, hooks);
}

/// <summary>The fixtures of one test attempt, bound to its built-ins (upstream's <c>TestAttemptFixtures</c>).</summary>
internal sealed class TestAttemptFixtures(
    BoundFixtures bound,
    BuiltInReader read,
    WorkerFixturePool pool,
    RegisteredTest test,
    AttemptHooks hooks
)
{
    /// <summary>Makes this attempt the one worker fixtures read their built-ins off, until the returned action is called.</summary>
    public Action Enter() => pool.Enter(read);

    /// <summary>
    /// Sets the fixtures up, runs the <c>beforeEach</c> hooks, then the body. <paramref name="running"/> is told the phase about to run and the code that runs in it, as
    /// <c>StepRecorder.owner</c> names it; <paramref name="signal"/> stops the sequence between two of them.
    /// </summary>
    public async Task RunAsync(Action<string, string> running, CancellationToken signal)
    {
        running("beforeEach", "the beforeEach hook");
        await bound
            .SetUpAsync(fixture => running("beforeEach", FixturePool.FixtureSetup(fixture)))
            .ConfigureAwait(false);
        signal.ThrowIfCancellationRequested();
        foreach (var hook in hooks.BeforeEach)
        {
            running("beforeEach", "the beforeEach hook");
            await bound.CallAsync(hook).ConfigureAwait(false);
            signal.ThrowIfCancellationRequested();
        }

        running("body", "the test body");
        await bound.CallAsync(test).ConfigureAwait(false);
    }

    /// <summary>
    /// What the attempt leaves to clean up: the <c>afterEach</c> hooks, then the test fixtures' teardowns, last set up first. A hook that names a fixture whose setup failed
    /// is left out. Taking the list ends fixture setup.
    /// </summary>
    public IReadOnlyList<AttemptCleanupStep> Cleanup() =>
        [
            .. hooks
                .AfterEach.Where(bound.Ready)
                .Select(hook => new AttemptCleanupStep($"{hook.Kind} hook", () => bound.CallAsync(hook))),
            .. bound
                .Teardowns()
                .Select(teardown => new AttemptCleanupStep($"fixture \"{teardown.Name}\" teardown", teardown.Run)),
        ];
}

/// <summary>What <see cref="FixturePool.BindFixtures"/> binds (upstream's <c>BindOptions</c>).</summary>
/// <param name="Plan">What to set up, and what each function receives: <c>planAttempt</c> or <c>planSuiteHook</c>.</param>
/// <param name="BuiltIns">The built-in fixtures of the attempt the code runs in; see <see cref="FixturePool.AttemptlessBuiltIns"/>.</param>
/// <param name="Pool">The worker's fixtures.</param>
/// <param name="Signal">The attempt's abort signal: setup stops once it fires.</param>
internal sealed record BindOptions(
    FixturePlan Plan,
    BuiltInReader BuiltIns,
    WorkerFixturePool Pool,
    CancellationToken Signal = default
);

/// <summary>The fixtures set up for one piece of user code, and the way to run that code with them (upstream's <c>BoundFixtures</c>).</summary>
internal sealed class BoundFixtures
{
    private readonly FixturePlan _plan;
    private readonly BuiltInReader _builtIns;
    private readonly WorkerFixturePool _pool;
    private readonly CancellationToken _signal;

    /// <summary>The value each fixture handed over.</summary>
    private readonly Dictionary<FixtureDefinition, object?> _provided = new(ReferenceEqualityComparer.Instance);
    private readonly List<FixtureTeardown> _active = [];
    private readonly CancellationTokenRegistration _registration;
    private volatile bool _abandoned;

    internal BoundFixtures(BindOptions options)
    {
        _plan = options.Plan;
        _builtIns = options.BuiltIns;
        _pool = options.Pool;
        _signal = options.Signal;
        _abandoned = _signal.IsCancellationRequested;
        if (!_abandoned)
        {
            _registration = _signal.Register(() => _abandoned = true);
        }
    }

    /// <summary>
    /// Sets up the fixtures the plan lists, in its order: a worker fixture is taken from the pool (set up there on first use), a test fixture set up for this attempt. Fails
    /// with the first failure; the test fixtures set up before it still get torn down. <paramref name="starting"/> hears each fixture's name right before its setup runs.
    /// </summary>
    public async Task SetUpAsync(Action<string>? starting = null)
    {
        RequireActive();
        foreach (var planned in _plan.Order)
        {
            starting?.Invoke(planned.Definition.Name);
            await SetUpOneAsync(planned).ConfigureAwait(false);
            RequireActive();
        }
    }

    /// <summary>Runs the test body or hook <paramref name="user"/> with the fixtures it names, and no others. A built-in one is acquired as the function reads it.</summary>
    public async Task CallAsync(FixtureUser user) =>
        await user.Fn(Receive(InputsOf(user), _builtIns)).ConfigureAwait(false);

    /// <summary>Whether every chain fixture <paramref name="user"/> names was set up. A hook that names one whose setup failed, or was never reached, has nothing to run with.</summary>
    public bool Ready(FixtureUser user) =>
        InputsOf(user).Values.All(definition => definition is null || _provided.ContainsKey(definition));

    /// <summary>
    /// The teardowns of the test fixtures whose setup completed, last set up first. Taking them ends setup, as does the signal: a test fixture still running that reaches
    /// <c>use</c> afterwards finds no attempt to hand its value to, so <c>use</c> resolves at once and its teardown runs detached. Worker fixtures are the pool's to tear down.
    /// </summary>
    public IReadOnlyList<FixtureTeardown> Teardowns()
    {
        _abandoned = true;
        _registration.Dispose();
        lock (_active)
        {
            return [.. Enumerable.Reverse(_active)];
        }
    }

    private FixtureValues Receive(FixtureInputs inputs, BuiltInReader builtIn)
    {
        var fixtures = new FixtureValues();
        foreach (var (name, definition) in inputs)
        {
            if (definition is null)
            {
                fixtures.Add(name, () => builtIn(name));
            }
            else
            {
                fixtures.Add(name, () => _provided.GetValueOrDefault(definition));
            }
        }

        return fixtures;
    }

    private FixtureInputs InputsOf(FixtureUser user) =>
        _plan.Inputs.TryGetValue(user, out var inputs)
            ? inputs
            : throw new InvalidOperationException("the fixture plan does not cover this test body or hook");

    /// <summary>Stops setup once its attempt has given up on it.</summary>
    private void RequireActive()
    {
        if (_abandoned)
        {
            throw new ConfigurationException(
                "TEST_SETUP_FAILED",
                "fixture setup ended after the attempt was abandoned"
            );
        }
    }

    private async Task SetUpOneAsync(PlannedFixture planned)
    {
        var (definition, scope, usesEngine, inputs) = planned;
        if (scope == FixtureScope.Worker)
        {
            // A worker fixture outlives this attempt, so a built-in reaches it through the pool, never as this attempt's object.
            var value = await _pool
                .AcquireAsync(definition, Receive(inputs, _pool.BuiltIn), usesEngine)
                .ConfigureAwait(false);
            lock (_active)
            {
                _provided[definition] = value;
            }

            return;
        }

        await FixturePool
            .StartFixtureAsync(definition, Receive(inputs, _builtIns), (value, teardown) =>
            {
                if (_abandoned)
                {
                    return false;
                }

                lock (_active)
                {
                    _provided[definition] = value;
                    _active.Add(teardown);
                }

                return true;
            })
            .ConfigureAwait(false);
    }
}

/// <summary>
/// The worker-scoped <c>test.extend()</c> fixtures of one worker and target: one instance per fixture, set up the first time a test or suite hook of its chain needs it,
/// and torn down when the worker ends. A module that is evaluated again defines its fixtures again, and they keep their instance: a fixture is known by its
/// <c>identity</c>, and by the definition itself only where it has none (upstream's <c>WorkerFixturePool</c>, <c>run/fixture-pool.ts</c>).
/// </summary>
internal sealed class WorkerFixturePool
{
    private readonly Dictionary<object, Task<object?>> _instances = [];
    private readonly List<WorkerFixtureTeardown> _active = [];
    private readonly BuiltInFixtures _builtInFixtures;
    private readonly object _gate = new();
    private bool _closed;

    /// <summary>The built-in fixtures of the attempt or suite hook running now; see <see cref="Enter"/>.</summary>
    private BuiltInReader? _current;

    /// <param name="target">The target the worker runs.</param>
    /// <param name="services">The service values started by the worker's entry point for the <c>services</c> fixture.</param>
    public WorkerFixturePool(BuiltInTarget target, IReadOnlyDictionary<string, object?>? services = null) =>
        _builtInFixtures = BuiltInFixtures.Of(target, services);

    /// <summary>
    /// Makes <paramref name="builtIns"/> the fixtures a worker fixture's built-ins forward to, until the returned action restores the previous ones. A worker fixture outlives
    /// the attempt it was set up in, so it holds a forwarder for <c>device</c>, <c>app</c>, and <c>screen</c>, never one attempt's object: the steps it takes land in the record of whatever runs at the time.
    /// </summary>
    public Action Enter(BuiltInReader builtIns)
    {
        var previous = _current;
        _current = builtIns;
        return () => _current = previous;
    }

    /// <summary>
    /// The value of <paramref name="definition"/>, set up on first use with <paramref name="fixtures"/>, the worker fixtures it names. A setup that fails is forgotten, so the next test to
    /// need the fixture tries again; one still running is shared by everyone who asks meanwhile. <paramref name="usesEngine"/> is the plan's word on whether the fixture reaches the engine,
    /// kept for its teardown.
    /// </summary>
    public Task<object?> AcquireAsync(FixtureDefinition definition, FixtureValues fixtures, bool usesEngine)
    {
        object key = (object?)definition.Identity ?? definition;
        Task<object?> instance;
        lock (_gate)
        {
            if (_instances.TryGetValue(key, out var known))
            {
                return known;
            }

            instance = SetUpAsync();
            _instances[key] = instance;
        }

        _ = instance.ContinueWith(
            _ =>
            {
                lock (_gate)
                {
                    if (_instances.TryGetValue(key, out var current) && ReferenceEquals(current, instance))
                    {
                        _instances.Remove(key);
                    }
                }
            },
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
        );
        return instance;

        async Task<object?> SetUpAsync()
        {
            object? value = null;
            await FixturePool
                .StartFixtureAsync(
                    definition,
                    fixtures,
                    (provided, teardown) =>
                    {
                        lock (_gate)
                        {
                            if (_closed)
                            {
                                return false;
                            }

                            value = provided;
                            _active.Add(new WorkerFixtureTeardown(teardown.Name, teardown.Run, usesEngine));
                            return true;
                        }
                    },
                    outlivesTest: true
                )
                .ConfigureAwait(false);
            return value;
        }
    }

    /// <summary>
    /// Ends the pool: the teardowns of every fixture whose setup completed, last set up first. A setup still running when the pool closes finds nobody to hand its value to,
    /// and its teardown runs detached.
    /// </summary>
    public IReadOnlyList<WorkerFixtureTeardown> Close()
    {
        lock (_gate)
        {
            _closed = true;
            _instances.Clear();
            var teardowns = new List<WorkerFixtureTeardown>(_active);
            _active.Clear();
            teardowns.Reverse();
            return teardowns;
        }
    }

    /// <summary>
    /// Returns built-in fixture <paramref name="name"/> for a worker fixture. If the value comes from the target or worker, it's returned directly. Otherwise, it returns a
    /// <see cref="ForwardedBuiltIn"/> that forwards to the attempt or suite hook running at the time.
    /// </summary>
    public object? BuiltIn(string name) =>
        _builtInFixtures.NeedsAttempt(name) ? Forwarder(name) : _builtInFixtures.Attemptless(name);

    /// <summary>An object that reads <paramref name="name"/> off the running attempt's fixtures at each access.</summary>
    private ForwardedBuiltIn Forwarder(string name) =>
        new(name, () =>
            (
                _current
                ?? throw new ConfigurationException(
                    "UNSUPPORTED_CAPABILITY",
                    $"a worker fixture used \"{name}\" when no test or suite hook was running"
                )
            )(name)
            ?? throw new ConfigurationException(
                "UNSUPPORTED_CAPABILITY",
                $"the running test or suite hook holds no \"{name}\""
            )
        );
}
