// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// One test registered in memory: its title and its body. Upstream's <c>RegisteredTest</c> (<c>collect/registry.ts</c>), with the body
/// taking the session the test's fixtures come from (.NET-only: a test has no <c>{ app, agent, screen }</c> object to destructure).
/// </summary>
internal sealed record InMemoryTest(string Title, Func<E2ESession, Task> Body);

/// <summary>
/// Tests registered in memory instead of found in files: one virtual file name and the tests its module would have registered. Upstream's
/// <c>InMemoryTests</c> (<c>run/runner.ts</c>). Bodies are closures, so the run is in-process, on one worker.
/// </summary>
internal sealed record InMemoryTests(string File, IReadOnlyList<InMemoryTest> Tests);

/// <summary>
/// What <see cref="Runner.RunAsync"/> runs and how. Upstream's <c>RunOptions</c> (<c>run/runner.ts</c>), with the options the port's run has:
/// it has no file collection, selection, or scheduling (a failed test runs again while the config's <c>retries</c> allow), so only <c>cwd</c>, <c>rawConfig</c>, <c>tests</c>, <c>env</c>, <c>noCache</c>,
/// <c>cacheDir</c>, <c>strictCache</c>, and <c>output</c> exist, plus the engine and agents a config file cannot hold.
/// </summary>
internal sealed record RunOptions
{
    /// <summary>The project root, over which the config's relative paths resolve. Defaults to the working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>A config value instead of a discovered file: the config's JSON. Defaults to <c>{}</c>.</summary>
    public string? RawConfig { get; init; }

    /// <summary>The tests to run, registered in memory.</summary>
    public required InMemoryTests Tests { get; init; }

    /// <summary>The environment the run resolves against, instead of the process's.</summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>Runs with the trace cache off (<c>--no-cache</c>), overriding the config.</summary>
    public bool NoCache { get; init; }

    /// <summary>The replay cache directory for this run (<c>--cache-dir</c>), relative to the project root.</summary>
    public string? CacheDir { get; init; }

    /// <summary>Turns <c>cache.strict</c> on (<c>--strict-cache</c>).</summary>
    public bool StrictCache { get; init; }

    /// <summary>The results directory for this run (<c>--output</c>), relative to the project root, over the config's <c>output</c>.</summary>
    public string? Output { get; init; }

    /// <summary>The engine every test and attempt runs on, one instance the caller owns and disposes (.NET-only: the config names a platform and holds no engine handle, as upstream's does). Defaults to the engine of the target's platform.</summary>
    public IEngine? Engine { get; init; }

    /// <summary>
    /// Agents given in code, by name, replacing the config's entry of the same name: upstream's config file is code, so its agents carry
    /// models and executors (.NET-only: <c>e2e.config.json</c> names a provider and a model id only).
    /// </summary>
    public IReadOnlyDictionary<string, AgentOptions>? Agents { get; init; }
}

