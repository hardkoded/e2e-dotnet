// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Execution;

namespace E2E.NUnit;

/// <summary>
/// The tests the test filters left out of this run, and how an assembly's tests are found for them (.NET-only: upstream's host applies
/// <c>filterSelection</c> to the collected files; NUnit has no collection step, so the tests are found by reflection). The selection is
/// made once, by <see cref="E2ERunFixture"/>, and each <see cref="E2ETest"/> skips itself at its start.
/// </summary>
internal static class RunSelection
{
    private static IReadOnlyDictionary<string, string> _excluded = new Dictionary<string, string>(
        StringComparer.Ordinal
    );

    private static IReadOnlyDictionary<string, double> _relevance = new Dictionary<string, double>(
        StringComparer.Ordinal
    );

    private static readonly Lock Gate = new();

    /// <summary>Keeps the scores the test filters gave the tests of this run, by test key, for <see cref="OrderByRelevance"/>.</summary>
    public static void SetRelevance(IReadOnlyDictionary<string, double> relevance) =>
        Volatile.Write(ref _relevance, relevance);

    /// <summary>
    /// The work item NUnit is running the setup of, when it can be reached: the executing worker keeps it in a private field, so a NUnit that renames the
    /// field gives null and the tests keep NUnit's order. NUnit builds the work items of the whole tree before any fixture runs, so this is the only
    /// place the order can still change (.NET-only; upstream's runner orders its own work plans).
    /// </summary>
    internal static CompositeWorkItem? CurrentWorkItem()
    {
        if (TestExecutionContext.CurrentContext.TestWorker is not { } worker)
        {
            return null;
        }

        try
        {
            return typeof(TestWorker)
                    .GetField("_currentWorkItem", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(worker) as CompositeWorkItem;
        }
        catch (Exception ex) when (ex is TargetException or ArgumentException or FieldAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Starts the likeliest tests first (upstream's <c>orderWorkPlans</c>). Puts the children of <paramref name="item"/> and of every suite below it in
    /// descending order of the relevance band (<c>relevanceBand</c>) of their best-scored test, in a stable sort that leaves an <c>[Order]</c> ranking,
    /// such as the one of <see cref="E2ESetupAttribute"/>, ahead of it.
    /// </summary>
    internal static void OrderByRelevance(CompositeWorkItem item)
    {
        foreach (var child in item.Children.OfType<CompositeWorkItem>())
        {
            OrderByRelevance(child);
        }

        var ordered = item
            .Children.OrderBy(child =>
                child.Test.Properties.Get(PropertyNames.Order) is int order ? order : int.MaxValue
            )
            .ThenByDescending(child => BandOf(child.Test))
            .ToList();
        item.Children.Clear();
        item.Children.AddRange(ordered);
    }

    private static int BandOf(ITest test)
    {
        if (test is TestSuite suite)
        {
            return suite.Tests.Count == 0 ? 0 : suite.Tests.Max(BandOf);
        }

        return
            test.TypeInfo?.Type is { } type
            && test.Method?.Name is { } method
            && Volatile.Read(ref _relevance).TryGetValue(KeyOf(type, method), out var score)
            ? Units.RelevanceBand(score)
            : 0;
    }

    public static void Set(IReadOnlyDictionary<string, string> excluded) => Volatile.Write(ref _excluded, excluded);

    /// <summary>Adds what one run fixture left out to what the others did, so a second <c>[SetUpFixture]</c> in the assembly never lifts an exclusion.</summary>
    public static void Add(IReadOnlyDictionary<string, string> excluded)
    {
        lock (Gate)
        {
            _excluded = new Dictionary<string, string>(_excluded, StringComparer.Ordinal)
                .Concat(excluded)
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);
        }
    }

    /// <summary>Why the filters left the test out; null when it runs.</summary>
    public static string? ReasonFor(Type fixture, string? method) =>
        method is not null && Volatile.Read(ref _excluded).TryGetValue(KeyOf(fixture, method), out var reason)
            ? reason
            : null;

    private static string KeyOf(Type fixture, string method) => fixture.FullName + "." + method;

    /// <summary>The tests of <paramref name="assembly"/>: every method of a concrete <see cref="E2ETest"/> that NUnit runs, a parameterized one once.</summary>
    public static IReadOnlyList<DiscoveredTest> Discover(Assembly assembly) =>
        Discover(LoadableTypes(assembly).Where(type => !IsExplicit(type)), assembly);

    /// <summary>The tests of <paramref name="types"/>, whose assembly's categories they carry.</summary>
    internal static IReadOnlyList<DiscoveredTest> Discover(IEnumerable<Type> types, Assembly assembly)
    {
        var found = new List<DiscoveredTest>();
        foreach (
            var type in types
                .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(E2ETest).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
        )
        {
            var typeTags = TagsOf(type, assembly);
            foreach (
                var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(IsTest)
                    .Where(method => !IsExplicit(method))
                    .OrderBy(method => method.MetadataToken)
            )
            {
                found.Add(
                    new DiscoveredTest(
                        KeyOf(type, method.Name),
                        type.Namespace ?? "",
                        TypeNameOf(type),
                        method.Name,
                        [
                            .. typeTags,
                            .. method.GetCustomAttributes<CategoryAttribute>(true).Select(category => category.Name),
                        ],
                        (
                            method.GetCustomAttribute<IgnoreAttribute>(true)
                            ?? type.GetCustomAttribute<IgnoreAttribute>(true)
                        )?.Reason
                    )
                );
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
        method.IsDefined(typeof(TestAttribute), true)
        || method.IsDefined(typeof(TestCaseAttribute), true)
        || method.IsDefined(typeof(TestCaseSourceAttribute), true)
        || method.IsDefined(typeof(TheoryAttribute), true);

    private static bool IsExplicit(MemberInfo member) => member.IsDefined(typeof(ExplicitAttribute), true);

    // A nested class is named with its enclosing classes, so two nested classes with one name stay apart.
    private static string TypeNameOf(Type type) =>
        type.DeclaringType is { } outer ? TypeNameOf(outer) + "+" + type.Name : type.Name;

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
