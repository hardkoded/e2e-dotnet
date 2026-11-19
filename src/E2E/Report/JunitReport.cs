// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Internal;
using static E2E.Reports.FailureText;

namespace E2E.Reports;

/// <summary>Options for rendering JUnit reports (upstream's <c>JunitReportOptions</c>).</summary>
public sealed record JunitReportOptions
{
    /// <summary>
    /// Where artifact files are located relative to the repository root or CI working directory (such as GitLab's <c>$CI_PROJECT_DIR</c>). When
    /// set, a test whose failure screenshot has a local <c>Path</c> (a URL-only screenshot gets none) gains <c>&lt;system-out&gt;[[ATTACHMENT|&lt;path&gt;]]&lt;/system-out&gt;</c>.
    /// </summary>
    public string? ArtifactsDir { get; init; }
}

/// <summary>
/// The JUnit XML renderer (upstream's <c>report/junit.ts</c>): the report as <c>junit.xml</c>, for CI test summaries. Like every reporter it
/// reads the document, never runner state, and it can never change run status.
/// </summary>
public static partial class JunitReport
{
    private sealed class SuiteCounts
    {
        public int Tests { get; set; }

        public int Failures { get; set; }

        public int Errors { get; set; }

        public int Skipped { get; set; }

        public long TimeMs { get; set; }
    }

    private enum CaseOutcome
    {
        Passed,
        Failure,
        Error,
        Skipped,
    }

    private sealed record RenderedCase(IReadOnlyList<string> Lines, CaseOutcome Outcome, long DurationMs);

