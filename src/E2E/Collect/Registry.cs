// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using E2E.Internal;

namespace E2E;

/// <summary>
/// What a test body, a hook, or a fixture function is handed: the fixtures it names (.NET-only: a function has no first parameter to destructure, so it
/// reads the fixtures it names by name). A fixture's value is read when it is asked for, so a built-in is acquired as the function reads it, not
/// before (upstream's getter properties). Reading enumerates and reads every fixture, as spreading the object does.
/// </summary>
internal sealed class FixtureValues : IReadOnlyDictionary<string, object?>
{
    private readonly List<(string Name, Func<object?> Read)> _entries = [];

    /// <summary>The fixture names, in the order the function names them.</summary>
    public IEnumerable<string> Keys => _entries.Select(entry => entry.Name);

    /// <summary>Every fixture's value, read.</summary>
    public IEnumerable<object?> Values => _entries.Select(entry => entry.Read());

    /// <summary>How many fixtures the function names.</summary>
    public int Count => _entries.Count;

    /// <summary>Reads the fixture <paramref name="name"/>; throws <see cref="KeyNotFoundException"/> for one the function does not name.</summary>
    public object? this[string name] =>
        TryGetValue(name, out var value)
            ? value
            : throw new KeyNotFoundException($"the function does not name the fixture \"{name}\"");

    /// <summary>Adds a fixture whose value <paramref name="read"/> produces each time it is read.</summary>
    public void Add(string name, Func<object?> read) => _entries.Add((name, read));

    /// <summary>Whether the function names <paramref name="name"/>.</summary>
    public bool ContainsKey(string name) => _entries.Any(entry => entry.Name == name);

    /// <summary>Reads the fixture <paramref name="name"/>, when the function names it.</summary>
    public bool TryGetValue(string name, out object? value)
    {
        foreach (var (each, read) in _entries)
        {
            if (each == name)
            {
                value = read();
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        _entries.Select(entry => KeyValuePair.Create(entry.Name, entry.Read())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A test body or a hook function (upstream's <c>TestBody</c>, <c>collect/registry.ts</c>), called with the fixtures it names.</summary>
internal delegate Task TestBody(FixtureValues fixtures);

/// <summary>A fixture function (upstream's <c>FixtureFn</c>, <c>types/fixtures.ts</c>): sets the fixture up, hands its value to <c>use</c>, and awaits what <c>use</c> returns before it tears down.</summary>
internal delegate Task FixtureFn(FixtureValues fixtures, Func<object?, Task> use);

/// <summary>
/// One fixture a <c>test.extend()</c> call defined: its name, its setup/teardown function, and how long one instance lives (upstream's <c>FixtureDefinition</c>,
/// <c>collect/registry.ts</c>). The object is the fixture's identity: a worker keeps one instance per definition. The fixtures it depends on are the ones its
/// function names (<see cref="Names"/>). One named like a built-in fixture, or like an earlier fixture of its chain, extends it: inside its function its
/// own name is the fixture it extends.
/// </summary>
internal sealed class FixtureDefinition
{
    /// <summary>The fixture's name.</summary>
    public required string Name { get; init; }

    /// <summary>The function that sets the fixture up and tears it down around its call to <c>use</c>.</summary>
    public required FixtureFn Fn { get; init; }

    /// <summary>The fixtures <see cref="Fn"/> names: what it depends on.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>The scope it declares. A plain function declares none: it has the scope of the fixture it extends, and is a test fixture when it extends none.</summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Whether it declares itself set up for every test (or once per worker) whether or not anything names it. One that does not say has the <c>auto</c>
    /// of the fixture it extends, and is set up on demand when it extends none.
    /// </summary>
    public bool? Auto { get; init; }

    /// <summary>Which <c>test.extend()</c> call of its chain defined it, counted from 0. A fixture sees the fixtures of its own call and of the ones before it.</summary>
    public int Layer { get; init; }

    /// <summary>Where its <c>test.extend()</c> call is.</summary>
    public SourceLocation? Source { get; init; }

    /// <summary>What every evaluation of its module calls it (<c>fixture-identity.ts</c>); a worker keeps one instance per identity.</summary>
    public string? Identity { get; init; }
}

/// <summary>
/// A registered test body or hook, as the planner reads it: the fixtures its function names, and where it is (upstream's <c>FixtureUser</c>,
/// <c>collect/fixture-plan.ts</c>, a union of <c>RegisteredTest</c> and <c>RegisteredHook</c>).
/// </summary>
internal abstract class FixtureUser
{
    /// <summary>What it is: <c>test</c>, <c>setup</c>, <c>beforeEach</c>, <c>afterEach</c>, <c>beforeAll</c>, or <c>afterAll</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The function, called with the fixtures it names.</summary>
    public required TestBody Fn { get; init; }

    /// <summary>The fixtures <see cref="Fn"/> names in its first parameter.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>The <c>test.extend()</c> chain it was registered through, outermost definition first.</summary>
    public IReadOnlyList<FixtureDefinition> Fixtures { get; init; } = [];

    /// <summary>Where it was registered.</summary>
    public SourceLocation? Source { get; init; }
}

/// <summary>A test as it was registered (upstream's <c>RegisteredTest</c>, <c>collect/registry.ts</c>), with the members the fixture plan reads.</summary>
internal sealed class RegisteredTest : FixtureUser
{
    /// <summary>The test's title.</summary>
    public string Title { get; init; } = "";

    /// <summary>The titles of its describe chain, outermost first, then its own.</summary>
    public required IReadOnlyList<string> TitlePath { get; init; }

    /// <summary>The order it was declared in.</summary>
    public int DeclarationIndex { get; init; }

    /// <summary>The group it sits in.</summary>
    public GroupNode? Group { get; init; }
}

/// <summary>A hook as it was registered (upstream's <c>RegisteredHook</c>, <c>collect/registry.ts</c>).</summary>
internal abstract class RegisteredHook : FixtureUser
{
    /// <summary>The group it sits in.</summary>
    public GroupNode? Group { get; init; }

    /// <summary>The order it was declared in.</summary>
    public int DeclarationIndex { get; init; }
}

/// <summary>A per-test hook (upstream's <c>TestHook</c>): runs with the attempt's fixtures. Its <see cref="FixtureUser.Kind"/> is <c>beforeEach</c> or <c>afterEach</c>.</summary>
internal sealed class TestHook : RegisteredHook;

/// <summary>A suite hook (upstream's <c>SuiteHook</c>): runs once per scope instance with worker fixtures only. Its <see cref="FixtureUser.Kind"/> is <c>beforeAll</c> or <c>afterAll</c>.</summary>
internal sealed class SuiteHook : RegisteredHook;
