// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using E2E.Engine;
using E2E.Internal;
using Pool = E2E.FixturePool;

namespace E2E.Tests.FixturePool;

/// <summary>The helpers of upstream's <c>tests/unit/fixture-pool.test.ts</c>, shared by the classes that port its <c>describe</c> blocks.</summary>
internal static class FixturePoolSupport
{
    /// <summary>What each test body or hook built here was handed when the pool called it.</summary>
    private static readonly ConditionalWeakTable<TestBody, Dictionary<string, object?>> Received = new();

    /// <summary>A log several threads write to: a fixture's function runs on the thread pool.</summary>
    internal sealed class Log
    {
        private readonly List<string> _lines = [];

        public void Add(string line)
        {
            lock (_lines)
            {
                _lines.Add(line);
            }
        }

        public List<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }
    }

    /// <summary>The options of <see cref="Fixture"/>: <c>Scope</c> and <c>Auto</c> default to <c>test</c> and false; null spells out upstream's <c>undefined</c>, a plain function that declares none.</summary>
    internal sealed record FixtureOptions(
        string? Scope = FixtureScope.Test,
        bool? Auto = false,
        int Layer = 0,
        string[]? Uses = null
    );

    /// <summary>A web-like target, whose attempts are isolated, or a device-like one, whose state outlives them (upstream's <c>toyTarget</c>).</summary>
    internal static BuiltInTarget ToyTarget(string isolation = EngineIsolation.Attempt) =>
        new(
            "toy",
            "toy",
            new Dictionary<string, string>(),
            new ResolvedEngine("toy", new HashSet<string>(), null, isolation, ["gadget"])
        );

    /// <summary>A device-like built-in, which a worker fixture reaches through <see cref="ForwardedBuiltIn"/>.</summary>
    internal sealed class Gadget(string id)
    {
        public string Id() => id;
    }

    internal static FixtureDefinition Fixture(
        string name,
        Func<FixtureValues, Func<object?, Task>, Task> run,
        FixtureOptions? options = null
    )
    {
        options ??= new FixtureOptions();
        return new FixtureDefinition
        {
            Name = name,
            Scope = options.Scope,
            Auto = options.Auto,
            Layer = options.Layer,
            Fn = (fixtures, use) => run(fixtures, use),
            Names = options.Uses ?? [],
            Source = new SourceLocation("/repo/tests/support.ts", 7, 1),
        };
    }

    /// <summary>A fixture that logs its setup, hands <paramref name="value"/> over, and logs its teardown.</summary>
    internal static FixtureDefinition Logged(Log log, string name, object? value, FixtureOptions? options = null) =>
        Fixture(
            name,
            async (_, use) =>
            {
                log.Add($"setup:{name}");
                await use(value);
                log.Add($"teardown:{name}");
            },
            options
        );

    /// <summary>A body that keeps what it is handed, reading every fixture the way destructuring them all would.</summary>
    internal static TestBody Receiving()
    {
        TestBody fn = null!;
        fn = fixtures =>
        {
            Received.AddOrUpdate(fn, fixtures.ToDictionary(pair => pair.Key, pair => pair.Value));
            return Task.CompletedTask;
        };
        return fn;
    }

    /// <summary>A test whose body names <paramref name="names"/>, registered through <paramref name="chain"/>.</summary>
    internal static RegisteredTest TestNaming(
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<string> names,
        TestBody? fn = null
    ) =>
        new()
        {
            Kind = "test",
            TitlePath = ["suite", "case"],
            Fn = fn ?? Receiving(),
            Names = names,
            Source = new SourceLocation("/repo/tests/a.e2e.ts", 3, 1),
            Fixtures = chain,
        };

    internal static TestHook TestHookNaming(
        string kind,
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<string> names
    ) =>
        new()
        {
            Kind = kind,
            Fn = Receiving(),
            Names = names,
            Fixtures = chain,
            Source = new SourceLocation("/repo/tests/a.e2e.ts", 9, 1),
        };

    internal static SuiteHook SuiteHookNaming(
        string kind,
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<string> names
    ) =>
        new()
        {
            Kind = kind,
            Fn = Receiving(),
            Names = names,
            Fixtures = chain,
            Source = new SourceLocation("/repo/tests/a.e2e.ts", 9, 1),
        };

    internal static SuiteHook SuiteHook(IReadOnlyList<FixtureDefinition> chain, IReadOnlyList<string> names) =>
        SuiteHookNaming("beforeAll", chain, names);

    /// <summary>Has the pool call <paramref name="user"/>, and returns the fixtures it was handed.</summary>
    internal static async Task<Dictionary<string, object?>> Call(BoundFixtures bound, FixtureUser user)
    {
        await bound.CallAsync(user);
        return Received.TryGetValue(user.Fn, out var received)
            ? received
            : throw new InvalidOperationException("not called");
    }

    internal static List<string> PlannedNames(FixturePlan plan) =>
        [.. plan.Order.Select(planned => planned.Definition.Name)];

    internal static string? PlannedScope(FixturePlan plan, FixtureDefinition definition) =>
        plan.Order.FirstOrDefault(planned => ReferenceEquals(planned.Definition, definition))?.Scope;

    internal static BuiltInReader Toy() =>
        Pool.AttemptBuiltIns(new Dictionary<string, object?> { ["platform"] = "toy" });

    /// <summary>A dictionary spelled like upstream's object literal.</summary>
    internal static Dictionary<string, object?> Obj(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    internal sealed record AttemptOptions
    {
        public WorkerFixturePool? Pool { get; init; }
        public CancellationToken Signal { get; init; }
        public BuiltInTarget? Target { get; init; }
        public BuiltInReader? BuiltIns { get; init; }
        public IReadOnlyList<TestHook> Hooks { get; init; } = [];
    }

    /// <summary>
    /// Binds <paramref name="chain"/> for one attempt of a test that names <paramref name="names"/>, with <c>BuiltIns</c> standing in for the attempt's own fixtures.
    /// <c>Body()</c> has the pool call the test body and returns what it was handed.
    /// </summary>
    internal static AttemptBinding Attempt(
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<string> names,
        AttemptOptions? options = null
    )
    {
        options ??= new AttemptOptions();
        var target = options.Target ?? ToyTarget();
        var test = TestNaming(chain, names);
        var bound = Pool.BindFixtures(
            new BindOptions(
                FixturePlanner.PlanAttempt(target, test, options.Hooks),
                options.BuiltIns ?? Toy(),
                options.Pool ?? new WorkerFixturePool(target),
                options.Signal
            )
        );
        return new AttemptBinding(bound, test);
    }

    internal sealed class AttemptBinding(BoundFixtures bound, RegisteredTest test)
    {
        public BoundFixtures Bound => bound;

        public Task SetUpAsync() => bound.SetUpAsync();

        public IReadOnlyList<FixtureTeardown> Teardowns() => bound.Teardowns();

        public bool Ready(FixtureUser user) => bound.Ready(user);

        public Task<Dictionary<string, object?>> Body() => Call(bound, test);
    }

    /// <summary>Asserts that <paramref name="plan"/> throws upstream's <c>COLLECTION_ERROR</c> with a message containing <paramref name="message"/>.</summary>
    internal static void AssertPlanError(Action plan, string message)
    {
        var error = Assert.Throws<FixtureUseException>(plan);
        Assert.Equal("COLLECTION_ERROR", error.Code);
        Assert.Contains(message, error.Message);
    }

    /// <summary>Upstream's <c>vi.waitFor</c>: retries <paramref name="assertion"/> until it holds, for at most five seconds.</summary>
    internal static async Task WaitFor(Action assertion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }
}