    /// <summary>
    /// Renders the report-1 document as JUnit XML: one <c>&lt;testsuite&gt;</c> per test file (in report order, so the output is completion-time
    /// independent), one <c>&lt;testcase&gt;</c> per test-target pair, plus a <c>run</c> suite for run-level errors. A pair a test filter left
    /// out is a skipped case naming the filter, so a run the filters left empty still lists its suite. Upstream's <c>renderJunitReport</c>.
    /// </summary>
    /// <param name="report">The report a reporter receives (<see cref="FinishedRun.Report"/>).</param>
    /// <param name="options">What attachments are relative to; <see langword="null"/> for none.</param>
    /// <returns>The XML document, ending in a newline.</returns>
    public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
    {
        List<(string File, List<RenderedCase> Cases)> byFile = [];
        foreach (var result in report.Run.Results)
        {
            // Other unselected results are report-only; a CI viewer counts what the run selected.
            if (!result.Selected && result.Skip?.Filter is null)
            {
                continue;
            }

            var entry = byFile.FindIndex(item => string.Equals(item.File, result.File, StringComparison.Ordinal));
            if (entry < 0)
            {
                byFile.Add((result.File, []));
                entry = byFile.Count - 1;
            }

            byFile[entry].Cases.Add(RenderResult(result, options));
        }

        var suites = byFile.Select(item => RenderSuite(item.File, item.Cases)).ToList();
        if (report.Run.Errors.Count > 0)
        {
            suites.Add(RenderSuite("run", [.. report.Run.Errors.Select(RenderRunError)]));
        }

        var total = new SuiteCounts();
        foreach (var suite in suites)
        {
            AddCounts(total, suite.Counts);
        }

        List<string> lines =
        [
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            $"<testsuites {Attributes([("name", "e2e"), .. CountAttributes(total)])}>",
        ];
        foreach (var suite in suites)
        {
            lines.AddRange(suite.Lines.Select(line => "  " + line));
        }

        lines.Add("</testsuites>");
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Code units XML 1.0 forbids beyond the control range <see cref="TextRules.SanitizeRecordedText"/> already replaces: the two noncharacters
    /// U+FFFE and U+FFFF, and a surrogate half without its partner. One of them makes a parser reject the whole document.
    /// </summary>
    [GeneratedRegex(
        "[\\uFFFE\\uFFFF]|[\\uD800-\\uDBFF](?![\\uDC00-\\uDFFF])|(?<![\\uD800-\\uDBFF])[\\uDC00-\\uDFFF]",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex XmlForbidden();

    /// <summary>
    /// Escapes text content; every character XML 1.0 forbids becomes U+FFFD. A carriage return is a reference, since a parser reads a raw one
    /// as a newline.
    /// </summary>
    private static string Text(string value) =>
        XmlForbidden()
            .Replace(TextRules.SanitizeRecordedText(value), "�")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("\r", "&#13;", StringComparison.Ordinal);

    /// <summary>Escapes an attribute value; whitespace is entity-encoded so parsers keep it verbatim.</summary>
    private static string Attribute(string value) =>
        Text(value).Replace("\n", "&#10;", StringComparison.Ordinal).Replace("\t", "&#9;", StringComparison.Ordinal);

    /// <summary>JUnit durations are seconds; three decimals keep millisecond precision.</summary>
    private static string Seconds(long ms) => (ms / 1_000d).ToString("F3", CultureInfo.InvariantCulture);

    private static string Attributes(IEnumerable<(string Key, string Value)> pairs) =>
        string.Join(' ', pairs.Select(pair => $"{pair.Key}=\"{Attribute(pair.Value)}\""));

    private static (string, string)[] CountAttributes(SuiteCounts counts) =>
        [
            ("tests", counts.Tests.ToString(CultureInfo.InvariantCulture)),
            ("failures", counts.Failures.ToString(CultureInfo.InvariantCulture)),
            ("errors", counts.Errors.ToString(CultureInfo.InvariantCulture)),
            ("skipped", counts.Skipped.ToString(CultureInfo.InvariantCulture)),
            ("time", Seconds(counts.TimeMs)),
        ];

    private static void AddCase(SuiteCounts counts, RenderedCase rendered)
    {
        counts.Tests++;
        counts.TimeMs += rendered.DurationMs;
        switch (rendered.Outcome)
        {
            case CaseOutcome.Failure:
                counts.Failures++;
                break;
            case CaseOutcome.Error:
                counts.Errors++;
                break;
            case CaseOutcome.Skipped:
                counts.Skipped++;
                break;
        }
    }

    private static void AddCounts(SuiteCounts total, SuiteCounts part)
    {
        total.Tests += part.Tests;
        total.Failures += part.Failures;
        total.Errors += part.Errors;
        total.Skipped += part.Skipped;
        total.TimeMs += part.TimeMs;
    }

    /// <summary><c>&lt;failure&gt;</c> for a product verdict, <c>&lt;error&gt;</c> for anything else that prevented one.</summary>
    private static CaseOutcome VerdictElement(ReportError? error) =>
        error is null || error.Category == ErrorCategory.Test ? CaseOutcome.Failure : CaseOutcome.Error;

    private static string ElementName(CaseOutcome outcome) => outcome == CaseOutcome.Failure ? "failure" : "error";

    /// <summary><c>CODE (phase): message</c>.</summary>
    private static string ErrorBody(ReportError error) =>
        $"{error.Code}{(error.Phase is { } phase ? $" ({PhaseText(phase)})" : "")}: {error.Message}";

    /// <summary>Why a result was skipped: its reason, after the test filter's name when one left it out.</summary>
    private static string SkippedMessage(ReportResult result)
    {
        var reason = result.Skip?.Reason ?? "skipped";
        return result.Skip?.Filter is { } filter ? $"left out by filter {filter}: {reason}" : reason;
    }

    /// <summary>
    /// <c>failed as expected: &lt;reason&gt;</c> and the covered error, for a passing case whose <c>test.fail</c> mark covered its failure.
    /// </summary>
    private static string? ExpectedNote(ReportResult result)
    {
        var mark = result.Attempts.LastOrDefault()?.ExpectedFailure;
        return mark?.Error is { } error ? $"failed as expected: {mark.Reason}\n{error.Code}: {error.Message}" : null;
    }

    /// <summary>The <c>&lt;system-out&gt;</c> element holding <paramref name="lines"/>, or nothing when there are none.</summary>
    private static List<string> SystemOut(IReadOnlyList<string> lines) =>
        lines.Count == 0 ? [] : [$"  <system-out>{Text(string.Join('\n', lines))}</system-out>"];

    /// <summary>The told attempt's failure screenshot as a path below <c>ArtifactsDir</c>, when both are known.</summary>
    private static string? ScreenshotAttachment(ReportAttempt told, JunitReportOptions? options)
    {
        if (options?.ArtifactsDir is not { } artifactsDir)
        {
            return null;
        }

        return FailureScreenshot(told)?.Path is { } path ? PosixJoin(artifactsDir, path) : null;
    }

    /// <summary>Upstream's <c>path.posix.join</c>: joins with <c>/</c> and resolves <c>.</c> and <c>..</c>.</summary>
    private static string PosixJoin(string first, string second)
    {
        List<string> parts = [];
        var rooted = first.StartsWith('/');
        foreach (var part in (first + "/" + second).Split('/'))
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != ".." || !rooted)
            {
                parts.Add(part);
            }
        }

        var joined = string.Join('/', parts);
        return rooted ? "/" + joined
            : joined.Length == 0 ? "."
            : joined;
    }

    private static string[] Optional(string? line) => line is null ? [] : [line];

    private static string[] Attachment(string? path) => path is null ? [] : [$"[[ATTACHMENT|{path}]]"];

    private static RenderedCase RenderResult(ReportResult result, JunitReportOptions? options)
    {
        var final = Outcome.Of(result);

        // The agent joins the name only when it is not `default`, so a suite that never names one keeps the case names its CI history is keyed on.
        var agent = result.Agent == "default" ? "" : $" [{result.Agent}]";
        var open =
            $"<testcase {Attributes([("name", $"{string.Join(" > ", result.TitlePath)}{ReportFormat.RepeatSuffix(result.Repeat)} [{result.TargetId}]{agent}"), ("classname", result.File), ("time", Seconds(final.DurationMs))])}";
        var expected = ExpectedNote(result);
        switch (result.Status)
        {
            case ResultStatus.Passed:
            {
                if (expected is null)
                {
                    return new RenderedCase([$"{open}/>"], CaseOutcome.Passed, final.DurationMs);
                }

                // A test.fail pass failed as expected; its failure screenshot shows that failure.
                var attachment = ScreenshotAttachment(ToldAttempt(result, final), options);
                List<string> lines = [expected, .. Attachment(attachment)];
                return new RenderedCase(
                    [$"{open}>", .. SystemOut(lines), "</testcase>"],
                    CaseOutcome.Passed,
                    final.DurationMs
                );
            }

            case ResultStatus.Flaky:
            {
                var count = final.FailedAttempts;
                var note =
                    $"flaky: {count.ToString(CultureInfo.InvariantCulture)} failed attempt{(count == 1 ? "" : "s")} before passing";
                var attachment = ScreenshotAttachment(ToldAttempt(result, final), options);
                List<string> lines = [note, .. Optional(expected), .. Attachment(attachment)];
                return new RenderedCase(
                    [$"{open}>", .. SystemOut(lines), "</testcase>"],
                    CaseOutcome.Passed,
                    final.DurationMs
                );
            }

            case ResultStatus.Skipped:
            case ResultStatus.Interrupted:
            {
                // JUnit has no interrupted outcome; a test the run stopped reached no verdict, so it reads as skipped with why.
                var message =
                    result.Status == ResultStatus.Skipped
                        ? SkippedMessage(result)
                        : $"interrupted: {final.Final.Error?.Message ?? "the run was stopped"}";
                return new RenderedCase(
                    [$"{open}>", $"  <skipped {Attributes([("message", message)])}/>", "</testcase>"],
                    CaseOutcome.Skipped,
                    final.DurationMs
                );
            }

            default:
            {
                var told = ToldAttempt(result, final);
                var error = told.Error;
                var element = VerdictElement(error);
                var status = KebabName(result.Status);
                var message = error?.Message ?? status;
                var type = error?.Code ?? status;
                var body = error is null ? status : ErrorBody(error);
                var attachment = ScreenshotAttachment(told, options);
                List<string> lines = [.. Attachment(attachment)];
                return new RenderedCase(
                    [
                        $"{open}>",
                        $"  <{ElementName(element)} {Attributes([("message", message), ("type", type)])}>{Text(body)}</{ElementName(element)}>",
                        .. SystemOut(lines),
                        "</testcase>",
                    ],
                    element,
                    final.DurationMs
                );
            }
        }
    }

    /// <summary>
    /// A run-level error never becomes a result, so it gets a case of its own in a <c>run</c> suite: a startup failure such as
    /// <c>APP_UNREACHABLE</c> is then visible in CI without opening the JSON report.
    /// </summary>
    private static RenderedCase RenderRunError(ReportError error)
    {
        var phase = error.Phase is { } value ? $" ({PhaseText(value)})" : "";
        return new RenderedCase(
            [
                $"<testcase {Attributes([("name", $"{error.Code}{phase}"), ("classname", "run"), ("time", "0.000")])}>",
                $"  <error {Attributes([("message", error.Message), ("type", error.Code)])}>{Text($"{KebabName(error.Category)} error {ErrorBody(error)}")}</error>",
                "</testcase>",
            ],
            CaseOutcome.Error,
            0
        );
    }

    private static (List<string> Lines, SuiteCounts Counts) RenderSuite(string name, IReadOnlyList<RenderedCase> cases)
    {
        var counts = new SuiteCounts();
        foreach (var rendered in cases)
        {
            AddCase(counts, rendered);
        }

        List<string> lines = [$"<testsuite {Attributes([("name", name), .. CountAttributes(counts)])}>"];
        foreach (var rendered in cases)
        {
            lines.AddRange(rendered.Lines.Select(line => "  " + line));
        }

        lines.Add("</testsuite>");
        return (lines, counts);
    }
}

/// <summary>
/// The built-in <c>junit</c> reporter (upstream's <c>junit()</c>, <c>report/junit.ts</c>): the report as JUnit XML in <c>junit.xml</c> beside
/// <c>report.json</c>, for CI test summaries. List it in the <c>reporters</c> of <c>e2e.config.json</c>:
/// <c>{ "type": "E2E.Reports.JunitReporter", "options": { "outputFile": "reports/junit.xml" } }</c>. Writes nothing when the report itself was not
/// written. The port's <see cref="Reporter"/> has no <c>reportFiles</c> hook, so the file is written when the run finishes.
/// </summary>
public class JunitReporter : Reporter, IConfigurableReporter
{
    private string? _outputFile;

