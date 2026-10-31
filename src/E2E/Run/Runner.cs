// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>How a run ended. Upstream's <c>RunStatus</c>, with the values a single run in the port can have.</summary>
public enum RunStatus
{
    Passed,

    /// <summary>A test or an agent step failed.</summary>
    Failed,

    /// <summary>Credentials, the environment, test setup, or the agent's own budget stopped the run, rather than the app misbehaving.</summary>
    Blocked,
}

/// <summary>The run's exit code and status, derived from the error that ended it, as upstream's runner derives them.</summary>
internal static class Runner
{
    /// <summary>
    /// The process exit code for an error: 0 for none, 1 for a test failure, 2 for a configuration or agent policy error, 3 for an
    /// engine, app, or model provider failure. The class of an agent code follows upstream's table.
    /// </summary>
    public static int ExitCodeOf(E2EException? error) => error switch
    {
        null => 0,
        AgentException agent => AgentCodeExit(agent.Code),
        TestException => 1,
        ConfigurationException => 2,
        _ => 3,
    };

    public static RunStatus StatusOf(E2EException? error) => error switch
    {
        null => RunStatus.Passed,
        AgentException { Blocked: true } => RunStatus.Blocked,
        _ => RunStatus.Failed,
    };

    private static int AgentCodeExit(string code) => ErrorSerializer.ExitCodeForCategory(AgentError.CategoryByCode.GetValueOrDefault(code, "test"));

    /// <summary>How long a reporter's <c>OnRunFinishedAsync</c> may take before the run stops waiting for it (upstream's <c>REPORTER_TIMEOUT_MS</c>).</summary>
    public static readonly TimeSpan ReporterTimeout = TimeSpan.FromMinutes(1);

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
                // On the thread pool, so a reporter that blocks before it returns a task cannot hold the budget off (a JavaScript promise cannot block its own timer).
                var running = Task.Run(() => reporter.OnRunFinishedAsync(finished, abandon.Token), CancellationToken.None);

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

    private static void Warn(Reporter reporter, string detail)
    {
        // A Name that throws is the reporter's fault; it must not cost the other reporters their rows.
        string name;
        try
        {
            name = reporter.Name;
        }
        catch (Exception)
        {
            name = reporter.GetType().Name;
        }

        Console.Error.WriteLine($"e2e: reporter \"{name}\" {detail}");
    }
}
