// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace E2E.XUnit.V3;

/// <summary>
/// The tests the test filters left out of this run, and how an assembly's tests are found for them (.NET-only: upstream's host applies
/// <c>filterSelection</c> to the collected files; xUnit has no collection step the fixture can ask, so the tests are found by reflection).
/// The selection is made once, by <see cref="E2ERunFixture"/>, and each <see cref="E2ETest"/> skips itself at its start.
/// </summary>
internal static class RunSelection
{
    /// <summary>The trait key that names a category, as <c>dotnet test --filter "TestCategory=..."</c> reads it.</summary>
    internal const string CategoryTrait = "TestCategory";

    // The contract of xUnit v3 for an exception that skips a test: its message starts with this, and the rest is the reason.
    private const string DynamicSkipToken = "$XunitDynamicSkip$";

    private static IReadOnlyDictionary<string, string> _excluded = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly Lock Gate = new();

    public static void Set(IReadOnlyDictionary<string, string> excluded) => Volatile.Write(ref _excluded, excluded);

    /// <summary>Adds what one run fixture left out to what the others did, so a second assembly fixture never lifts an exclusion.</summary>
    public static void Add(IReadOnlyDictionary<string, string> excluded)
    {
        lock (Gate)
        {
            _excluded = new Dictionary<string, string>(_excluded, StringComparer.Ordinal).Concat(excluded).GroupBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
        }
    }

    /// <summary>Ends a test the filters left out as skipped, with their reason, before its session starts.</summary>
    public static void SkipIfLeftOut(Type fixture, string? method)
    {
        if (method is not null && Volatile.Read(ref _excluded).TryGetValue(KeyOf(fixture, method), out var reason))
        {
            throw new InvalidOperationException(DynamicSkipToken + reason);
        }
    }

    /// <summary>Ends a setup test as skipped when it already ran for a test that restores its session (see <c>SetupDependencies</c>).</summary>
    public static void SkipIfSetupRan(Type fixture, string? method)
    {
        if (SetupDependencies.HasRun(fixture, method))
        {
            throw new InvalidOperationException(DynamicSkipToken + "the setup test already ran for a test that restores its session");
        }
    }

    private static string KeyOf(Type fixture, string method) => fixture.FullName + "." + method;

    /// <summary>The tests of <paramref name="assembly"/>: every method of a concrete <see cref="E2ETest"/> that xUnit runs, a theory once.</summary>
    public static IReadOnlyList<DiscoveredTest> Discover(Assembly assembly) =>
        Discover(LoadableTypes(assembly), assembly);

    /// <summary>The tests of <paramref name="types"/>, whose assembly's traits they carry.</summary>
    internal static IReadOnlyList<DiscoveredTest> Discover(IEnumerable<Type> types, Assembly assembly)
    {
        var found = new List<DiscoveredTest>();
        foreach (var type in types.Where(type => type is { IsAbstract: false, IsClass: true } && typeof(E2ETest).IsAssignableFrom(type)).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var typeTags = TagsOf(type, assembly).ToList();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).OrderBy(method => method.MetadataToken))
            {
                if (method.GetCustomAttributes(true).OfType<IFactAttribute>().FirstOrDefault() is not { Explicit: not true } fact)
                {
                    continue;
                }

                found.Add(new DiscoveredTest(
                    KeyOf(type, method.Name),
                    type.Namespace ?? "",
                    TypeNameOf(type),
                    method.Name,
                    [.. typeTags, .. CategoriesOf(method)],
                    // A skip with a condition (SkipUnless, SkipWhen) is decided when the test runs, so it is not an outright skip.
                    string.IsNullOrEmpty(fact.Skip) || fact.SkipUnless is not null || fact.SkipWhen is not null ? null : fact.Skip));
            }
        }

        return found;
    }

    // A type that fails to load (a missing optional dependency) is not a test the run could start.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    // A nested class is named with its enclosing classes, so two nested classes with one name stay apart.
    private static string TypeNameOf(Type type) => type.DeclaringType is { } outer ? TypeNameOf(outer) + "+" + type.Name : type.Name;

    private static IEnumerable<string> CategoriesOf(MemberInfo member) =>
        member.GetCustomAttributes(true).OfType<ITraitAttribute>().SelectMany(trait => trait.GetTraits() ?? [])
            .Where(trait => string.Equals(trait.Key, CategoryTrait, StringComparison.Ordinal)).Select(trait => trait.Value);

    private static IEnumerable<string> TagsOf(Type type, Assembly assembly)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            foreach (var category in CategoriesOf(current))
            {
                yield return category;
            }
        }

        foreach (var trait in assembly.GetCustomAttributes().OfType<ITraitAttribute>().SelectMany(trait => trait.GetTraits() ?? []).Where(trait => string.Equals(trait.Key, CategoryTrait, StringComparison.Ordinal)))
        {
            yield return trait.Value;
        }
    }
}
