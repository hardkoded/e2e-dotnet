// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.Loader;
using E2E.Engine;

namespace E2E.Tests.Core;

/// <summary>
/// Stands in for upstream's worker thread that loads its own copy of the module: a host opened from a second copy of the library, loaded
/// into its own <see cref="AssemblyLoadContext"/> beside the one the test process already holds.
/// </summary>
public static class IsolatedHost
{
    /// <summary>Opens a host on a fake engine from a copy of the library in a context of its own, and returns the token its engine saw.</summary>
    public static async Task<string?> HostIdFromOwnCopyAsync(string cwd)
    {
        var context = new CopyContext();
        try
        {
            var copy = context.LoadFromAssemblyPath(typeof(IsolatedHost).Assembly.Location);
            var launch = context
                .LoadFromAssemblyName(typeof(Launch).Assembly.GetName())
                .GetType(typeof(Launch).FullName!)!;
            if (
                launch == typeof(Launch)
                || launch.Assembly == typeof(Launch).Assembly
                || AssemblyLoadContext.GetLoadContext(launch.Assembly) != context
            )
            {
                throw new InvalidOperationException("The copy of the library was not loaded into its own context.");
            }

            var run = copy.GetType(typeof(IsolatedHost).FullName!)!
                .GetMethod(nameof(RunAsync), BindingFlags.Public | BindingFlags.Static)!;
            return await (Task<string?>)run.Invoke(null, BindingFlags.DoNotWrapExceptions, null, [cwd], null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>Runs inside the copy: opens a host on a fake engine, closes it, and returns the token its engine saw.</summary>
    public static async Task<string?> RunAsync(string cwd)
    {
        var engine = new CoreFakeEngine();
        await using var opened = await Launch.LaunchAsync(
            (IEngine)engine,
            new LaunchOptions { Cwd = cwd, Env = new Dictionary<string, string?>() }
        );
        await (await opened.NewSessionAsync()).CloseAsync();
        await opened.CloseAsync();
        return engine.InitInfos[0].HostId;
    }

    private sealed class CopyContext : AssemblyLoadContext
    {
        private static readonly string[] Copied = ["E2E", "E2E.Tests"];

        public CopyContext()
            : base("e2e-own-copy", isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is null || !Array.Exists(Copied, name => name == assemblyName.Name))
            {
                return null;
            }

            return LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll"));
        }
    }
}
