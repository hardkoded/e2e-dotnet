// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Reports;

/// <summary>
/// Structured run events: the run's single event spine (upstream's <c>run/events.ts</c>), as a <see cref="Reporter"/> sees it through
/// <see cref="Reporter.OnEvent"/>. The list reporter consumes the same stream (<c>ListReporter.Handle</c>), so no two consumers can drift. The
/// port's test run fixtures and sessions emit them: the run's start, plan, notices, setup steps, started tests, step progress, finished tests, errors,
/// interrupts, and end. <c>output</c> is defined and the list reporter prints it, but nothing emits it (the test framework captures stdout). Not ported: <c>serial-group</c> (the port has no serial groups), and
/// <c>explore</c>, which feeds the live window the port has no use for yet. A new event type may be added, so a reporter handles the ones it
/// knows and ignores the rest.
/// </summary>
/// <param name="Seq">The event's place in the run's order, from 1. Upstream's <c>seq</c>.</param>
/// <param name="At">When the event was emitted. Upstream's <c>at</c>.</param>
/// <param name="Fact">What happened. Upstream spreads the fact into the event; a C# record keeps it as a member.</param>
public sealed record RunEvent(long Seq, DateTimeOffset At, RunEventFact Fact);

/// <summary>One fact about the run; the emitter wraps it in the <see cref="RunEvent"/> envelope. One record per upstream event type.</summary>
public abstract record RunEventFact
{
    private protected RunEventFact()
    {
    }
}

/// <summary>Upstream's <c>run-started</c> fact.</summary>
public sealed record RunStartedEvent : RunEventFact
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
public sealed record PlanEvent(int Total, IReadOnlyList<(string File, string Target, int Tests)> Files) : RunEventFact;

/// <summary>Upstream's <c>notice</c> fact: one line of run-level progress outside any test.</summary>
public sealed record NoticeEvent(string Target, string Message) : RunEventFact;

/// <summary>Which stream a test wrote to (upstream's <c>stream</c> of the <c>output</c> fact).</summary>
public enum OutputStream
{
    Stdout,
    Stderr,
}

/// <summary>The test executing when output was written (upstream's inline <c>pair</c> of the <c>output</c> fact): its id, agent, and repeat.</summary>
public sealed record OutputPair(string TestId, string Agent, int Repeat);

/// <summary>
/// Upstream's <c>output</c> fact: text a test wrote to stdout or stderr, as one write on <paramref name="Target"/>. <paramref name="Pair"/> is the
/// test executing at the time, or null for output between tests. The list reporter prints it above its live window. The port's test framework
/// captures stdout, so nothing in the port emits it yet.
/// </summary>
public sealed record OutputEvent(string Target, OutputPair? Pair, OutputStream Stream, string Text) : RunEventFact;

/// <summary>
/// What one setup step is about (upstream's <c>SetupStep</c>, <c>run/events.ts</c>). <c>collect</c> loads the test files; <c>filter</c> is one test
/// filter, by its name; <c>prepare</c> is one target's engine provisioning; <c>service</c> and <c>app</c> are the processes the run owns, labelled
/// the way the runner names them (<c>service "postgres"</c>, <c>target "web" command</c>).
/// </summary>
public abstract record SetupStep
{
    private protected SetupStep()
    {
    }
}

/// <summary>Upstream's <c>{ kind: 'collect' }</c> step.</summary>
public sealed record CollectSetupStep : SetupStep;

/// <summary>Upstream's <c>{ kind: 'filter' }</c> step.</summary>
public sealed record FilterSetupStep(string Name) : SetupStep;

/// <summary>Upstream's <c>{ kind: 'prepare' }</c> step.</summary>
public sealed record PrepareSetupStep(string Target, string Engine) : SetupStep;

/// <summary>Upstream's <c>{ kind: 'service' }</c> step.</summary>
public sealed record ServiceSetupStep(string Label) : SetupStep;

/// <summary>
/// Upstream's <c>{ kind: 'app' }</c> step. <paramref name="Url"/> is the one its readiness probe polls, with a free port already assigned and
/// without userinfo or query; null for a command that is ready when it exits.
/// </summary>
public sealed record AppSetupStep(string Label, string? Url = null) : SetupStep;

/// <summary>
/// How a setup step ended other than plainly done (upstream's <c>SetupOutcome</c>): <c>Reused</c> when a service or app command attached to a process
/// already serving its URL instead of spawning; <c>Failed</c> and <c>TimedOut</c> for a test filter (upstream's <c>FilterOutcome</c>).
/// </summary>
public enum SetupOutcome
{
    Reused,
    Failed,
    TimedOut,
}

