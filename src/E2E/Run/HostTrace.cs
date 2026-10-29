// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>What <see cref="HostTrace.WriteHostTraceAsync"/> is given. Upstream's <c>HostTraceOptions</c> (<c>run/host-trace.ts</c>).</summary>
internal sealed record HostTraceOptions
{
    /// <summary>
    /// The attempt as a report of one result, as <see cref="AttemptReport.Build"/> returns it. It stands for upstream's <c>config</c>,
    /// <c>target</c>, <c>runId</c>, <c>testId</c>, <c>title</c>, <c>agent</c> and <c>attempt</c>, which the port's attempt report already holds.
    /// </summary>
    public required Report1Document Attempt { get; init; }

    /// <summary>The project root the page's artifact directory is told from.</summary>
    public required string ProjectRoot { get; init; }

    /// <summary>The results directory the page's artifact links resolve against.</summary>
    public required string ResultsDir { get; init; }

    /// <summary>Path segments of the attempt's directory under <see cref="ResultsDir"/>.</summary>
    public required IReadOnlyList<string> Segments { get; init; }

    public required string TracePath { get; init; }

    public required Func<string, string> Redact { get; init; }
}

/// <summary>The per-session <c>trace.md</c> a hosted attempt writes at close, through the run report's renderer. Upstream's <c>run/host-trace.ts</c>.</summary>
internal static class HostTrace
{
    /// <summary>Writes one session's trace page as a single-result run and returns its <see cref="Report"/>. Upstream's <c>writeHostTrace</c>.</summary>
    public static async Task<Report> WriteHostTraceAsync(HostTraceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var single = options.Attempt;
        var document = await ReportFile.SingleAsync(single, options.ProjectRoot, options.ResultsDir, options.Redact).ConfigureAwait(false);
        var report = ReportSerializer.Read(document);
        var result = single.Run.Results[0];
        var page = FailureText.RenderTracePage(
            single,
            result,
            Outcome.Of(result),
            new TracePageOptions
            {
                Standalone = true,
                ArtifactsDir = Source.ProjectRelativePath(options.ProjectRoot, options.ResultsDir) ?? options.ResultsDir.Replace('\\', '/'),
                PageDir = string.Join('/', options.Segments),
                ReadArtifact = reportPath =>
                {
                    try
                    {
                        return File.ReadAllText(Path.Combine(options.ResultsDir, reportPath));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        return null;
                    }
                },
            });
        await WriteReport.WriteTextReportAsync(options.TracePath, options.Redact(page)).ConfigureAwait(false);
        return report;
    }
}
