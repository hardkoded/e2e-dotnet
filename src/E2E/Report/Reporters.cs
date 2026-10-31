// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// Runs the reporters when the run is over (upstream's <c>runReporters</c>, <c>run/runner.ts</c>). .NET-only: <see cref="RunFinishedAsync"/> and
/// <see cref="Discover"/>, since upstream's runner owns the config that lists the reporters and knows when the run ends. The run fixtures of
/// <c>E2E.NUnit</c> and <c>E2E.XUnit.V3</c> call it when the last test of the assembly is done.
/// </summary>
internal static class Reporters
{
    /// <summary>How long a reporter's <c>OnRunFinishedAsync</c> may take before the run stops waiting for it (upstream's <c>REPORTER_TIMEOUT_MS</c>).</summary>
    public static readonly TimeSpan ReporterTimeout = TimeSpan.FromMinutes(1);

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
                var rows = await RunReportersAsync(reporters, finished, timeout ?? ReporterTimeout, force).ConfigureAwait(false);
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

    /// <summary>
    /// Awaits every reporter's <c>OnRunFinishedAsync</c> at once, each within <paramref name="timeout"/> and until <paramref name="force"/> (a forced
    /// stop of the run), and collects the rows they return. A reporter that throws, runs out of time, or returns a row without a label and text is one
    /// line on stderr; it can never change the run's outcome.
    /// </summary>
    internal static async Task<IReadOnlyList<ReporterSummaryRow>> RunReportersAsync(IReadOnlyList<Reporter> reporters, FinishedRun finished, TimeSpan timeout, CancellationToken force)
    {
        var budget = Time.BudgetText((long)timeout.TotalMilliseconds);
        const string forced = "abandoned: the run was forced to stop";
        var outcomes = await Task.WhenAll(reporters.Select(async Task<IReadOnlyList<ReporterSummaryRow>> (reporter) =>
        {
            if (force.IsCancellationRequested)
            {
                Warn(reporter, forced);
                return [];
            }

            using var abandon = CancellationTokenSource.CreateLinkedTokenSource(force);
            abandon.CancelAfter(timeout);
            try
            {
                var running = reporter.OnRunFinishedAsync(finished, abandon.Token);

                // The reporter may ignore its token; the run stops waiting for it all the same.
                var gone = new TaskCompletionSource();
                using var registration = abandon.Token.Register(() => gone.TrySetResult());
                if (await Task.WhenAny(running, gone.Task).ConfigureAwait(false) != running)
                {
                    _ = running.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    Warn(reporter, force.IsCancellationRequested ? forced : $"did not finish within {budget}");
                    return [];
                }

                var result = await running.ConfigureAwait(false);
                if (result is null)
                {
                    return [];
                }

                var rows = result.Where(row => row is { Label.Length: > 0, Text.Length: > 0 }).ToList();
                if (rows.Count < result.Count)
                {
                    Warn(reporter, $"returned {result.Count - rows.Count} row(s) without a label and text; dropped");
                }

                return rows;
            }
            catch (OperationCanceledException) when (abandon.IsCancellationRequested)
            {
                // A reporter that honors its token ends cancelled: that is the run giving up on it, not a failure of its own.
                Warn(reporter, force.IsCancellationRequested ? forced : $"did not finish within {budget}");
                return [];
            }
            catch (Exception ex)
            {
                Warn(reporter, $"failed: {ex.Message}");
                return [];
            }
        })).ConfigureAwait(false);
        return [.. outcomes.SelectMany(rows => rows)];
    }

    private static void Warn(Reporter reporter, string detail) => Console.Error.WriteLine($"e2e: reporter \"{reporter.Name}\" {detail}");
}