    /// <summary>A reporter that writes <c>junit.xml</c> beside <c>report.json</c>.</summary>
    public JunitReporter() { }

    /// <summary>A reporter that writes the XML to <paramref name="outputFile"/>, absolute or relative to the project root.</summary>
    public JunitReporter(string? outputFile) => _outputFile = outputFile;

    /// <inheritdoc />
    public override string Name => "junit";

    /// <summary>
    /// Reads the <c>options</c> of the config entry (.NET-only: upstream's <c>junit({ outputFile })</c> is called in code). The only option is
    /// <c>outputFile</c>, a string. An unknown option or one that is not a string is an <see cref="ArgumentException"/> that names the option.
    /// </summary>
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
                option.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(option.Value.GetString())
                    ? option.Value.GetString()
                    : throw new ArgumentException("option \"outputFile\" must be a non-empty string");
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(
        FinishedRun run,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(run.ReportPath))
        {
            return null;
        }

        var path = Path.GetFullPath(
            _outputFile ?? Path.Combine(Path.GetDirectoryName(run.ReportPath) ?? ".", "junit.xml"),
            run.ProjectRoot
        );
        if (string.Equals(path, Path.GetFullPath(run.ReportPath, run.ProjectRoot), StringComparison.Ordinal))
        {
            // Upstream's writeReportFiles skips a file that is the run's own report.
            Console.Error.WriteLine($"e2e: reporter \"junit\" cannot write {path}: it is the run's report");
            return null;
        }

