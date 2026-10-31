// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Reports;

/// <summary>What a reporter receives once the run is over and its report is on disk (upstream's <c>FinishedRun</c>, <c>types.ts</c>).</summary>
/// <param name="Report">The report-1 document, as written to <paramref name="ReportPath"/>.</param>
/// <param name="Status">The same status <c>Report.Run.Status</c> carries.</param>
/// <param name="ExitCode">The exit code the run ended with (upstream's <c>exitCode</c>). The test process's own exit code is the test framework's.</param>
/// <param name="ProjectRoot">Absolute project root, what the terminal shows paths relative to.</param>
/// <param name="ReportPath">Where <c>report.json</c> was written.</param>
/// <param name="ArtifactsRoot">Absolute directory the report's artifact paths are relative to: <c>&lt;output&gt;/results</c>.</param>
public sealed record FinishedRun(Report Report, ReportRunStatus Status, int ExitCode, string ProjectRoot, string ReportPath, string ArtifactsRoot);

/// <summary>One row a reporter hands back for the terminal summary: a label and its text, a URL or a path (upstream's <c>ReporterSummary</c> element).</summary>
public sealed record ReporterSummaryRow(string Label, string Text);

/// <summary>
/// A reporter, registered with <see cref="E2EReporterAttribute"/> (upstream's <c>Reporter</c>, <c>types.ts</c>). <see cref="OnRunFinishedAsync"/> runs
/// once, when the test run is over and <c>report.json</c> is final; the run is awaited for one minute, and the rows it returns print under the run's
/// output. The token is cancelled when that budget runs out or the run is forced to stop, so a reporter hands it to its requests and leaves nothing
/// running behind. A reporter can never change the run's outcome: a failure or a timeout is one line on stderr. The test run ends when the
/// run fixture (<c>E2ERunFixture</c> of <c>E2E.NUnit</c> or <c>E2E.XUnit.V3</c>) is torn down, so a run without it calls no reporter. A reporter
/// has a public constructor with no parameters and no side effects. Not ported: <c>onEvent</c>, since the port emits no run events.
/// </summary>
public abstract class Reporter
{
    /// <summary>Label in diagnostics.</summary>
    public abstract string Name { get; }

    /// <summary>Runs once the report is written; the returned rows print under the run's output. Null prints nothing.</summary>
    public virtual Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReporterSummaryRow>?>(null);
}

/// <summary>
/// Registers a <see cref="Reporter"/> for the test assembly: <c>[assembly: E2EReporter(typeof(MyReporter))]</c>. .NET-only: upstream registers
/// reporter objects in the <c>reporters</c> field of its config module, and the port's JSON config holds no code.
/// </summary>
/// <param name="reporterType">A <see cref="Reporter"/> with a public constructor with no parameters.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class E2EReporterAttribute(Type reporterType) : Attribute
{
    /// <summary>The reporter's type.</summary>
    public Type ReporterType { get; } = reporterType;
}