/// <summary>Whether a <see cref="SetupEvent"/> opens its step or closes it.</summary>
public enum SetupState
{
    Started,
    Finished,
}

/// <summary>
/// Upstream's <c>setup</c> fact: the work before the tests start. Upstream has two members of one type, <c>started</c> and <c>finished</c>; here
/// <see cref="DurationMs"/> and <see cref="Outcome"/> are set only when <see cref="State"/> is <see cref="SetupState.Finished"/>.
/// </summary>
public sealed record SetupEvent(SetupStep Step, SetupState State, long DurationMs = 0, SetupOutcome? Outcome = null) : RunEventFact;

/// <summary>Who a test is (upstream's <c>TestIdentity</c>, <c>collect/collect.ts</c>).</summary>
public sealed record TestIdentity(IReadOnlyList<string> TitlePath, string Id, string File, int DeclarationIndex);

/// <summary>Upstream's <c>RunEventResult</c>: a test's result with its target's identity. The attempts are the report's.</summary>
public sealed record RunEventResult
{
    public required TestIdentity Test { get; init; }

    /// <summary>The target the result ran on, by its stable identity: upstream's inline <c>{ name, platform }</c>.</summary>
    public required (string Name, string Platform) Target { get; init; }

    /// <summary>The report id of the result, which keys its trace page in <see cref="RunFinishedEvent.Traces"/>.</summary>
    public required string ResultId { get; init; }

    /// <summary>The configured agent the test ran as.</summary>
    public string Agent { get; init; } = "default";

    /// <summary>Which run of the test this is under repeat-each, 0 for the first.</summary>
    public int Repeat { get; init; }

    public required ResultStatus Status { get; init; }

    public bool Selected { get; init; } = true;

    public SkipInfo? Skip { get; init; }

    /// <summary>Every attempt the test has had so far, as the report tells them.</summary>
    public IReadOnlyList<ReportAttempt> Attempts { get; init; } = [];

    /// <summary>
    /// .NET-only: the port's own record of the result, which the list reporter renders from (the report's public tree carries the same facts, in
    /// types the reporter's helpers do not read). Set by the producer in this process; null for an event built from the public members alone.
    /// </summary>
    internal global::E2E.ReportResult? Recorded { get; init; }
}

/// <summary>
/// Upstream's <c>test-started</c> fact: a test began executing. A test that runs as several agents, on several targets, or under repeat-each
/// starts once per pair. <c>SerialId</c> is the test's serial group; the port has no serial groups, so the list reporter does not read it.
/// </summary>
public sealed record TestStartedEvent(string TestId, string Agent, int Repeat, string Title, string File, string? SerialId, string Target) : RunEventFact;

/// <summary>
/// Upstream's <c>step</c> fact: one moment of a running test's step, routed to its pair by test id, agent, target, and repeat. .NET-only name:
/// <c>StepEvent</c> is the report's model-turn record.
/// </summary>
public sealed record StepProgressEvent(string TestId, string Agent, int Repeat, string Target, StepProgress Progress) : RunEventFact;

/// <summary>Upstream's <c>test-finished</c> fact.</summary>
public sealed record TestFinishedEvent(RunEventResult Result) : RunEventFact;

/// <summary>Upstream's <c>run-error</c> fact: a run-level error, which aborts before, between, or after test execution.</summary>
public sealed record RunErrorEvent(ReportError Error) : RunEventFact;

/// <summary>How an interrupt landed: the running test is interrupted and its teardown runs, or (a second interrupt) every worker is torn down at once.</summary>
public enum InterruptMode
{
    Graceful,
    Forced,
}

/// <summary>Upstream's <c>run-interrupted</c> fact.</summary>
public sealed record RunInterruptedEvent(InterruptMode Mode) : RunEventFact;

/// <summary>Upstream's <c>run-stopped</c> fact: the run reached its failure limit.</summary>
public sealed record RunStoppedEvent(int Failures, int Limit) : RunEventFact;

/// <summary>Upstream's <c>run-finished</c> fact.</summary>
public sealed record RunFinishedEvent : RunEventFact
{
    public required ReportRunStatus Status { get; init; }

    public int ExitCode { get; init; }

    public string? ReportPath { get; init; }

    /// <summary>Where the run's model calls were written, once the file exists.</summary>
    public string? AiTracePath { get; init; }

    /// <summary>The trace page written for each test that kept a trace, by report result id, as a path from the project root.</summary>
    public IReadOnlyDictionary<string, string>? Traces { get; init; }
}