        var xml = JunitReport.RenderJunitReport(
            run.Report,
            new JunitReportOptions { ArtifactsDir = AttachmentBase(run) }
        );
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await AtomicWrite.WriteFileAtomicAsync(path, xml, cancellationToken).ConfigureAwait(false);
        return [new ReporterSummaryRow("JUnit", Paths.DisplayPath(run.ProjectRoot, path))];
    }

    /// <summary>
    /// Where <c>ArtifactsRoot</c> sits relative to the directory GitLab resolves attachment paths against: <c>$CI_PROJECT_DIR</c> when set, else
    /// the project root. Null when the artifacts are outside it, since GitLab cannot serve those (upstream's <c>attachmentBase</c>).
    /// </summary>
    internal static string? AttachmentBase(FinishedRun run)
    {
        var ci = Environment.GetEnvironmentVariable("CI_PROJECT_DIR");
        var baseDir = string.IsNullOrWhiteSpace(ci) ? run.ProjectRoot : ci;
        if (
            string.Equals(
                Paths.RealPathOfExisting(baseDir),
                Paths.RealPathOfExisting(run.ArtifactsRoot),
                StringComparison.Ordinal
            )
        )
        {
            return ".";
        }

        return Paths.RelativeToProjectRoot(baseDir, run.ArtifactsRoot)?.Replace(Path.DirectorySeparatorChar, '/');
    }
}
