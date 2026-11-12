// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace E2E;

/// <summary>
/// Runs the setup test that saves a session before the test that restores it, once per run. Upstream's runner schedules a
/// <c>test.setup(title, { sessions }, fn)</c> ahead of every test that declares <c>{ session: name }</c>, even when only the dependent is
/// selected. .NET-only: NUnit and xUnit own the test order and the selection, so the first test that needs a session nobody saved yet
/// runs the setup test itself, here, in the test framework's place. It builds the declaring class with its parameterless constructor and runs
/// the setup method inside a session of its own (the fixture's start, the method, the settle and the commit), not the framework's
/// <c>[SetUp]</c> and <c>[TearDown]</c> of the derived class. The setup test then does not run again when the framework reaches it. A
/// setup that failed fails each dependent with <c>SESSION_UNAVAILABLE</c> without running again (upstream skips them with <c>setup-failed</c>).
/// </summary>
internal static class SetupDependencies
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<bool>>> Runs = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> Ran = new(StringComparer.Ordinal);

    /// <summary>
    /// Makes sure the session <paramref name="name"/> is saved when the test class <paramref name="dependent"/> restores it, by running the
    /// one setup test of its assembly that declares it. Does nothing when the session is saved, or when no setup test, or more than one,
    /// declares it: the restore then fails with <c>SESSION_UNAVAILABLE</c> as before.
    /// </summary>
    public static async Task EnsureAsync(SessionPlan plan, string name, Type dependent, Func<E2EFixture, MethodInfo, CancellationToken, Task> run, CancellationToken cancellationToken)
    {
        if (plan.Store.Exists(name, plan.Identity))
        {
            return;
        }

        var declaring = Find(dependent.Assembly, name);
        if (declaring is not { } setup)
        {
            return;
        }

        var key = plan.Identity.TargetId + "\0" + name;
        var lazy = Runs.GetOrAdd(key, _ => new Lazy<Task<bool>>(() => RunAsync(setup, run), LazyThreadSafetyMode.ExecutionAndPublication));
        // A caller that is cancelled stops waiting; the run goes on for the others.
        var passed = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!passed)
        {
            throw new ConfigurationException("SESSION_UNAVAILABLE", $"session \"{name}\" was not produced: its setup test {setup.Type.FullName}.{setup.Method.Name} failed");
        }
    }

    /// <summary>Whether the setup test <paramref name="method"/> of <paramref name="type"/> already ran for a test that restores its session.</summary>
    public static bool HasRun(Type type, string? method) => method is not null && Ran.ContainsKey(KeyOf(type, method));

    /// <summary>Forgets what ran, when the run's sessions go.</summary>
    public static void Reset()
    {
        Runs.Clear();
        Ran.Clear();
    }

    private static string KeyOf(Type type, string method) => type.FullName + "." + method;

    private static (Type Type, MethodInfo Method)? Find(Assembly assembly, string name)
    {
        (Type, MethodInfo)? found = null;
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }

        foreach (var type in types.Where(type => type is { IsAbstract: false, IsClass: true } && typeof(E2EFixture).IsAssignableFrom(type)))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (SessionAttributes.Saves(method) is { } saves && saves.Contains(name, StringComparer.Ordinal))
                {
                    if (found is not null)
                    {
                        return null;
                    }

                    found = (type, method);
                }
            }
        }

        return found;
    }

    private static async Task<bool> RunAsync((Type Type, MethodInfo Method) setup, Func<E2EFixture, MethodInfo, CancellationToken, Task> run)
    {
        if (setup.Type.GetConstructor(Type.EmptyTypes) is null)
        {
            return false;
        }

        // The run belongs to every dependent, so no one test's token drives it: the setup is bounded by its own timeouts.
        var fixture = (E2EFixture)Activator.CreateInstance(setup.Type)!;
        try
        {
            await run(fixture, setup.Method, CancellationToken.None).ConfigureAwait(false);

            // Only a setup that saved is skipped later; a failed one runs again when the framework reaches it, so its failure is reported.
            Ran[KeyOf(setup.Type, setup.Method.Name)] = true;
            return true;
        }
#pragma warning disable CA1031 // The setup's failure is reported when the framework runs it; each dependent fails with SESSION_UNAVAILABLE.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
        finally
        {
            switch (fixture)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
