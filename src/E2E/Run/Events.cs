// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Structured run events: the run's single event spine (upstream's <c>run/events.ts</c>). The list reporter consumes exactly this
/// stream (<c>ListReporter.Handle</c>), so no two consumers can drift. Only the events the port's list reporter reads are here: the
/// run's start, plan, notices, setup steps, finished tests, errors, interrupts, and end. Not ported: <c>output</c>,
/// <c>test-started</c>, <c>step</c>, <c>serial-group</c>, and <c>explore</c>, which feed the live window the port has no use for yet.
/// </summary>
internal abstract record RunEventFact;

/// <summary>Upstream's <c>run-started</c> fact.</summary>
internal sealed record RunStartedEvent : RunEventFact
{
    public required string RunId { get; init; }

    public required string ProjectId { get; init; }

    public required string ProjectRoot { get; init; }

    /// <summary>Absolute directory the report's artifact paths are relative to.</summary>
    public required string ArtifactsRoot { get; init; }

    public bool Ci { get; init; }

    public required IReadOnlyList<string> Targets { get; init; }

    /// <summary>The configured agents unpinned tests run as, when they are not <c>default</c> alone; null otherwise.</summary>
    public IReadOnlyList<string>? Agents { get; init; }

    /// <summary>The run agent's model as <c>provider/model-id</c>; null when none is configured or the run names several agents.</summary>
    public string? Model { get; init; }

    /// <summary>The run agent's judge, in the same form; null when judgments use <see cref="Model"/>.</summary>
    public string? Judge { get; init; }
}

/// <summary>
/// Upstream's <c>plan</c> fact: what the run will execute, emitted once collection, selection, and every target's setup are done.
/// From here the run executes, so the list reporter's clock starts with it. <c>Files</c> is upstream's inline <c>{ file, target, tests }</c> list, one tuple per test file on one target.
/// </summary>
internal sealed record PlanEvent(int Total, IReadOnlyList<(string File, string Target, int Tests)> Files) : RunEventFact;

/// <summary>Upstream's <c>notice</c> fact: one line of run-level progress outside any test.</summary>
internal sealed record NoticeEvent(string Target, string Message) : RunEventFact;

/// <summary>
/// What one setup step is about (upstream's <c>SetupStep</c>, <c>run/events.ts</c>). <c>collect</c> loads the test files; <c>filter</c> is one test
/// filter, by its name; <c>prepare</c> is one target's engine provisioning; <c>service</c> and <c>app</c> are the processes the run owns, labelled
/// the way the runner names them (<c>service "postgres"</c>, <c>target "web" command</c>).
/// </summary>
internal abstract record SetupStep;

/// <summary>Upstream's <c>{ kind: 'collect' }</c> step.</summary>
internal sealed record CollectSetupStep : SetupStep;

/// <summary>Upstream's <c>{ kind: 'filter' }</c> step.</summary>
internal sealed record FilterSetupStep(string Name) : SetupStep;

/// <summary>Upstream's <c>{ kind: 'prepare' }</c> step.</summary>
internal sealed record PrepareSetupStep(string Target, string Engine) : SetupStep;

/// <summary>Upstream's <c>{ kind: 'service' }</c> step.</summary>
internal sealed record ServiceSetupStep(string Label) : SetupStep;

/// <summary>
/// Upstream's <c>{ kind: 'app' }</c> step. <paramref name="Url"/> is the one its readiness probe polls, with a free port already assigned and
/// without userinfo or query; null for a command that is ready when it exits.
/// </summary>
internal sealed record AppSetupStep(string Label, string? Url = null) : SetupStep;

/// <summary>
/// How a setup step ended other than plainly done (upstream's <c>SetupOutcome</c>): <c>Reused</c> when a service or app command attached to a process
/// already serving its URL instead of spawning; <c>Failed</c> and <c>TimedOut</c> for a test filter (upstream's <c>FilterOutcome</c>).
/// </summary>
internal enum SetupOutcome
{
    Reused,
    Failed,
    TimedOut,
}

/// <summary>Whether a <see cref="SetupEvent"/> opens its step or closes it.</summary>
internal enum SetupState
{
    Started,
    Finished,
}

/// <summary>
/// Upstream's <c>setup</c> fact: the work before the tests start. Upstream has two members of one type, <c>started</c> and <c>finished</c>; here
/// <see cref="DurationMs"/> and <see cref="Outcome"/> are set only when <see cref="State"/> is <see cref="SetupState.Finished"/>.
/// </summary>
internal sealed record SetupEvent(SetupStep Step, SetupState State, long DurationMs = 0, SetupOutcome? Outcome = null) : RunEventFact;

/// <summary>Who a test is (upstream's <c>TestIdentity</c>, <c>collect/collect.ts</c>).</summary>
internal sealed record TestIdentity(IReadOnlyList<string> TitlePath, string Id, string File, int DeclarationIndex);

