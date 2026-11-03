// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Reports;

/// <summary>What a reporter receives once the run is over and its report is on disk (upstream's <c>FinishedRun</c>, <c>types.ts</c>).</summary>
/// <param name="Report">The report-1 document, as written to <paramref name="ReportPath"/>.</param>
/// <param name="Status">The same status <c>Report.Run.Status</c> carries.</param>
/// <param name="ExitCode">The exit code the run ended with (upstream's <c>exitCode</c>). The test process's own exit code is the test framework's.</param>
/// <param name="ProjectRoot">Absolute project root, what the terminal shows paths relative to.</param>
/// <param name="ReportPath">Where <c>report.json</c> was written.</param>
/// <param name="ArtifactsRoot">Absolute directory the report's artifact paths are relative to: <c>&lt;output&gt;/results</c>.</param>
public sealed record FinishedRun(Report Report, ReportRunStatus Status, int ExitCode, string ProjectRoot, string ReportPath, string ArtifactsRoot)
{
    /// <summary>
    /// The trace page the process wrote for each test that kept a trace, by report result id, as a path from <see cref="ProjectRoot"/>
    /// (<c>.e2e/results/checkout-applies-the-coupon-1a2b3c4d5e6f7a8b/trace.md</c>). Empty when no test kept one. Upstream's <c>traces</c>, a map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Traces { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Where <c>E2E_AI_TRACE=1</c> wrote the process's model calls, once the file exists (<c>ai-trace-&lt;process id&gt;.json</c> in the output directory,
    /// one file for the whole process); null when the process recorded none. Upstream's <c>aiTracePath</c>.
    /// </summary>
    public string? AiTracePath { get; init; }

    /// <summary>
    /// The report of the run this one reran, when it was a <c>--last-failed</c> rerun (<c>E2E_LAST_FAILED</c>): the <c>report.json</c> it selected from, read before this
    /// run replaced it. A reporter can fold the two into one view of the suite. Null for any other run. Upstream's <c>lastRun</c>.
    /// </summary>
    public Report? LastRun { get; init; }
}

/// <summary>One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's <c>ReporterSummary</c> element).</summary>
public sealed record ReporterSummaryRow(string Label, string Text);

/// <summary>
/// A reporter, listed in the <c>reporters</c> of <c>e2e.config.json</c> (upstream's <c>Reporter</c>, <c>types.ts</c>). <see cref="OnRunFinishedAsync"/> runs
/// once, when the test run is over and <c>report.json</c> is final; the run is awaited for one minute, and the rows it returns print under the run's
/// output. The token is cancelled when that budget runs out or the run is forced to stop, so a reporter hands it to its requests and leaves nothing
/// running behind. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. The test run ends when the
/// run fixture (<c>E2ERunFixture</c> of <c>E2E.NUnit</c> or <c>E2E.XUnit.V3</c>) is torn down, so a run without it calls no reporter. A reporter
/// has a public constructor with no parameters and no side effects; one that takes options also implements <see cref="IConfigurableReporter"/>. The run fixture
/// builds the reporters when the run starts, so <see cref="OnEvent"/> sees the run's events as they happen, and calls the same instances when it ends.
/// </summary>
public abstract class Reporter
{
    /// <summary>Label in diagnostics.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Every run event as it happens, exactly what the list reporter renders (upstream's <c>onEvent</c>). Must not block or throw: a throw quarantines this
    /// reporter's events for the rest of the run, with one line on stderr, and never changes the run's outcome. Event types are added over time; a
    /// reporter handles the ones it knows and ignores the rest. Events arrive in <see cref="RunEvent.Seq"/> order, one at a time, from whichever thread
    /// emitted them. A retried test emits <see cref="TestStartedEvent"/> and <see cref="TestFinishedEvent"/> once per attempt, the later result holding every attempt so far.
    /// </summary>
    public virtual void OnEvent(RunEvent runEvent)
    {
    }

    /// <summary>Runs once the report is written; the returned rows print under the run's output. Null prints nothing.</summary>
    public virtual Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReporterSummaryRow>?>(null);
}

/// <summary>
/// A <see cref="Reporter"/> that takes the <c>options</c> of its <c>e2e.config.json</c> entry. .NET-only: upstream's config lists reporter
/// objects built in code (<c>github(options)</c>), and a JSON entry holds no code, so the options arrive as JSON.
/// </summary>
public interface IConfigurableReporter
{
    /// <summary>
    /// Called once, right after the reporter is built, and only when the entry has <c>options</c>. The reporter rejects content it does not
    /// know with a clear error that names the key, never the value: <c>e2e.config.json</c> is committed, so options hold no tokens.
    /// </summary>
    void Configure(JsonElement options);
}
