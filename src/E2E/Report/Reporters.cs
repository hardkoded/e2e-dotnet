// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// Finds and runs the reporters when the run is over (.NET-only: <see cref="RunFinishedAsync"/> and
/// <see cref="Discover"/>, since upstream's runner owns the config that lists the reporters and knows when the run ends. The run fixtures of
/// <c>E2E.NUnit</c> and <c>E2E.XUnit.V3</c> call it when the last test of the assembly is done. The reporters themselves run through <see cref="Runner.RunReportersAsync"/> (upstream's <c>runReporters</c>).
/// </summary>
internal static class Reporters
{
    /// <summary>The reporters that <paramref name="assembly"/> registers with <see cref="E2EReporterAttribute"/>. One that cannot be built is one line on stderr.</summary>
    public static IReadOnlyList<Reporter> Discover(Assembly assembly)
    {
        List<Reporter> reporters = [];
        foreach (var attribute in assembly.GetCustomAttributes<E2EReporterAttribute>())
        {
            try
            {
                reporters.Add(Activator.CreateInstance(attribute.ReporterType) as Reporter
                    ?? throw new InvalidOperationException($"{attribute.ReporterType} is not a {nameof(Reporter)}"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"e2e: reporter {attribute.ReporterType} could not be created: {(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex).Message}");
            }
        }

        return reporters;
    }

    /// <summary>
    /// Runs the reporters of <paramref name="assembly"/> on every run this process wrote a report for, and prints the rows they return. It
    /// never throws: a reporter can never change the run's outcome.
    /// </summary>
    public static async Task RunFinishedAsync(Assembly assembly, TimeSpan? timeout = null, CancellationToken force = default)
    {
        try
        {
            var reporters = Discover(assembly);
            if (reporters.Count == 0)
            {
                return;
            }

            foreach (var finished in await ReportFile.FinishedRunsAsync().ConfigureAwait(false))
            {
                var rows = await Runner.RunReportersAsync(reporters, finished, timeout ?? Runner.ReporterTimeout, force).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    Console.Out.WriteLine($"{row.Label}  {row.Text}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"e2e: reporters failed: {ex.Message}");
        }
    }
}
