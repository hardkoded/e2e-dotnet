// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using TUnit.Core;
using TUnit.Core.Enums;

namespace E2E.TUnit;

/// <summary>
/// The tests the test filters left out of this run, and how an assembly's tests are found for them (.NET-only: upstream's host applies
/// <c>filterSelection</c> to the collected files; the tests are found by reflection over the <c>[Test]</c> methods). The selection is
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

    /// <summary>
    /// Keeps the scores the test filters gave the tests of this run, by test key, for <see cref="RankRegistered"/> (.NET-only: upstream's runner
    /// orders its work plans itself, and TUnit owns the order of the tests).
    /// </summary>
    public static void SetRelevance(IReadOnlyDictionary<string, double> relevance)
    {
        lock (Gate)
        {
            // Merged, keeping the highest score per test, so a second run fixture in the assembly never lowers one.
            var merged = new Dictionary<string, double>(_relevance, StringComparer.Ordinal);
            foreach (var (key, score) in relevance)
            {
                merged[key] = merged.TryGetValue(key, out var existing) ? Math.Max(existing, score) : score;
            }

            Volatile.Write(ref _relevance, merged);
        }
    }

    /// <summary>Forgets every score (for the tests of this adapter).</summary>
    internal static void ClearRelevance() =>
        Volatile.Write(ref _relevance, new Dictionary<string, double>(StringComparer.Ordinal));

    /// <summary>
    /// Starts a registered test in the TUnit priority of its relevance band (upstream's <c>relevanceBand</c>), as a file ranks in <c>orderWorkPlans</c>;
    /// tests in one band keep TUnit's order. TUnit reads the priority when it plans the run, so this is called as each test registers, after the
    /// run fixture's discovery hook has scored the tests. A test keeps a higher priority of its own, such as a setup test's (see <c>E2ESetupAttribute</c>).
    /// </summary>
    public static void RankRegistered(global::TUnit.Core.TestContext context)
    {
        var details = context.Metadata.TestDetails;
        var ranked = PriorityOf(Units.RelevanceBand(RelevanceOf(KeyOf(details.ClassType, details.MethodName))));
        if (ranked > context.Parallelism.ExecutionPriority)
        {
            context.Parallelism.ExecutionPriority = ranked;
        }
    }

    /// <summary>How likely the test filters rated a test to fail on this change, from 0 to 1; 0 for a test none scored.</summary>
    public static double RelevanceOf(string key) =>
        Volatile.Read(ref _relevance).TryGetValue(key, out var score) ? score : 0;

    /// <summary>The TUnit priority of a relevance band: the unscored band 0 is <see cref="Priority.Normal"/>, and each band above runs earlier.</summary>
    internal static Priority PriorityOf(int band) =>
        band switch
        {
            <= 0 => Priority.Normal,
            1 => Priority.AboveNormal,
            2 => Priority.High,
            _ => Priority.Critical,
        };

    public static void Set(IReadOnlyDictionary<string, string> excluded) => Volatile.Write(ref _excluded, excluded);

    /// <summary>Adds what one run fixture left out to what the others did, so a second run fixture in the assembly never lifts an exclusion.</summary>
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
        method is not null && Volatile.Read(ref _excluded).TryGetValue(KeyOf(fixture, method), out var reason)
            ? reason
            : null;

    private static string KeyOf(Type fixture, string method) => fixture.FullName + "." + method;

    /// <summary>The tests of <paramref name="assembly"/>: every method of a concrete <see cref="E2ETest"/> that TUnit runs, a parameterized one once.</summary>
    public static IReadOnlyList<DiscoveredTest> Discover(Assembly assembly) =>
        Discover(LoadableTypes(assembly), assembly);

    /// <summary>The tests of <paramref name="types"/>, whose assembly's categories they carry.</summary>
    internal static IReadOnlyList<DiscoveredTest> Discover(IEnumerable<Type> types, Assembly assembly)
    {
        var found = new List<DiscoveredTest>();
        foreach (
            var type in types
                .Where(type =>
                    type is { IsAbstract: false, IsClass: true }
                    && typeof(E2ETest).IsAssignableFrom(type)
                    && !IsExplicit(type)
                )
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
        )
        {
            var typeTags = TagsOf(type, assembly).ToList();
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
                        [.. typeTags, .. CategoriesOf(method)],
                        // A skip with a condition (a derived SkipAttribute) is decided when the test runs, so it is not an outright skip.
                        OutrightSkip(method) ?? OutrightSkip(type)
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

    private static bool IsTest(MethodInfo method) => method.IsDefined(typeof(BaseTestAttribute), true);

    private static bool IsExplicit(MemberInfo member) => member.IsDefined(typeof(ExplicitAttribute), true);

    private static string? OutrightSkip(MemberInfo member) =>
        member
            .GetCustomAttributes<SkipAttribute>(true)
            .FirstOrDefault(skip => skip.GetType() == typeof(SkipAttribute))
            ?.Reason;

    // A nested class is named with its enclosing classes, so two nested classes with one name stay apart.
    private static string TypeNameOf(Type type) =>
        type.DeclaringType is { } outer ? TypeNameOf(outer) + "+" + type.Name : type.Name;

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
