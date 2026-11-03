// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E;

/// <summary>
/// The run events of one session (.NET-only: upstream's runner emits them for the pairs it dispatches; a .NET test run has no runner, so each
/// session, which is one attempt of a test, reports its own start, its steps, and its end to the process's events, see <see cref="RunEvents"/>).
/// A test that retries emits <c>test-started</c> and <c>test-finished</c> once per attempt; the later result holds every attempt so far. The
/// port has no repeat-each and no test declaration order, so <c>Repeat</c> and <c>DeclarationIndex</c> are 0.
/// </summary>
internal sealed class SessionEvents
{
    private const string UnknownFile = "unknown";

    private readonly string _file;
    private readonly string _testId;
    private readonly string _title;
    private readonly string _agent;
    private readonly string _target;

    private SessionEvents(string file, string title, string agent, string target)
    {
        _file = file;
        _title = title;
        _agent = agent;
        _target = target;
        _testId = AttemptReport.TestId(file, title);
    }

    /// <summary>The events of the session <paramref name="options"/> starts on <paramref name="target"/>; null for a session that is not a test (an exploration).</summary>
    public static SessionEvents? For(E2ESessionOptions options, string target) =>
        options.EmitRunEvents ? new SessionEvents(string.IsNullOrEmpty(options.TestFile) ? UnknownFile : options.TestFile, options.TestTitle, options.Agent ?? "default", target) : null;

    /// <summary>The attempt began executing (<c>test-started</c>).</summary>
    public void Started() => Emit(new TestStartedEvent(_testId, _agent, 0, _title, _file, null, _target));

    /// <summary>Feeds each step's progress to the events (<c>step</c>), then to <paramref name="inner"/>, the progress the session's own caller listens to.</summary>
    public Action<StepProgress>? Progress(Action<StepProgress>? inner) =>
        progress =>
        {
            inner?.Invoke(progress);
            if (RunEvents.HasSubscribers)
            {
                Emit(() => EventWire.Step(progress, _testId, _agent, _target));
            }
        };

    /// <summary>
    /// The attempt ended (<c>test-finished</c>): <paramref name="result"/> is the test's result with every attempt so far, as <see cref="ReportFile"/> was given it, and
    /// <paramref name="resultsRoot"/> is the directory its artifact paths start at.
    /// </summary>
    public void Finished(ReportResult result, string resultsRoot, Func<string, string> redact)
    {
        if (!RunEvents.HasSubscribers)
        {
            return;
        }

        Emit(() => new TestFinishedEvent(EventWire.Result(result, new TestIdentity([_title], _testId, _file, 0), resultsRoot, redact)));
    }

    private static void Emit(RunEventFact fact) => RunEvents.Emit(fact);

    // A producer that cannot build its fact must not fail the test it reports: the events are a courtesy, as the report is.
    private static void Emit(Func<RunEventFact> build)
    {
        try
        {
            RunEvents.Emit(build());
        }
#pragma warning disable CA1031 // Events are a courtesy: whatever fails in building one must not fail a test that passed.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine("[e2e] could not emit a run event: " + ex.Message);
        }
    }
}
