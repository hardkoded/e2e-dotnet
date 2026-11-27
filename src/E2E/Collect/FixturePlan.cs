// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>What each function of a piece of test code receives: each fixture it names, and what the name stands for, a <c>test.extend()</c> fixture or, when null, a built-in (upstream's <c>FixtureInputs</c>).</summary>
internal sealed class FixtureInputs : IReadOnlyList<KeyValuePair<string, FixtureDefinition?>>
{
    private readonly List<KeyValuePair<string, FixtureDefinition?>> _entries = [];

    /// <summary>The names, in the order the function names them.</summary>
    public IEnumerable<string> Keys => _entries.Select(entry => entry.Key);

    /// <summary>What each name stands for.</summary>
    public IEnumerable<FixtureDefinition?> Values => _entries.Select(entry => entry.Value);

    /// <summary>How many fixtures the function names.</summary>
    public int Count => _entries.Count;

    /// <inheritdoc />
    public KeyValuePair<string, FixtureDefinition?> this[int index] => _entries[index];

    /// <summary>Records that <paramref name="name"/> stands for <paramref name="definition"/>, or a built-in when null; a name set twice keeps its place and takes the last value.</summary>
    public void Set(string name, FixtureDefinition? definition)
    {
        var index = _entries.FindIndex(entry => entry.Key == name);
        if (index < 0)
        {
            _entries.Add(KeyValuePair.Create(name, definition));
        }
        else
        {
            _entries[index] = KeyValuePair.Create(name, definition);
        }
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, FixtureDefinition?>> GetEnumerator() => _entries.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>One fixture a plan sets up (upstream's <c>PlannedFixture</c>).</summary>
internal sealed record PlannedFixture(
    FixtureDefinition Definition,
    string Scope,
    bool UsesEngine,
    FixtureInputs Inputs
);

/// <summary>What to set up for one piece of test code, what each of its functions receives, and whether it reaches the engine (upstream's <c>FixturePlan</c>).</summary>
internal sealed record FixturePlan(
    IReadOnlyList<PlannedFixture> Order,
    IReadOnlyDictionary<FixtureUser, FixtureInputs> Inputs,
    bool UsesEngine
);

/// <summary>
/// Which fixtures a piece of test code needs, and in what order: the ones its functions name, the ones those fixtures name in turn, and the <c>auto</c> ones
/// (upstream's <c>collect/fixture-plan.ts</c>). Planning runs no fixture, so the rules it enforces (a name some target has, a suite hook or worker fixture
/// that reads worker fixtures only, no cycle) fail at collection, and again in the worker before anything is set up.
/// </summary>
internal static class FixturePlanner
{
    private const string ScopeRule =
        "beforeAll, afterAll, and worker fixtures see worker fixtures only, since a test fixture is set up for one test. Use it in beforeEach, or save the state it builds with test.setup()";

    private static readonly JsonSerializerOptions Quoting = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>How an error names a test body or a hook: <c>test "signs in"</c>, <c>setup test "auth"</c>, <c>beforeEach hook</c> (upstream's <c>describeUser</c>).</summary>
    public static string DescribeUser(FixtureUser user) =>
        user is RegisteredTest test
            ? $"{(test.Kind == "setup" ? "setup test" : "test")} {JsonSerializer.Serialize(string.Join(" > ", test.TitlePath), Quoting)}"
            : $"{user.Kind} hook";

    /// <summary>How an error names a <c>test.extend()</c> fixture of <paramref name="scope"/>: <c>fixture "page"</c>, <c>worker fixture "database"</c> (upstream's <c>describeFixture</c>).</summary>
    public static string DescribeFixture(string name, string? scope) =>
        $"{(scope == FixtureScope.Worker ? "worker fixture" : "fixture")} \"{name}\"";

    /// <summary>
    /// Checks the fixtures one <c>test.extend()</c> call added (<paramref name="added"/>, the end of <paramref name="chain"/>) as far as they can be before a
    /// target is known: no two fixtures need each other, and a worker fixture names no fixture known to be a test fixture (upstream's <c>checkDeclaredFixtures</c>).
    /// Not ported: that each function destructures its first parameter, which a C# function has no first parameter to break.
    /// </summary>
    public static void CheckDeclaredFixtures(
        IReadOnlyList<FixtureDefinition> chain,
        IReadOnlyList<FixtureDefinition> added
    )
    {
        var planner = new Planner(chain, BuiltInFixtures.Declared);
        foreach (var definition in added)
        {
            planner.Visit(definition);
        }
    }

    /// <summary>
    /// The plan of one test attempt, resolved as one unit: what the body and every <c>beforeEach</c> and <c>afterEach</c> hook that applies name, over the
    /// test's own chain. A fixture only an <c>afterEach</c> names is set up before the first <c>beforeEach</c>, never on the cleanup budget (upstream's <c>planAttempt</c>).
    /// </summary>
    public static FixturePlan PlanAttempt(BuiltInTarget target, RegisteredTest test, IReadOnlyList<TestHook> hooks) =>
        PlanFixtures(target, test.Fixtures, FixtureScope.Test, [.. hooks, test]);

    /// <summary>The plan of one <c>beforeAll</c> or <c>afterAll</c> hook, over the chain it was registered through (upstream's <c>planSuiteHook</c>).</summary>
    public static FixturePlan PlanSuiteHook(BuiltInTarget target, SuiteHook hook) =>
        PlanFixtures(target, hook.Fixtures, FixtureScope.Worker, [hook]);

    /// <summary>
    /// Plans the fixtures of <paramref name="chain"/> that <paramref name="users"/> need on <paramref name="target"/>: the ones they name, the ones those name,
    /// and the <c>auto</c> ones (a suite hook sets up no test fixture, <c>auto</c> or not). Throws a <see cref="FixtureUseException"/>, before anything is set up, for a name
    /// no target in the config has a fixture for, a test fixture named by a suite hook or a worker fixture, and a cycle. <paramref name="scope"/> is <c>test</c> for an
    /// attempt (its hooks and body), <c>worker</c> for a suite hook.
    /// </summary>
    private static FixturePlan PlanFixtures(
        BuiltInTarget target,
        IReadOnlyList<FixtureDefinition> chain,
        string scope,
        IReadOnlyList<FixtureUser> users
    )
    {
        var builtIns = BuiltInFixtures.Of(target);
        var planner = new Planner(chain, builtIns);
        // An auto fixture is a root where its name stands for it: one that an override extends is reached through the override, or not at all.
        bool Stands(FixtureDefinition definition) => ReferenceEquals(planner.Declared(definition.Name), definition);
        var roots = new List<FixtureDefinition>();
        void AddRoot(FixtureDefinition definition)
        {
            if (!roots.Contains(definition))
            {
                roots.Add(definition);
            }
        }

        foreach (var definition in chain)
        {
            if (
                Stands(definition)
                && Planner.AutoOf(definition)
                && (scope == FixtureScope.Test || Planner.ScopeOf(definition) == FixtureScope.Worker)
            )
            {
                AddRoot(definition);
            }
        }

        var inputs = new Dictionary<FixtureUser, FixtureInputs>(ReferenceEqualityComparer.Instance);
        foreach (var user in users)
        {
            var what = DescribeUser(user);
            var resolved = new FixtureInputs();
            foreach (var name in user.Names)
            {
                resolved.Set(name, planner.Resolve(name, what, user.Source, scope));
            }

            inputs[user] = resolved;
            foreach (var definition in resolved.Values)
            {
                if (definition is not null)
                {
                    AddRoot(definition);
                }
            }
        }

        foreach (var definition in roots)
        {
            planner.Visit(definition);
        }

        // Declaration order over everything reached, each fixture after the ones it names: a fixture's place does not depend on who named it, and a suite
        // that names every fixture sets them up as it did before setup was on demand.
        var order = new List<PlannedFixture>();
        var placed = new Dictionary<FixtureDefinition, PlannedFixture>(ReferenceEqualityComparer.Instance);
        PlannedFixture Place(FixtureDefinition definition)
        {
            if (placed.TryGetValue(definition, out var known))
            {
                return known;
            }

            var own = planner.Inputs.GetValueOrDefault(definition) ?? new FixtureInputs();
            // reachesEngine stops at the first name that reaches the engine, so it cannot be what orders the rest.
            foreach (var dependency in own.Values)
            {
                if (dependency is not null)
                {
                    Place(dependency);
                }
            }

            var planned = new PlannedFixture(definition, Planner.ScopeOf(definition), ReachesEngine(own), own);
            placed[definition] = planned;
            order.Add(planned);
            return planned;
        }

        bool ReachesEngine(FixtureInputs own) =>
            own.Any(pair => pair.Value is null ? builtIns.NeedsAttempt(pair.Key) : Place(pair.Value).UsesEngine);
        foreach (var definition in chain)
        {
            if (planner.Inputs.ContainsKey(definition))
            {
                Place(definition);
            }
        }

        var usesEngine = inputs.Values.Any(ReachesEngine) || roots.Any(definition => Place(definition).UsesEngine);
        return new FixturePlan(order, inputs, usesEngine);
    }

    /// <summary>
    /// Walks the fixtures a plan needs, each after the fixtures it names (upstream's <c>Planner</c>). The built-ins are the target's, or, while a
    /// <c>test.extend()</c> call is checked, the ones known before any target is.
    /// </summary>
    private sealed class Planner(IReadOnlyList<FixtureDefinition> chain, IBuiltInScopes builtIns)
    {
        private readonly Dictionary<FixtureDefinition, bool> _done = new(ReferenceEqualityComparer.Instance);

        /// <summary>Every fixture visited, with what each name its function reads resolved to.</summary>
        public Dictionary<FixtureDefinition, FixtureInputs> Inputs { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>The scope of <paramref name="definition"/>: the one it declares, else <c>test</c>, even when it extends another fixture.</summary>
        public static string ScopeOf(FixtureDefinition definition) => definition.Scope ?? FixtureScope.Test;

        /// <summary>Whether <paramref name="definition"/> is set up without being named: what it declares, else not, even when it extends another fixture.</summary>
        public static bool AutoOf(FixtureDefinition definition) => definition.Auto ?? false;

        /// <summary>
        /// The chain fixture <paramref name="name"/> stands for. A fixture that names itself reads the one of its name declared before it, which it extends; any other
        /// reader gets the last one declared, so an override reaches everyone.
        /// </summary>
        public FixtureDefinition? Declared(string name, FixtureDefinition? from = null)
        {
            IReadOnlyList<FixtureDefinition> before =
                from is not null && from.Name == name ? [.. chain.Take(IndexOf(from))] : chain;
            return before.LastOrDefault(definition => definition.Name == name);
        }

        /// <summary>
        /// Resolves <paramref name="name"/>, which the code <paramref name="what"/> describes reads, to the chain fixture it stands for, or to null for a built-in;
        /// <paramref name="source"/> is where that code is. <paramref name="scope"/> is how long the code lives: a suite hook or a worker fixture (<c>worker</c>)
        /// cannot read a test fixture. <paramref name="from"/> is the fixture reading it, which sees the fixtures of its own <c>test.extend()</c> call and of the ones
        /// before it, and every fixture that extends a built-in.
        /// </summary>
        public FixtureDefinition? Resolve(
            string name,
            string what,
            SourceLocation? source,
            string scope,
            FixtureDefinition? from = null
        )
        {
            var builtIn = builtIns.Scope(name);
            var definition = Declared(name, from);
            var visible = chain.Where(each => from is null || each.Layer <= from.Layer).ToList();
            if (definition is null && builtIn is null)
            {
                var available = new[]
                {
                    builtIns.Names,
                    visible.Where(each => !ReferenceEquals(each, from)).Select(each => each.Name).ToList(),
                }
                    .SelectMany(names => names)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(each => each, StringComparer.Ordinal)
                    .ToList();
                throw new FixtureUseException($"{what} names \"{name}\": {builtIns.Missing(name, available)}", source);
            }

            if (definition is not null && builtIn is null && !visible.Any(each => each.Name == name))
            {
                throw new FixtureUseException(
                    $"{what} names \"{name}\", which a later test.extend() declares: a fixture sees the fixtures of its own extend() call and of the ones before it",
                    source
                );
            }

            if (
                scope == FixtureScope.Worker
                && (definition is null ? builtIn : ScopeOf(definition)) == FixtureScope.Test
            )
            {
                throw new FixtureUseException(
                    $"{what} names \"{name}\", a test fixture{OnTarget()}: {ScopeRule}",
                    source
                );
            }

            return definition;
        }

        /// <summary>Resolves what <paramref name="definition"/> names, and what those fixtures name. <paramref name="path"/> is the fixtures that led here, outermost first.</summary>
        public void Visit(FixtureDefinition definition, IReadOnlyList<FixtureDefinition>? path = null)
        {
            path ??= [];
            if (_done.TryGetValue(definition, out var finished))
            {
                if (finished)
                {
                    return;
                }

                var cycle = string.Join(
                    " -> ",
                    path.Skip(path.ToList().IndexOf(definition)).Append(definition).Select(each => $"\"{each.Name}\"")
                );
                throw new FixtureUseException(
                    $"fixtures depend on each other in a cycle: {cycle}. A fixture cannot name one that needs it",
                    definition.Source
                );
            }

            _done[definition] = false;
            var scope = ScopeOf(definition);
            var what = DescribeFixture(definition.Name, scope);
            var inputs = new FixtureInputs();
            foreach (var name in definition.Names)
            {
                var dependency = Resolve(name, what, definition.Source, scope, definition);
                inputs.Set(name, dependency);
                if (dependency is not null)
                {
                    Visit(dependency, [.. path, definition]);
                }
            }

            _done[definition] = true;
            Inputs[definition] = inputs;
        }

        private int IndexOf(FixtureDefinition definition)
        {
            for (var index = 0; index < chain.Count; index++)
            {
                if (ReferenceEquals(chain[index], definition))
                {
                    return index;
                }
            }

            return -1;
        }

        private string OnTarget() => builtIns.TargetName is null ? "" : $" on target \"{builtIns.TargetName}\"";
    }
}
