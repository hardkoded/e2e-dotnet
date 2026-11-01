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
