// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Internal;
using static E2E.Reports.FailureText;

namespace E2E.Reports;

/// <summary>Options for rendering JUnit reports (upstream's <c>JunitReportOptions</c>).</summary>
public sealed record JunitReportOptions
{
    /// <summary>
    /// Where artifact files are located relative to the repository root or CI working directory (such as GitLab's <c>$CI_PROJECT_DIR</c>). When provided,
    /// a test with a failure screenshot gains <c>&lt;system-out&gt;[[ATTACHMENT|&lt;path&gt;]]&lt;/system-out&gt;</c>.
    /// </summary>
    public string? ArtifactsDir { get; init; }
}

/// <summary>
/// JUnit XML: the report-1 document as <c>junit.xml</c> (upstream's <c>report/junit.ts</c>). <see cref="RenderJunitReport"/> renders one
/// <c>&lt;testsuite&gt;</c> per test file (in report order, so the output is completion-time independent), one <c>&lt;testcase&gt;</c> per
/// test-target pair, plus a <c>run</c> suite for run-level errors. A pair a test filter left out is a skipped case naming the filter, so a run
/// the filters left empty still lists its suite. Like every reporter it reads the document, never runner state, and it can never change run status.
/// The port has no serial groups, so a result always answers from its own attempts.
/// </summary>
public static partial class JunitReport
{
    private sealed class SuiteCounts
    {
        public int Tests { get; set; }

        public int Failures { get; set; }

        public int Errors { get; set; }

        public int Skipped { get; set; }

        public double TimeMs { get; set; }
    }

    private enum CaseOutcome
    {
        Passed,
        Failure,
        Error,
        Skipped,
    }

    private sealed record RenderedCase(IReadOnlyList<string> Lines, CaseOutcome Outcome, double DurationMs);

