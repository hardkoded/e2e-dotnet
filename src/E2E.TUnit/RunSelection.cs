// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using TUnit.Core;

namespace E2E.TUnit;

/// <summary>
/// The tests the test filters left out of this run, and how an assembly's tests are found for them (.NET-only: upstream's host applies
/// <c>filterSelection</c> to the collected files; the tests are found by reflection over the <c>[Test]</c> methods). The selection is
/// made once, by <see cref="E2ERunFixture"/>, and each <see cref="E2ETest"/> skips itself at its start.
/// </summary>
internal static class RunSelection
{
    private static IReadOnlyDictionary<string, string> _excluded = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly Lock Gate = new();

    public static void Set(IReadOnlyDictionary<string, string> excluded) => Volatile.Write(ref _excluded, excluded);

    /// <summary>Adds what one run fixture left out to what the others did, so a second run fixture in the assembly never lifts an exclusion.</summary>
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
        if (ReasonFor(fixture, method) is { } reason)
        {
            Skip.Test(reason);
        }
    }

    /// <summary>Ends a setup test as skipped when it already ran for a test that restores its session (see <c>SetupDependencies</c>).</summary>
    public static void SkipIfSetupRan(Type fixture, string? method)
    {
        if (SetupDependencies.HasRun(fixture, method))
        {
            Skip.Test("the setup test already ran for a test that restores its session");
        }
    }

    /// <summary>Why the filters left the test out; null when it runs.</summary>
    internal static string? ReasonFor(Type fixture, string? method) =>
        method is not null && Volatile.Read(ref _excluded).TryGetValue(KeyOf(fixture, method), out var reason) ? reason : null;

    private static string KeyOf(Type fixture, string method) => fixture.FullName + "." + method;

    /// <summary>The tests of <paramref name="assembly"/>: every method of a concrete <see cref="E2ETest"/> that TUnit runs, a parameterized one once.</summary>
    public static IReadOnlyList<DiscoveredTest> Discover(Assembly assembly) =>
        Discover(LoadableTypes(assembly), assembly);

    /// <summary>The tests of <paramref name="types"/>, whose assembly's categories they carry.</summary>
    internal static IReadOnlyList<DiscoveredTest> Discover(IEnumerable<Type> types, Assembly assembly)
    {
        var found = new List<DiscoveredTest>();
        foreach (var type in types.Where(type => type is { IsAbstract: false, IsClass: true } && typeof(E2ETest).IsAssignableFrom(type) && !IsExplicit(type)).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var typeTags = TagsOf(type, assembly).ToList();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(IsTest).Where(method => !IsExplicit(method)).OrderBy(method => method.MetadataToken))
            {
                found.Add(new DiscoveredTest(
                    KeyOf(type, method.Name),
                    type.Namespace ?? "",
                    TypeNameOf(type),
                    method.Name,
                    [.. typeTags, .. CategoriesOf(method)],
                    // A skip with a condition (a derived SkipAttribute) is decided when the test runs, so it is not an outright skip.
                    OutrightSkip(method) ?? OutrightSkip(type)));
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

    private static bool IsTest(MethodInfo method) => method.IsDefined(typeof(BaseTestAttribute), true);

    private static bool IsExplicit(MemberInfo member) => member.IsDefined(typeof(ExplicitAttribute), true);

    private static string? OutrightSkip(MemberInfo member) =>
        member.GetCustomAttributes<SkipAttribute>(true).FirstOrDefault(skip => skip.GetType() == typeof(SkipAttribute))?.Reason;

    // A nested class is named with its enclosing classes, so two nested classes with one name stay apart.
    private static string TypeNameOf(Type type) => type.DeclaringType is { } outer ? TypeNameOf(outer) + "+" + type.Name : type.Name;

    private static IEnumerable<string> CategoriesOf(MemberInfo member) =>
        member.GetCustomAttributes<CategoryAttribute>(true).Select(category => category.Category);

    private static IEnumerable<string> TagsOf(Type type, Assembly assembly)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            foreach (var category in CategoriesOf(current))
            {
                yield return category;
            }
        }

        foreach (var category in assembly.GetCustomAttributes<CategoryAttribute>())
        {
            yield return category.Category;
        }
    }
}
