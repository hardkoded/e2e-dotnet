// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NUnit.Framework;

namespace E2E.NUnit;

/// <summary>
/// The tests the test filters left out of this run, and how an assembly's tests are found for them (.NET-only: upstream's host applies
/// <c>filterSelection</c> to the collected files; NUnit has no collection step, so the tests are found by reflection). The selection is
/// made once, by <see cref="E2ERunFixture"/>, and each <see cref="E2ETest"/> skips itself at its start.
/// </summary>
internal static class RunSelection
{
    private static IReadOnlyDictionary<string, string> _excluded = new Dictionary<string, string>(StringComparer.Ordinal);

    public static void Set(IReadOnlyDictionary<string, string> excluded) => Volatile.Write(ref _excluded, excluded);

    /// <summary>Why the filters left the test out; null when it runs.</summary>
    public static string? ReasonFor(Type fixture, string? method) =>
        method is not null && Volatile.Read(ref _excluded).TryGetValue(KeyOf(fixture, method), out var reason) ? reason : null;

    private static string KeyOf(Type fixture, string method) => fixture.FullName + "." + method;

    /// <summary>The tests of <paramref name="assembly"/>: every method of a concrete <see cref="E2ETest"/> that NUnit runs, a parameterized one once.</summary>
    public static IReadOnlyList<DiscoveredTest> Discover(Assembly assembly)
    {
        var found = new List<DiscoveredTest>();
        foreach (var type in LoadableTypes(assembly).Where(type => type is { IsAbstract: false, IsClass: true } && typeof(E2ETest).IsAssignableFrom(type) && !IsExplicit(type)).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var typeTags = TagsOf(type, assembly);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(IsTest).Where(method => !IsExplicit(method)).OrderBy(method => method.MetadataToken))
            {
                found.Add(new DiscoveredTest(
                    KeyOf(type, method.Name),
                    type.Namespace ?? "",
                    TypeNameOf(type),
                    method.Name,
                    [.. typeTags, .. method.GetCustomAttributes<CategoryAttribute>(true).Select(category => category.Name)],
                    (method.GetCustomAttribute<IgnoreAttribute>(true) ?? type.GetCustomAttribute<IgnoreAttribute>(true))?.Reason));
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

    private static bool IsTest(MethodInfo method) =>
        method.IsDefined(typeof(TestAttribute), true) || method.IsDefined(typeof(TestCaseAttribute), true)
        || method.IsDefined(typeof(TestCaseSourceAttribute), true) || method.IsDefined(typeof(TheoryAttribute), true);

    private static bool IsExplicit(MemberInfo member) => member.IsDefined(typeof(ExplicitAttribute), true);

    // A nested class is named with its enclosing classes, so two nested classes with one name stay apart.
    private static string TypeNameOf(Type type) => type.DeclaringType is { } outer ? TypeNameOf(outer) + "+" + type.Name : type.Name;

    private static IEnumerable<string> TagsOf(Type type, Assembly assembly)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            foreach (var category in current.GetCustomAttributes<CategoryAttribute>(true))
            {
                yield return category.Name;
            }
        }

        foreach (var category in assembly.GetCustomAttributes<CategoryAttribute>())
        {
            yield return category.Name;
        }
    }
}
