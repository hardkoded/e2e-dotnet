// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.Loader;

namespace E2E.Cli.Mcp;

/// <summary>
/// Loads the project's test assembly and finds the <see cref="ISessionTestRunner"/> its test framework package ships (.NET-only: upstream runs
/// the test with its own runner). The tool names no test framework: the runner comes from an <c>E2E.*</c> assembly the test assembly references.
/// </summary>
internal static class SessionTests
{
    private static readonly List<AssemblyDependencyResolver> Resolvers = [];

    private static readonly Dictionary<string, (Assembly Tests, ISessionTestRunner Runner)> Loaded = new(StringComparer.Ordinal);

    /// <summary>Loads <paramref name="assemblyPath"/> into the tool's own load context, so the test shares this tool's E2E assembly.</summary>
    public static (Assembly Tests, ISessionTestRunner Runner) Load(string assemblyPath)
    {
        // A load context cannot unload an assembly: one load per path, for the life of the server.
        lock (Loaded)
        {
            if (Loaded.TryGetValue(assemblyPath, out var known))
            {
                return known;
            }

            return Loaded[assemblyPath] = LoadFresh(assemblyPath);
        }
    }

    /// <summary>Tests a <c>TEST_AMBIGUOUS</c> message names before it counts the rest (upstream's <c>MAX_LISTED_TESTS</c>).</summary>
    private const int MaxListedTests = 10;

    /// <summary>
    /// Checks that <paramref name="name"/> selects exactly one test a session can run alone, before anything boots: <c>NO_TESTS</c> for none,
    /// <c>TEST_AMBIGUOUS</c> for several, and <c>SESSION_TEST_UNSUPPORTED</c> for one that starts from a saved session. Upstream's
    /// <c>selectStandaloneTest</c> (it names tests as <c>file:line</c>; the port names them by full name, and a test's target is its fixture's).
    /// </summary>
    public static void SelectOne(ISessionTestRunner runner, Assembly tests, string name, string assembly)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var selected = runner.Select(tests, name);
        if (selected.Count == 0)
        {
            throw new ConfigurationException("NO_TESTS", "no test named \"" + name + "\" in " + assembly + "; use the full name, Namespace.Class.Method");
        }

        if (selected.Count > 1)
        {
            var listed = selected.Take(MaxListedTests).Select(test => "\"" + test.FullName + "\"").ToList();
            var rest = selected.Count - listed.Count;
            throw new ConfigurationException(
                "TEST_AMBIGUOUS",
                "\"" + name + "\" selects " + selected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " tests, and a session runs one; name it by its full name: "
                    + string.Join(", ", listed) + (rest > 0 ? ", and " + rest.ToString(System.Globalization.CultureInfo.InvariantCulture) + " more" : string.Empty));
        }

        if (selected[0].Unsupported is { } reason)
        {
            throw new ConfigurationException("SESSION_TEST_UNSUPPORTED", reason);
        }
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException cause)
        {
            return cause.Types.OfType<Type>();
        }
    }

    private static (Assembly Tests, ISessionTestRunner Runner) LoadFresh(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new ConfigurationException("INVALID_CONFIG", "testAssembly " + assemblyPath + " does not exist; build the test project first");
        }

        // The test assembly's dependencies sit beside it. E2E is already loaded here, and the test binds to this copy.
        var resolver = new AssemblyDependencyResolver(assemblyPath);
        Resolvers.Add(resolver);
        if (Resolvers.Count == 1)
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
                Resolvers.Select(known => known.ResolveAssemblyToPath(name)).FirstOrDefault(path => path is not null) is { } path ? context.LoadFromAssemblyPath(path) : null;
        }

        var tests = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        foreach (var reference in tests.GetReferencedAssemblies().Where(name => name.Name?.StartsWith("E2E.", StringComparison.Ordinal) == true))
        {
            var framework = Assembly.Load(reference);
            var type = TypesOf(framework).FirstOrDefault(candidate => !candidate.IsAbstract && typeof(ISessionTestRunner).IsAssignableFrom(candidate));
            if (type is not null)
            {
                return (tests, (ISessionTestRunner)Activator.CreateInstance(type, nonPublic: true)!);
            }
        }

        throw new ConfigurationException("INVALID_CONFIG", "testAssembly " + assemblyPath + " references no E2E test framework package that can run a test in a session (E2E.NUnit does)");
    }
}