    /// <summary>
    /// Code units XML 1.0 forbids beyond the control range <see cref="TextRules.SanitizeRecordedText"/> already replaces: the two noncharacters
    /// U+FFFE and U+FFFF, and a surrogate half without its partner. One of them makes a parser reject the whole document.
    /// </summary>
    [GeneratedRegex(
        @"[￾￿]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex XmlForbidden();

    /// <summary>
    /// Renders the report-1 document as JUnit XML: one <c>&lt;testsuite&gt;</c> per test file (in report order), one <c>&lt;testcase&gt;</c> per test-target
    /// pair, plus a <c>run</c> suite for run-level errors. Upstream's <c>renderJunitReport</c>.
    /// </summary>
    public static string RenderJunitReport(Report report, JunitReportOptions? options = null)
    {
        List<string> fileOrder = [];
        Dictionary<string, List<RenderedCase>> byFile = new(StringComparer.Ordinal);
        foreach (var result in report.Run.Results)
        {
            // Other unselected results are report-only; a CI viewer counts what the run selected.
            if (!result.Selected && result.Skip?.Filter is null)
            {
                continue;
            }

            if (!byFile.TryGetValue(result.File, out var cases))
            {
                fileOrder.Add(result.File);
                byFile[result.File] = cases = [];
            }

            cases.Add(RenderResult(result, options));
        }

        var suites = fileOrder.Select(file => RenderSuite(file, byFile[file])).ToList();
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

    /// <summary>Escapes text content; every character XML 1.0 forbids becomes U+FFFD. A carriage return is a reference, since a parser reads a raw one as a newline.</summary>
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
    private static string Seconds(double ms) => (ms / 1_000).ToString("F3", CultureInfo.InvariantCulture);

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

    /// <summary>
    /// <c>CODE (phase): message</c>; a failure on a step of a session beside the test's own names it, <c>CODE (phase) on launch-1: message</c>.
    /// </summary>
    private static string ErrorBody(ReportError error, string? actor = null)
    {
        var phase = error.Phase is null ? "" : $" ({PhaseText(error.Phase.Value)})";
        return $"{error.Code}{phase}{(actor is null ? "" : $" on {actor}")}: {error.Message}";
    }

    /// <summary>Why a result was skipped: its reason, after the test filter's name when one left it out.</summary>
    private static string SkippedMessage(ReportResult result)
    {
        var reason = result.Skip?.Reason ?? "skipped";
        return result.Skip?.Filter is null ? reason : $"left out by filter {result.Skip.Filter}: {reason}";
    }

    /// <summary><c>failed as expected: &lt;reason&gt;</c> and the covered error, for a passing case whose <c>test.fail</c> mark covered its failure.</summary>
    private static string? ExpectedNote(ReportResult result)
    {
        var mark = result.Attempts.LastOrDefault()?.ExpectedFailure;
        return mark?.Error is null
            ? null
            : $"failed as expected: {mark.Reason}\n{mark.Error.Code}: {mark.Error.Message}";
    }

    /// <summary>The <c>&lt;system-out&gt;</c> element holding <paramref name="lines"/>, or nothing when there are none.</summary>
    private static List<string> SystemOut(IReadOnlyList<string> lines) =>
        lines.Count == 0 ? [] : [$"  <system-out>{Text(string.Join('\n', lines))}</system-out>"];

    /// <summary>The told attempt's failure screenshot as a path below <c>ArtifactsDir</c>, when both are known.</summary>
    private static string? ScreenshotAttachment(ReportAttempt told, JunitReportOptions? options)
    {
        if (options?.ArtifactsDir is not { } dir)
        {
            return null;
        }

        return FailureScreenshot(told)?.Path is { } path ? PosixJoin(dir, path) : null;
    }

    /// <summary>Upstream's <c>path.posix.join</c>, over the two parts: normalized, with <c>.</c> and empty segments dropped.</summary>
    private static string PosixJoin(string left, string right)
    {
        List<string> parts = [];
        foreach (var segment in (left + "/" + right).Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (segment != ".." || !left.StartsWith('/'))
            {
                parts.Add(segment);
            }
        }

        var joined = string.Join('/', parts);
        return (left.StartsWith('/') ? "/" : "") + joined;
    }

    private static RenderedCase RenderResult(ReportResult result, JunitReportOptions? options)
    {
        var final = Outcome.Of(result);
        // The agent joins the name only when it is not `default`, so a suite that never names one keeps the case names its CI history is keyed on.
        var agent = result.Agent == "default" ? "" : $" [{result.Agent}]";
        var open =
            "<testcase "
            + Attributes([
                (
                    "name",
                    $"{string.Join(" > ", result.TitlePath)}{ReportFormat.RepeatSuffix(result.Repeat)} [{result.TargetId}]{agent}"
                ),
                ("classname", result.File),
                ("time", Seconds(final.DurationMs)),
            ]);
        var expected = ExpectedNote(result);
        switch (result.Status)
        {
            case ResultStatus.Passed:
            {
                if (expected is null)
                {
                    return new RenderedCase([open + "/>"], CaseOutcome.Passed, final.DurationMs);
                }

                // A test.fail pass failed as expected; its failure screenshot shows that failure.
                var attachment = ScreenshotAttachment(ToldAttempt(result, final), options);
                List<string> lines = [expected, .. attachment is null ? [] : new[] { $"[[ATTACHMENT|{attachment}]]" }];
                return new RenderedCase(
                    [open + ">", .. SystemOut(lines), "</testcase>"],
                    CaseOutcome.Passed,
                    final.DurationMs
                );
            }

            case ResultStatus.Flaky:
            {
                var count = final.FailedAttempts;
                var note = $"flaky: {count} failed attempt{(count == 1 ? "" : "s")} before passing";
                var attachment = ScreenshotAttachment(ToldAttempt(result, final), options);
                List<string> lines =
                [
                    note,
                    .. expected is null ? [] : new[] { expected },
                    .. attachment is null ? [] : new[] { $"[[ATTACHMENT|{attachment}]]" },
                ];
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
                        : $"interrupted: {final.Final.Error?.Message ?? "the run was stopped"}";
                return new RenderedCase(
                    [open + ">", $"  <skipped {Attributes([("message", message)])}/>", "</testcase>"],
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
                var body = error is null ? status : ErrorBody(error, FailedStepOf(told)?.Step.Actor);
                var attachment = ScreenshotAttachment(told, options);
                List<string> lines = attachment is null ? [] : [$"[[ATTACHMENT|{attachment}]]"];
                return new RenderedCase(
                    [
                        open + ">",
                        $"  <{name} {Attributes([("message", message), ("type", type)])}>{Text(body)}</{name}>",
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
        var phase = error.Phase is null ? "" : $" ({PhaseText(error.Phase.Value)})";
        var category = error.Category.ToString().ToLowerInvariant();
        return new RenderedCase(
            [
                $"<testcase {Attributes([("name", $"{error.Code}{phase}"), ("classname", "run"), ("time", "0.000")])}>",
                $"  <error {Attributes([("message", error.Message), ("type", error.Code)])}>{Text($"{category} error {ErrorBody(error)}")}</error>",
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

/// <summary>Options of the built-in <see cref="JunitReporter"/> (upstream's <c>JunitReporterOptions</c>).</summary>
public sealed record JunitReporterOptions
{
    /// <summary>Where the XML goes, from the project root; <c>junit.xml</c> in the output directory by default.</summary>
    public string? OutputFile { get; init; }
}

/// <summary>
/// The built-in <c>junit</c> reporter (upstream's <c>junit()</c>, <c>report/junit.ts</c>): the report as JUnit XML in <c>junit.xml</c> beside
/// <c>report.json</c>, for CI test summaries. List it in the <c>reporters</c> of <c>e2e.config.json</c>:
/// <c>{ "type": "E2E.Reports.JunitReporter", "options": { "outputFile": "reports/junit.xml" } }</c>. The port has no <c>reportFiles</c> hook, so it
/// writes the file itself when the run finishes (.NET-only). It writes nothing when the report itself was not written.
/// </summary>
public class JunitReporter : Reporter, IConfigurableReporter
{
    private JunitReporterOptions _options = new();

    /// <summary>A reporter that writes <c>junit.xml</c> in the output directory. The public constructor the config builds it with.</summary>
    public JunitReporter() { }

    /// <summary>A reporter with these options. For a class that derives from this one and is listed in the config.</summary>
    protected JunitReporter(JunitReporterOptions options) => _options = options;

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

        string? outputFile = null;
        foreach (var option in options.EnumerateObject())
        {
            if (option.Name != "outputFile")
            {
                throw new ArgumentException(
                    $"option \"{option.Name}\" is not supported; the only option is \"outputFile\""
                );
            }

            outputFile =
                option.Value.ValueKind == JsonValueKind.String
                    ? option.Value.GetString()
                    : throw new ArgumentException("option \"outputFile\" must be a string");
        }

        _options = new JunitReporterOptions { OutputFile = outputFile };
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

        var outputDir = Path.GetDirectoryName(run.ReportPath) ?? run.ProjectRoot;
        var file = Path.GetFullPath(_options.OutputFile ?? Path.Combine(outputDir, "junit.xml"), run.ProjectRoot);
        var xml = JunitReport.RenderJunitReport(
            run.Report,
            new JunitReportOptions { ArtifactsDir = AttachmentBase(run) }
        );
        await WriteReport.WriteTextReportAsync(file, xml, cancellationToken).ConfigureAwait(false);
        return [new ReporterSummaryRow("JUnit", Paths.DisplayPath(run.ProjectRoot, file))];
    }

    /// <summary>
    /// Where <c>ArtifactsRoot</c> sits relative to the directory GitLab resolves attachment paths against: <c>$CI_PROJECT_DIR</c> when set, else
    /// the project root. Null when the artifacts are outside it, since GitLab cannot serve those.
    /// </summary>
    internal static string? AttachmentBase(FinishedRun run)
    {
        var raw = Environment.GetEnvironmentVariable("CI_PROJECT_DIR");
        var baseDir = string.IsNullOrWhiteSpace(raw) ? run.ProjectRoot : raw.Trim();
        if (Paths.RealPathOfExisting(baseDir) == Paths.RealPathOfExisting(run.ArtifactsRoot))
        {
            return ".";
        }

        return Paths.RelativeToProjectRoot(baseDir, run.ArtifactsRoot) is { } relative
            ? WriteReport.ToPosixPath(relative)
            : null;
    }
}
