// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// One entry of <c>reporters</c> in <c>e2e.config.json</c>: the name of a <see cref="Reporter"/> type and the options for it. .NET-only: upstream's config
/// lists reporter objects, and a JSON file holds no code, so it names the type and the reporter is built by reflection when the run begins.
/// The type is resolved then, not when the config loads, since a type of the test assembly is not loaded for every command that reads the config.
/// </summary>
internal sealed record ReporterEntry(string Name, JsonElement? Options)
{
    /// <summary>
    /// Resolves <see cref="Name"/>, an assembly-qualified type name or the name of a type in a loaded assembly, builds the reporter, and hands it
    /// its options. Throws <c>INVALID_CONFIG</c> for a type that cannot be a reporter. A message never holds an option value.
    /// </summary>
    public Reporter Create()
    {
        var type = Find(Name) ?? throw Invalid("the type was not found; name it with its assembly (\"Namespace.Type, Assembly\"), make sure that assembly is referenced and copied to the output, and in a trimmed app keep the type");
        if (type.IsAbstract || type.IsInterface || !type.IsAssignableTo(typeof(Reporter)))
        {
            throw Invalid("the type must be a non-abstract class that derives from E2E.Reports.Reporter");
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw Invalid("the type needs a public constructor with no parameters");
        }

        if (Options is not null && !type.IsAssignableTo(typeof(IConfigurableReporter)))
        {
            throw Invalid("the type takes no options; remove \"options\" or implement E2E.Reports.IConfigurableReporter");
        }

        var reporter = (Reporter)Activator.CreateInstance(type)!;
        if (Options is { } options)
        {
            ((IConfigurableReporter)reporter).Configure(options);
        }

        return reporter;
    }

    // A reporter is chosen by name in a file, so reflection is the point. A trimmed or AOT app must keep the type (see COMPATIBILITY.md, Reporters).
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "The config names the reporter type by string.")]
    private static Type? Find(string name)
    {
        if (Type.GetType(name, throwOnError: false) is { } type)
        {
            return type;
        }

        if (name.Contains(',', StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && assembly.GetType(name, throwOnError: false) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", message);
}

/// <summary>
/// Builds the reporters the config lists (.NET-only: <see cref="Create"/>, since upstream's runner owns the config that lists the reporters). <see cref="HostRun"/>
/// calls it when the run begins, for the run fixtures of <c>E2E.NUnit</c> and <c>E2E.XUnit.V3</c>. The reporters themselves run through
/// <see cref="Runner.RunReportersAsync"/> (upstream's <c>runReporters</c>).
/// </summary>
internal static class Reporters
{
    /// <summary>
    /// The reporters of <paramref name="config"/>, in config order. One that cannot be built (a type that is not found or is not a reporter, a
    /// constructor or <c>Configure</c> that throws) is one line on stderr that names its type string, and the others still run.
    /// </summary>
    public static IReadOnlyList<Reporter> Create(E2EConfig config)
    {
        List<Reporter> reporters = [];
        foreach (var entry in config.Reporters)
        {
            try
            {
                reporters.Add(entry.Create());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"e2e: reporter \"{entry.Name}\" could not be created: {(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex).Message}");
            }
        }

        reporters.AddRange(config.CustomReporters);
        return reporters;
    }
}
