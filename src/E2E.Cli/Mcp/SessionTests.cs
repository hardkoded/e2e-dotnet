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

    private static readonly Dictionary<string, ISessionTestRunner> Runners = new(StringComparer.Ordinal);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Assembly, string> Paths = new();

    /// <summary>
    /// Loads <paramref name="assemblyPath"/> as it is now, into a load context of its own that <see cref="TestAssembly.Dispose"/> unloads: a test
    /// edited and rebuilt between two opens runs as it is now (upstream collects the test files again each time a session opens). The
    /// test binds to this tool's E2E assembly and test framework, which stay in the default context.
    /// </summary>
    public static TestAssembly Load(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new ConfigurationException("INVALID_CONFIG", "testAssembly " + assemblyPath + " does not exist; build the test project first");
        }

        var context = new TestLoadContext(assemblyPath);
        try
        {
            // Read into memory so the build output stays unlocked: the next build can replace it while a session is open.
            var tests = context.LoadFromBytes(assemblyPath);
            Paths.AddOrUpdate(tests, assemblyPath);
            return new TestAssembly(tests, RunnerOf(assemblyPath, tests), context);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    /// <summary>The path <paramref name="tests"/> was loaded from (the load reads bytes, so <see cref="Assembly.Location"/> is empty).</summary>
    internal static string? PathOf(Assembly tests) => Paths.TryGetValue(tests, out var path) ? path : null;

    /// <summary>A test assembly loaded for one open; disposing it unloads its load context.</summary>
    internal sealed class TestAssembly(Assembly tests, ISessionTestRunner runner, AssemblyLoadContext context) : IDisposable
    {
        public Assembly Tests { get; } = tests;

        public ISessionTestRunner Runner { get; } = runner;

        public void Deconstruct(out Assembly testsOut, out ISessionTestRunner runnerOut)
        {
            testsOut = Tests;
            runnerOut = Runner;
        }

        public void Dispose() => context.Unload();
    }

    private static ISessionTestRunner RunnerOf(string assemblyPath, Assembly tests)
    {
        // The runner's assembly loads once per path: it lives in the default context, beside E2E.
        lock (Runners)
        {
            if (Runners.TryGetValue(assemblyPath, out var known))
            {
                return known;
            }

            var resolver = new AssemblyDependencyResolver(assemblyPath);
            Resolvers.Add(resolver);
            if (Resolvers.Count == 1)
            {
                AssemblyLoadContext.Default.Resolving += (context, name) =>
                    Resolvers.Select(candidate => candidate.ResolveAssemblyToPath(name)).FirstOrDefault(path => path is not null) is { } path ? context.LoadFromAssemblyPath(path) : null;
            }

            foreach (var reference in tests.GetReferencedAssemblies().Where(name => name.Name?.StartsWith("E2E.", StringComparison.Ordinal) == true))
            {
                var framework = Assembly.Load(reference);
                var type = TypesOf(framework).FirstOrDefault(candidate => !candidate.IsAbstract && typeof(ISessionTestRunner).IsAssignableFrom(candidate));
                if (type is not null)
                {
                    return Runners[assemblyPath] = (ISessionTestRunner)Activator.CreateInstance(type, nonPublic: true)!;
                }
            }

            throw new ConfigurationException("INVALID_CONFIG", "testAssembly " + assemblyPath + " references no E2E test framework package that can run a test in a session (E2E.NUnit does)");
        }
    }

    /// <summary>Loads the test assembly and what only it ships; everything else, E2E and the test framework included, comes from the default context.</summary>
    private sealed class TestLoadContext(string assemblyPath) : AssemblyLoadContext("e2e-test:" + assemblyPath, isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        public Assembly LoadFromBytes(string path)
        {
            using var dll = new MemoryStream(File.ReadAllBytes(path));
            var pdbPath = System.IO.Path.ChangeExtension(path, ".pdb");
            if (!File.Exists(pdbPath))
            {
                return LoadFromStream(dll);
            }

            using var pdb = new MemoryStream(File.ReadAllBytes(pdbPath));
            return LoadFromStream(dll, pdb);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // What the default context can resolve is shared with the tool: a second copy of E2E or NUnit would not be the one the runner uses.
            try
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }
            catch (FileNotFoundException)
            {
            }

            return _resolver.ResolveAssemblyToPath(assemblyName) is { } path ? LoadFromAssemblyPath(path) : null;
        }
    }

    /// <summary>Tests a <c>TEST_AMBIGUOUS</c> message names before it counts the rest (upstream's <c>MAX_LISTED_TESTS</c>).</summary>
    private const int MaxListedTests = 10;

    /// <summary>
    /// Checks that <paramref name="name"/> selects exactly one test a session can run alone, before anything boots: <c>NO_TESTS</c> for none,
    /// <c>TEST_AMBIGUOUS</c> for several, and <c>SESSION_TEST_UNSUPPORTED</c> for one that starts from a saved session. Upstream's
    /// <c>selectStandaloneTest</c>. A test is named by its full name or as <c>file:line</c>, the line its attributes or signature is on; returns the
    /// full name to run.
    /// </summary>
    public static string SelectOne(ISessionTestRunner runner, Assembly tests, string name, string assembly)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var selected = TestLocations.TryParse(name, out var file, out var line) && runner.List(tests) is var all && all.Any(test => test.Method is not null)
            ? SelectAt(all, file, line, name, assembly)
            : runner.Select(tests, name);
        if (selected.Count == 0)
        {
            throw new ConfigurationException("NO_TESTS", "no test named \"" + name + "\" in " + assembly + "; use the full name, Namespace.Class.Method, or file:line");
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

        return selected[0].FullName;
    }

    private static IReadOnlyList<SessionTestInfo> SelectAt(IReadOnlyList<SessionTestInfo> all, string file, int line, string name, string assembly)
    {
        var inFile = all.Where(test => TestLocations.Declared(test, file) is not null).ToList();
        var hit = inFile.Where(test => TestLocations.Declared(test, file)!.Contains(line)).ToList();
        if (hit.Count == 0 && inFile.Count > 0)
        {
            var lines = inFile.Select(test => TestLocations.Declared(test, file)!.First).Distinct().Order();
            throw new ConfigurationException("NO_TESTS", name + " names no test (declared at lines " + string.Join(", ", lines) + "); name the line its attributes or signature is on, not a line inside the test");
        }

        return hit.Count > 0 ? hit : throw new ConfigurationException("NO_TESTS", "no test in " + file + " in " + assembly + "; name a source file of the test project, with :line on the line its test is declared on");
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
}
