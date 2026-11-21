// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;

namespace E2E.Reports;

/// <summary>
/// The report as JUnit XML in <c>junit.xml</c> beside <c>report.json</c>, for CI test summaries (upstream's <c>junit()</c> reporter,
/// <c>report/junit.ts</c>). Writes nothing when the report itself was not written. Upstream's runner writes the files a reporter's
/// <c>reportFiles</c> returns; the port has no such hook, so this reporter writes its file itself in <see cref="OnRunFinishedAsync"/>,
/// atomically, and a failed write is one line on stderr through the reporter seam. List it in the <c>reporters</c> of <c>e2e.config.json</c>
/// as <c>E2E.Reports.JunitReporter</c>; the optional <c>options.outputFile</c> is upstream's <c>outputFile</c>.
/// </summary>
public sealed class JunitReporter : Reporter, IConfigurableReporter
{
    private string? _outputFile;

    /// <summary>A reporter that writes <c>junit.xml</c> in the output directory.</summary>
    public JunitReporter() { }

    /// <summary>A reporter that writes the XML to <paramref name="outputFile"/>, from the project root (upstream's <c>outputFile</c>).</summary>
    public JunitReporter(string outputFile) => _outputFile = outputFile;

    /// <inheritdoc />
    public override string Name => "junit";

    /// <inheritdoc />
    public void Configure(JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("options must be an object");
        }

        foreach (var option in options.EnumerateObject())
        {
            if (option.Name != "outputFile")
            {
                throw new ArgumentException(
                    $"option \"{option.Name}\" is not supported; the only option is \"outputFile\""
                );
            }

            _outputFile =
                option.Value.ValueKind == JsonValueKind.String
                    ? option.Value.GetString()
                    : throw new ArgumentException("option \"outputFile\" must be a string");
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(
        FinishedRun run,
        CancellationToken cancellationToken
    )
    {
        var outputDirectory = Path.GetDirectoryName(run.ReportPath) ?? run.ProjectRoot;
        var path = _outputFile is null
            ? Path.Combine(outputDirectory, "junit.xml")
            : Path.GetFullPath(_outputFile, run.ProjectRoot);

        // The canonical report is the run's own file; a reporter never replaces it.
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(run.ReportPath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("the JUnit file would replace report.json; choose another outputFile");
        }

        var xml = JunitReport.RenderJunitReport(
            run.Report,
            new JunitReportOptions { ArtifactsDir = JunitReport.AttachmentBase(run) }
        );
        await WriteReport.WriteTextReportAsync(path, xml, cancellationToken).ConfigureAwait(false);
        return [new ReporterSummaryRow("JUnit", Paths.DisplayPath(run.ProjectRoot, path))];
    }
}