/// <summary>Upstream's <c>RunEventResult</c>: a test's result with its target's identity. The attempts are the report's.</summary>
internal sealed record RunEventResult
{
    public required TestIdentity Test { get; init; }

    /// <summary>The target the result ran on, by its stable identity: upstream's inline <c>{ name, platform }</c>.</summary>
    public required (string Name, string Platform) Target { get; init; }

    /// <summary>
    /// The report id of the result, which keys its trace page in <see cref="RunFinishedEvent.Traces"/>. Upstream derives it with
    /// <c>resultId</c> (<c>internal/ids.ts</c>); the port has no such digest, so the producer supplies it.
    /// </summary>
    public required string ResultId { get; init; }

    /// <summary>The configured agent the test ran as.</summary>
    public string Agent { get; init; } = "default";

    /// <summary>Which run of the test this is under repeat-each, 0 for the first.</summary>
    public int Repeat { get; init; }

    public required ResultStatus Status { get; init; }

    public bool Selected { get; init; } = true;

    public ReportSkip? Skip { get; init; }

    public IReadOnlyList<ReportAttempt> Attempts { get; init; } = [];
}

/// <summary>Upstream's <c>test-finished</c> fact.</summary>
internal sealed record TestFinishedEvent(RunEventResult Result) : RunEventFact;

/// <summary>Upstream's <c>run-error</c> fact: a run-level error, which aborts before, between, or after test execution.</summary>
internal sealed record RunErrorEvent(ReportError Error) : RunEventFact;

/// <summary>How an interrupt landed: the running test is interrupted and its teardown runs, or (a second interrupt) every worker is torn down at once.</summary>
internal enum InterruptMode
{
    Graceful,
    Forced,
}

/// <summary>Upstream's <c>run-interrupted</c> fact.</summary>
internal sealed record RunInterruptedEvent(InterruptMode Mode) : RunEventFact;

/// <summary>Upstream's <c>run-stopped</c> fact: the run reached its failure limit.</summary>
internal sealed record RunStoppedEvent(int Failures, int Limit) : RunEventFact;

/// <summary>Upstream's <c>run-finished</c> fact.</summary>
internal sealed record RunFinishedEvent : RunEventFact
{
    public required ReportRunStatus Status { get; init; }

    public int ExitCode { get; init; }

    public string? ReportPath { get; init; }

    /// <summary>Where the run's model calls were written, once the file exists.</summary>
    public string? AiTracePath { get; init; }

    /// <summary>The trace page written for each test that kept a trace, by report result id, as a path from the project root.</summary>
    public IReadOnlyDictionary<string, string>? Traces { get; init; }
}

/// <summary>One consumer of the run's events (upstream's <c>RunEventSubscriber</c>): a name for the diagnostic and the sink.</summary>
internal sealed record RunEventSubscriber(string Name, Action<RunEventFact> OnEvent);

/// <summary>The run's event writer (upstream's <c>createRunEventEmitter</c>, <c>run/events.ts</c>).</summary>
internal static partial class RunEvents
{
    /// <summary>
    /// Builds the run's single event writer over every subscriber. Subscribers are quarantined independently, so one broken sink cannot silence
    /// another, and each quarantine is one stderr line naming the sink, the event, and the error. Returns a no-op when there is no subscriber, so
    /// call sites never branch. Not ported: the <c>seq</c> and <c>at</c> envelope, and async sinks (the port's sinks are synchronous).
    /// </summary>
    public static Action<RunEventFact> CreateRunEventEmitter(IEnumerable<RunEventSubscriber?> subscribers)
    {
        var active = subscribers.OfType<RunEventSubscriber>().ToList();
        if (active.Count == 0)
        {
            return _ => { };
        }

        var quarantined = new HashSet<RunEventSubscriber>();
        return fact =>
        {
            foreach (var subscriber in active)
            {
                if (quarantined.Contains(subscriber))
                {
                    continue;
                }

                try
                {
                    subscriber.OnEvent(fact);
                }
                catch (Exception cause)
                {
                    quarantined.Add(subscriber);
                    Console.Error.WriteLine($"e2e: reporter \"{subscriber.Name}\" threw on {EventType(fact)}: {cause.Message}; ignoring it for the rest of the run");
                }
            }
        };
    }

    // The fact as upstream's `type` names it: `SetupEvent` is `setup`, `RunStartedEvent` is `run-started`.
    private static string EventType(RunEventFact fact)
    {
        var name = fact.GetType().Name;
        return TypeWords().Replace(name.EndsWith("Event", StringComparison.Ordinal) ? name[..^"Event".Length] : name, "$1-$2").ToLowerInvariant();
    }

    [System.Text.RegularExpressions.GeneratedRegex("([a-z])([A-Z])")]
    private static partial System.Text.RegularExpressions.Regex TypeWords();
}