/// <summary>What a run ended with. Upstream's <c>RunOutcome</c> (<c>run/runner.ts</c>), with the report of the tests that ran.</summary>
/// <param name="ExitCode">0 when every test passed, 1 for a test failure, 2 for a configuration or agent policy error, 3 for an engine, app, or model provider failure; a failure of closing a test's session joins it by the precedence 130, 4, 3, 2, 1, 0.</param>
/// <param name="Status">The same status <c>report.run.status</c> carries.</param>
/// <param name="Report">The report of the tests that ran, one result each, with the attempt of each.</param>
internal sealed record RunOutcome(int ExitCode, RunStatus Status, Report Report);

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

    /// <summary>
    /// Runs the in-memory tests of <paramref name="options"/> one after another, each attempt in a session of its own, and returns the run's
    /// exit code, status, and report. Upstream's <c>run</c> for a run with <c>tests</c> (<c>run/runner.ts</c>); a test that fails does not stop the
    /// next. A config that does not resolve throws, as a bad flag does. A cancelled <paramref name="cancellationToken"/> throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public static async Task<RunOutcome> RunAsync(RunOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = Path.GetFullPath(options.Cwd ?? Directory.GetCurrentDirectory());
        var env = options.Env;
        var config = E2EConfig.Parse(
            options.RawConfig ?? "{}",
            root,
            name => env is null ? Environment.GetEnvironmentVariable(name) : env.GetValueOrDefault(name),
            new CliOverrides { NoCache = options.NoCache, CacheDir = options.CacheDir, CacheStrict = options.StrictCache });
        var results = new List<E2E.Reports.ReportResult>();
        Report? first = null;
        var exitCodes = new List<int>();
        var errors = new List<Exception>();
        foreach (var test in options.Tests.Tests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A test that fails is run again while the config allows retries; the last attempt's error is the test's, and every attempt's cleanup failure is the run's.
            Report? report = null;
            var attempts = new List<E2E.Reports.ReportAttempt>();
            Exception? error = null;
            for (var attempt = 1; attempt <= config.Retries + 1; attempt++)
            {
                var fixture = new InMemoryFixture(config, options, options.Tests.File);
                IReadOnlyList<Exception> cleanup;
                (report, error, cleanup) = await fixture.RunAsync(test, attempt, cancellationToken).ConfigureAwait(false);

                // A session's report holds its own attempt only; the test's result holds them all, as upstream's does.
                attempts.AddRange(report?.Run.Results.SelectMany(result => result.Attempts) ?? []);
                exitCodes.AddRange(cleanup.Select(failure => ErrorSerializer.ExitCodeForCategory(ErrorSerializer.Classify(failure).Category)));
                if (error is null)
                {
                    break;
                }
            }

            if (report is { Run.Results: [var only] })
            {
                report = report with { Run = report.Run with { Results = [only with { Attempts = attempts, Status = Enum.Parse<E2E.Reports.ResultStatus>(Retry.RetryVerdict([.. attempts.Select(each => new RetryAttempt(Enum.Parse<E2E.Internal.AttemptStatus>(each.Status.ToString())))]).ToString()) }] } };
            }

            first ??= report;
            results.AddRange(report?.Run.Results ?? []);
            if (error is not null)
            {
                errors.Add(error);
            }

            exitCodes.Add(error is null ? 0 : error is E2EException known ? ExitCodeOf(known) : ErrorSerializer.ExitCodeForCategory(ErrorSerializer.Classify(error).Category));
        }

        var cleanupFailed = exitCodes.Any(code => code != 0) && errors.Count == 0;
        var status = cleanupFailed ? RunStatus.Failed : errors.Count == 0 ? RunStatus.Passed : errors.All(error => error is E2EException known && StatusOf(known) == RunStatus.Blocked) ? RunStatus.Blocked : RunStatus.Failed;
        var exitCode = ErrorSerializer.CombineExitCodes(exitCodes);
        var merged = first is null ? new Report() : first with { Run = first.Run with { Results = results, ExitCode = exitCode, Status = status == RunStatus.Passed ? E2E.ReportRunStatus.Passed : status == RunStatus.Blocked ? E2E.ReportRunStatus.Blocked : E2E.ReportRunStatus.Failed } };
        return new RunOutcome(exitCode, status, merged);
    }

    /// <summary>
    /// The fixture one in-memory test runs in (.NET-only: upstream's worker builds the test's fixtures from the resolved config; in the port a
    /// session is started by <see cref="E2EFixture"/>, whose settings this takes from the run's options).
    /// </summary>
    private sealed class InMemoryFixture(E2EConfig config, RunOptions options, string file) : E2EFixture
    {
        protected override E2EConfig Config => config;

        internal override string? TestFile => file;

        protected override IEngine CreateEngine() => options.Engine ?? TargetEngines.Create(Config.Target, null, null, null);

        protected override string? ResultsDirectory => OutputLayout.Of(Config.ResolveOutput(options.Output)).Results;

        private AgentOptions Default => options.Agents?.GetValueOrDefault("default") ?? Config.Agent.CreateOptions();

        protected override IAgentModel? CreateModel() => Default.Model;

        protected override IAgentModel? CreateJudge() => Default.Judge;

        protected override IStepExecutor? AgentExecutor => Default.Executor;

        protected override string? AgentSystem => Default.System;

        protected override IReadOnlyDictionary<string, AgentTool>? AgentTools => Default.Tools;

        protected override string? AgentContext => Default.Context;

        protected override IReadOnlyDictionary<string, AgentOptions> CreateAgents()
        {
            var agents = Config.Agents
                .Where(pair => !string.Equals(pair.Key, "default", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value.CreateOptions(), StringComparer.Ordinal);
            foreach (var pair in options.Agents ?? new Dictionary<string, AgentOptions>())
            {
                if (!string.Equals(pair.Key, "default", StringComparison.Ordinal))
                {
                    agents[pair.Key] = pair.Value;
                }
            }

            return agents;
        }

        /// <summary>Runs the test as attempt <paramref name="attempt"/> and closes its session. What the body throws is the test's error; the session's close failures are the cleanup's.</summary>
        public async Task<(Report? Report, Exception? Error, IReadOnlyList<Exception> Cleanup)> RunAsync(InMemoryTest test, int attempt, CancellationToken cancellationToken)
        {
            try
            {
                await StartSessionAsync(test.Title, attempt, null, () => false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A session that cannot start is the test's failure, with the exit code of its class, not an exception out of the run.
                return (null, ex, []);
            }

            var session = TakeSession()!;
            Exception? error = null;
            IReadOnlyList<Exception> cleanup;
            try
            {
                try
                {
                    await test.Body(session).ConfigureAwait(false);
                    await session.SettleAttemptAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Whatever the body throws fails the test, as upstream's runner reads any error a test throws.
                    error = ex;
                    await session.CaptureFailureEvidenceAsync(ex).ConfigureAwait(false);
                }

                session.Complete(error, cancellationToken);
            }
            finally
            {
                cleanup = await session.CloseAsync().ConfigureAwait(false);
            }

            return (session.Report(), error, cleanup);
        }
    }

    private static int AgentCodeExit(string code) => ErrorSerializer.ExitCodeForCategory(AgentError.CategoryByCode.GetValueOrDefault(code, "test"));

    /// <summary>How long a reporter's <c>OnRunFinishedAsync</c> may take before the run stops waiting for it (upstream's <c>REPORTER_TIMEOUT_MS</c>).</summary>
    public static readonly TimeSpan ReporterTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Awaits every reporter's <c>OnRunFinishedAsync</c> at once, each within <paramref name="timeout"/> and until <paramref name="force"/> (a forced
    /// stop of the run), and collects the rows they return. A reporter that throws, runs out of time, or returns a row without a label and text is one
    /// line on stderr; it can never change the run's outcome. Giving up on a reporter that is still running sets <see cref="AbandonedWork.LeftWorkBehind"/>.
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
                    AbandonedWork.LeaveWorkBehind();
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
                AbandonedWork.LeaveWorkBehind();
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
