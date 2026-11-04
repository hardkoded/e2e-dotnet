// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>
/// One test process's run, as a run fixture drives it (.NET-only: upstream's runner owns the run's start, plan, and end, and builds the reporters from
/// its config; a .NET test framework has no such owner, so the run fixtures of <c>E2E.NUnit</c> and <c>E2E.XUnit.V3</c> call this when the first test is about to
/// start and when the last is done). The reporters are built when the run begins and subscribed to the process's events, so a reporter's
/// <see cref="Reporter.OnEvent"/> sees the run's start and the same instances run <see cref="Reporter.OnRunFinishedAsync"/> at its end.
/// </summary>
internal sealed class HostRun : IDisposable
{
    private static string? _rerunDir;

    private readonly IReadOnlyList<Reporter> _reporters;
    private readonly List<IDisposable> _subscriptions = [];

    private HostRun(IReadOnlyList<Reporter> reporters)
    {
        _reporters = reporters;
        foreach (var reporter in reporters)
        {
            _subscriptions.Add(RunEvents.Subscribe(new RunEventSubscriber(NameOf(reporter), reporter.OnEvent)));
        }
    }

    /// <summary>
    /// Builds the reporters of the config and subscribes them to the run's events. A config that cannot load, or a reporter that cannot be built, is one line on stderr:
    /// a reporter can never change the run's outcome. <paramref name="config"/> is called once.
    /// </summary>
    public static HostRun Begin(Func<E2EConfig> config)
    {
        ArgumentNullException.ThrowIfNull(config);
        try
        {
            return new HostRun(Reporters.Create(config()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"e2e: reporters failed: {ex.Message}");
            return new HostRun([]);
        }
    }

    /// <summary>A run over reporters that are already built, listening from now on. .NET-only: a test hands in the reporter objects, as upstream's config does, where a run fixture builds them from the config's type names.</summary>
    internal static HostRun Begin(IReadOnlyList<Reporter> reporters)
    {
        ArgumentNullException.ThrowIfNull(reporters);
        return new HostRun(reporters);
    }

    /// <summary>
    /// The directory the attempts of this process go in, inside each test's, when the run is a <c>--last-failed</c> rerun (.NET-only: upstream's runner hands
    /// <c>rerunDir</c> to the workers it starts; a .NET test framework starts the tests, so the run fixture sets it once, before the first test). Null for any other run.
    /// </summary>
    internal static string? RerunDir
    {
        get => Volatile.Read(ref _rerunDir);
        set => Volatile.Write(ref _rerunDir, value);
    }

    /// <summary>
    /// The run began (<c>run-started</c>). The run's id is the process's: every report this process writes carries it. A run that explores
    /// one target as one agent names them: <paramref name="targets"/> are the targets it runs on, and <paramref name="agentName"/> the agent whose model it shows.
    /// </summary>
    public static void Started(E2EConfig config, IReadOnlyList<string>? targets = null, string agentName = "default")
    {
        ArgumentNullException.ThrowIfNull(config);
        var agent = config.Agents.GetValueOrDefault(agentName);
        var judge = agent?.Judge is { } judged && judged != agent.Model ? ModelLabel(agent, judged) : null;
        RunEvents.Emit(new RunStartedEvent
        {
            RunId = AttemptReport.RunId,
            ProjectId = Path.GetFileName(Path.TrimEndingDirectorySeparator(config.ProjectRoot)),
            ProjectRoot = config.ProjectRoot,
            ArtifactsRoot = OutputLayout.Of(config.Output).Results,
            Ci = config.Ci,
            Targets = targets ?? [.. config.Targets.Select(target => target.Name)],
            Agents = agentName == "default" ? null : [agentName],
            Model = agent?.Model is { } model ? ModelLabel(agent, model) : null,
            Judge = judge,
        });
    }

    /// <summary>Finds the tests of the run (the <c>collect</c> setup step, with how long it took).</summary>
    public static T Collect<T>(Func<T> find)
    {
        ArgumentNullException.ThrowIfNull(find);
        var step = new CollectSetupStep();
        RunEvents.Emit(new SetupEvent(step, SetupState.Started));
        var started = Stopwatch.GetTimestamp();
        var found = find();
        RunEvents.Emit(new SetupEvent(step, SetupState.Finished, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        return found;
    }

    /// <summary>
    /// What the run is about to execute (<c>plan</c>), once collection and selection are done. Partial: an estimate from the tests found by reflection, which are the
    /// test methods of the assembly, one per method however many cases the framework expands it to. A test the framework ignores, or a filter left out,
    /// is not counted; a test the framework cases expand, or that skips itself while it runs, is counted as the method it is. The file is the
    /// full name of the test's class, as a session names it (see <see cref="E2ESessionOptions.TestFile"/>).
    /// </summary>
    public static void Planned(string target, IReadOnlyList<DiscoveredTest> discovered, IReadOnlyDictionary<string, string> excluded)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentNullException.ThrowIfNull(excluded);
        var files = discovered
            .Where(test => test.SkipReason is null && !excluded.ContainsKey(test.Key))
            .GroupBy(test => FileOf(test), StringComparer.Ordinal)
            .Select(group => (File: group.Key, Target: target, Tests: group.Count()))
            .ToList();
        RunEvents.Emit(new PlanEvent(files.Sum(file => file.Tests), files));
    }

    /// <summary>A failure that stopped the run before, between, or after its tests (<c>run-error</c>). The failure is the caller's to rethrow.</summary>
    public static void Failed(E2EConfig config, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(failure);
        try
        {
            RunEvents.Emit(new RunErrorEvent(EventWire.Error(failure, config.ProjectRoot, Redactor.For(config.AllSecrets).Redact)));
        }
#pragma warning disable CA1031 // Events are a courtesy: a failure to describe the failure must not hide it.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine("[e2e] could not emit a run event: " + ex.Message);
        }
    }

    /// <summary>
    /// The run ended: every run this process wrote a report for is told to the reporters (<c>run-finished</c>), then each reporter's
    /// <see cref="Reporter.OnRunFinishedAsync"/> runs on it and the rows they return print. A reporter can never change the run's outcome, so a failure is one line on stderr.
    /// </summary>
    public async Task FinishAsync(TimeSpan? timeout = null, CancellationToken force = default)
    {
        if (_reporters.Count == 0 && !RunEvents.HasSubscribers)
        {
            return;
        }

        try
        {
            foreach (var finished in await ReportFile.FinishedRunsAsync().ConfigureAwait(false))
            {
                RunEvents.Emit(new RunFinishedEvent
                {
                    Status = finished.Status,
                    ExitCode = finished.ExitCode,
                    ReportPath = finished.ReportPath,
                    AiTracePath = finished.AiTracePath,
                    Traces = finished.Traces.Count == 0 ? null : finished.Traces,
                });
                if (_reporters.Count == 0)
                {
                    continue;
                }

                var rows = await Runner.RunReportersAsync(_reporters, finished, timeout ?? Runner.ReporterTimeout, force).ConfigureAwait(false);
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

    /// <summary>Stops the reporters listening to the run's events.</summary>
    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    // The class a session names as its test file; a nested class is joined with `+`, as the runtime's full name does.
    private static string FileOf(DiscoveredTest test) => test.Namespace.Length == 0 ? test.TypeName : test.Namespace + "." + test.TypeName;

    private static string ModelLabel(AgentConfig agent, string model) => agent.Provider + "/" + model;

    // A Name that throws is the reporter's fault; it must not cost the other reporters their events.
    private static string NameOf(Reporter reporter)
    {
        try
        {
            return reporter.Name;
        }
        catch (Exception)
        {
            return reporter.GetType().Name;
        }
    }
}
