// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Internal;
using static E2E.Reports.FailureText;

namespace E2E.Reports;

/// <summary>Options for rendering JUnit reports (upstream's <c>JunitReportOptions</c>).</summary>
public sealed record JunitReportOptions
{
    /// <summary>
    /// Where artifact files are located relative to the repository root or CI working directory (such as GitLab's <c>$CI_PROJECT_DIR</c>).
    /// When provided, a test with a failure screenshot gains <c>&lt;system-out&gt;[[ATTACHMENT|&lt;path&gt;]]&lt;/system-out&gt;</c>.
    /// </summary>
    public string? ArtifactsDir { get; init; }
}

/// <summary>
/// JUnit XML reporter: the report-1 document as <c>junit.xml</c> (upstream's <c>report/junit.ts</c>). One <c>&lt;testsuite&gt;</c> per test file (in report
/// order, so the output is completion-time independent), one <c>&lt;testcase&gt;</c> per test-target pair, plus a <c>run</c> suite for run-level errors.
/// A pair a test filter left out is a skipped case naming the filter. Like every reporter it reads the document, never runner state, and it can never
/// change run status. Not ported, since the report has no such records: serial groups and the <c>test.fail</c> expected-failure note.
/// .NET-only: <c>CaseOutcome</c> (an enum where upstream uses a string union in <c>RenderedCase.outcome</c>) and <c>PosixJoin</c> (upstream calls Node's <c>path.posix.join</c>, which .NET has no equivalent of).
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
    /// Renders the report-1 document as JUnit XML (upstream's <c>renderJunitReport</c>). A test the selection did not choose is left out, unless a test
    /// filter did: that one is a skipped case.
    /// </summary>
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

            var entry = byFile.FirstOrDefault(item => item.File == result.File);
            if (entry.Cases is null)
            {
                entry = (result.File, []);
                byFile.Add(entry);
            }

            entry.Cases.Add(RenderResult(result, options));
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
            "<testsuites " + Attributes([("name", "e2e"), .. CountAttributes(total)]) + ">",
        ];
        foreach (var suite in suites)
        {
            lines.AddRange(suite.Lines.Select(line => "  " + line));
        }

        lines.Add("</testsuites>");
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Code units XML 1.0 forbids beyond the control range <see cref="TextRules.SanitizeRecordedText"/> already replaces: the two noncharacters U+FFFE
    /// and U+FFFF, and a surrogate half without its partner. One of them makes a parser reject the whole document.
    /// </summary>
    [GeneratedRegex("[￾￿]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]")]
    private static partial Regex XmlForbidden();

    /// <summary>
    /// Escapes text content; every character XML 1.0 forbids becomes U+FFFD. A carriage return is a reference, since a parser reads a raw one as a
    /// newline.
    /// </summary>
    private static string Text(string value) =>
        XmlForbidden()
            .Replace(TextRules.SanitizeRecordedText(value), "�")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("\r", "&#13;");

    /// <summary>Escapes an attribute value; whitespace is entity-encoded so parsers keep it verbatim.</summary>
    private static string Attribute(string value) => Text(value).Replace("\n", "&#10;").Replace("\t", "&#9;");

    /// <summary>JUnit durations are seconds; three decimals keep millisecond precision.</summary>
    private static string Seconds(long ms) => (ms / 1_000m).ToString("F3", CultureInfo.InvariantCulture);

    private static string Attributes(IEnumerable<(string Key, string Value)> pairs) =>
        string.Join(' ', pairs.Select(pair => pair.Key + "=\"" + Attribute(pair.Value) + "\""));

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

    private static string ErrorBody(ReportError error) =>
        error.Code + (error.Phase is { } phase ? " (" + PhaseText(phase) + ")" : "") + ": " + error.Message;

    /// <summary>Why a result was skipped: its reason, after the test filter's name when one left it out.</summary>
    private static string SkippedMessage(ReportResult result)
    {
        var reason = result.Skip?.Reason ?? "skipped";
        return result.Skip?.Filter is { } filter ? "left out by filter " + filter + ": " + reason : reason;
    }

    /// <summary>The <c>&lt;system-out&gt;</c> element holding <paramref name="lines"/>, or nothing when there are none.</summary>
    private static List<string> SystemOut(IReadOnlyList<string> lines) =>
        lines.Count == 0 ? [] : ["  <system-out>" + Text(string.Join('\n', lines)) + "</system-out>"];

    /// <summary>The told attempt's failure screenshot as a path below <c>ArtifactsDir</c>, when both are known.</summary>
    private static string? ScreenshotAttachment(ReportAttempt told, JunitReportOptions? options) =>
        options?.ArtifactsDir is { } dir && FailureScreenshot(told)?.Path is { } path ? PosixJoin(dir, path) : null;

    /// <summary>Upstream's <c>path.posix.join</c>: the parts joined with <c>/</c> and normalized.</summary>
    private static string PosixJoin(string left, string right)
    {
        List<string> parts = [];
        foreach (var part in (left + "/" + right).Split('/'))
        {
            if (part is "" or ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }

        return parts.Count == 0 ? "." : string.Join('/', parts);
    }

    private static RenderedCase RenderResult(ReportResult result, JunitReportOptions? options)
    {
        var final = Outcome.Of(result);
        // The agent joins the name only when it is not `default`, so a suite that never names one keeps the case names its CI history is keyed on.
        var agent = result.Agent == "default" ? "" : " [" + result.Agent + "]";
        var open =
            "<testcase "
            + Attributes([
                (
                    "name",
                    string.Join(" > ", result.TitlePath)
                        + ReportFormat.RepeatSuffix(result.Repeat)
                        + " ["
                        + result.TargetId
                        + "]"
                        + agent
                ),
                ("classname", result.File),
                ("time", Seconds(final.DurationMs)),
            ]);
        switch (result.Status)
        {
            case ResultStatus.Passed:
                return new RenderedCase([open + "/>"], CaseOutcome.Passed, final.DurationMs);
            case ResultStatus.Flaky:
            {
                var count = final.FailedAttempts;
                var note = "flaky: " + count + " failed attempt" + (count == 1 ? "" : "s") + " before passing";
                var attachment = ScreenshotAttachment(ToldAttempt(result, final), options);
                List<string> lines = [note, .. attachment is null ? [] : new[] { "[[ATTACHMENT|" + attachment + "]]" }];
                return new RenderedCase(
                    [open + ">", .. SystemOut(lines), "</testcase>"],
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
                        : "interrupted: " + (final.Final.Error?.Message ?? "the run was stopped");
                return new RenderedCase(
                    [open + ">", "  <skipped " + Attributes([("message", message)]) + "/>", "</testcase>"],
                    CaseOutcome.Skipped,
                    final.DurationMs
                );
            }

            default:
            {
                var told = ToldAttempt(result, final);
                var error = told.Error;
                var element = VerdictElement(error);
                var name = element == CaseOutcome.Failure ? "failure" : "error";
                var status = KebabName(result.Status);
                var message = error?.Message ?? status;
                var type = error?.Code ?? status;
                var body = error is null ? status : ErrorBody(error);
                var attachment = ScreenshotAttachment(told, options);
                IReadOnlyList<string> lines = attachment is null ? [] : ["[[ATTACHMENT|" + attachment + "]]"];
                return new RenderedCase(
                    [
                        open + ">",
                        "  <"
                            + name
                            + " "
                            + Attributes([("message", message), ("type", type)])
                            + ">"
                            + Text(body)
                            + "</"
                            + name
                            + ">",
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
        var phase = error.Phase is { } value ? " (" + PhaseText(value) + ")" : "";
        return new RenderedCase(
            [
                "<testcase "
                    + Attributes([("name", error.Code + phase), ("classname", "run"), ("time", "0.000")])
                    + ">",
                "  <error "
                    + Attributes([("message", error.Message), ("type", error.Code)])
                    + ">"
                    + Text(KebabName(error.Category) + " error " + ErrorBody(error))
                    + "</error>",
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

        List<string> lines = ["<testsuite " + Attributes([("name", name), .. CountAttributes(counts)]) + ">"];
        foreach (var rendered in cases)
        {
            lines.AddRange(rendered.Lines.Select(line => "  " + line));
        }

        lines.Add("</testsuite>");
        return (lines, counts);
    }
}

/// <summary>
/// The built-in <c>junit</c> reporter (upstream's <c>junitReporter</c>): the report as JUnit XML in <c>junit.xml</c> beside <c>report.json</c>, for CI
/// test summaries. List it in <c>reporters</c> of <c>e2e.config.json</c> by its type name.
/// </summary>
public sealed class JunitReporter : Reporter
{
    /// <inheritdoc />
    public override string Name => "junit";

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(
        FinishedRun run,
        CancellationToken cancellationToken
    )
    {
        // Upstream's `fileReporter`: the file goes beside report.json.
        var file = Path.Combine(Path.GetDirectoryName(run.ReportPath) ?? "", "junit.xml");
        var xml = JunitReport.RenderJunitReport(
            run.Report,
            new JunitReportOptions { ArtifactsDir = AttachmentBase(run) }
        );
        await WriteReport.WriteTextReportAsync(file, xml, cancellationToken).ConfigureAwait(false);
        var shown = Path.GetRelativePath(run.ProjectRoot, file);
        return [new ReporterSummaryRow("JUnit", shown.Length == 0 ? file : shown)];
    }

    /// <summary>
    /// Where <c>ArtifactsRoot</c> sits relative to the directory GitLab resolves attachment paths against: <c>$CI_PROJECT_DIR</c> when set, else the
    /// project root. Null when the artifacts are outside it, since GitLab cannot serve those.
    /// </summary>
    internal static string? AttachmentBase(FinishedRun run)
    {
        var ci = Environment.GetEnvironmentVariable("CI_PROJECT_DIR");
        var baseDir = string.IsNullOrWhiteSpace(ci) ? run.ProjectRoot : ci;
        if (Paths.RealPathOfExisting(baseDir) == Paths.RealPathOfExisting(run.ArtifactsRoot))
        {
            return ".";
        }

        var relative = Paths.RelativeToProjectRoot(baseDir, run.ArtifactsRoot);
        return relative is null ? null : WriteReport.ToPosixPath(relative);
    }
}
