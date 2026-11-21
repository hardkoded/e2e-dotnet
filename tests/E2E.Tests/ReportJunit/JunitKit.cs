// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using System.Xml;
using E2E.Internal;
using E2E.Tests.Traces;
using ArtifactProducer = E2E.Reports.ArtifactProducer;
using ArtifactProducerKind = E2E.Reports.ArtifactProducerKind;
using JunitReport = E2E.Reports.JunitReport;
using JunitReportOptions = E2E.Reports.JunitReportOptions;

namespace E2E.Tests.ReportJunit;

/// <summary>The helpers upstream's <c>unit/report-junit.test.ts</c> declares at the top of the file, over the port's records.</summary>
internal static class JunitKit
{
    /// <summary>
    /// Upstream's <c>assertWellFormed</c>, which stands in for a parser because none is a dependency there. .NET has one: <see cref="XmlReader"/>
    /// rejects a forbidden character, an unbalanced tag, and a raw ampersand, and requires the XML declaration and a single root.
    /// </summary>
    public static void AssertWellFormed(string xml)
    {
        if (!xml.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("missing XML declaration");
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, CheckCharacters = true };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var roots = 0;
        var depth = 0;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (depth == 0)
                {
                    roots++;
                }

                if (!reader.IsEmptyElement)
                {
                    depth++;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                depth--;
            }
        }

        if (roots != 1)
        {
            throw new InvalidOperationException($"expected one root element, found {roots}");
        }
    }

    /// <summary>Renders and asserts well-formedness, so every case also parses.</summary>
    public static string Render(Report1Document document, JunitReportOptions? options = null)
    {
        var xml = JunitReport.RenderJunitReport(ReportBuilders.Public(document), options);
        AssertWellFormed(xml);
        return xml;
    }

    public static Dictionary<string, string> RootAttributes(string xml)
    {
        var match = Regex.Match(xml, "<testsuites ([^>]*)>");
        if (!match.Success)
        {
            throw new InvalidOperationException($"no <testsuites> root in:\n{xml}");
        }

        return Regex
            .Matches(match.Groups[1].Value, "(\\w+)=\"([^\"]*)\"")
            .ToDictionary(attribute => attribute.Groups[1].Value, attribute => attribute.Groups[2].Value);
    }

    /// <summary>Upstream's <c>reportDocument</c>: a run holding <paramref name="results"/> and the run-level <paramref name="errors"/>.</summary>
    public static Report1Document Document(
        IReadOnlyList<ReportResult>? results = null,
        IReadOnlyList<ReportError>? errors = null
    ) => new(new ReportRun("run-1", "0.0.0", [], results ?? []) { Errors = errors ?? [] });

    /// <summary>Upstream's <c>reportError</c>; <paramref name="category"/> is the wire word.</summary>
    public static ReportError Error(
        string code = "ASSERTION_FAILED",
        string message = "expected \"Sign in\" to be visible",
        string? phase = null,
        string category = "test"
    ) => new(code, message, phase) { Category = category };

    /// <summary>Upstream's <c>reportAttempt</c>.</summary>
    public static ReportAttempt Attempt(
        AttemptStatus status = AttemptStatus.Passed,
        long durationMs = 1234,
        ReportError? error = null,
        IReadOnlyList<StepRecord>? steps = null,
        ExpectedFailureInfo? expectedFailure = null,
        string? failureScreenshot = null,
        IReadOnlyList<ReportArtifact>? artifacts = null
    ) =>
        ReportBuilders.Attempt(status, error, steps: steps) with
        {
            DurationMs = durationMs,
            ExpectedFailure = expectedFailure,
            Failure = failureScreenshot is null
                ? null
                : new global::E2E.FailureEvidence { Screenshot = failureScreenshot },
            Artifacts = artifacts ?? [],
        };

    /// <summary>Upstream's <c>reportResult</c>.</summary>
    public static ReportResult Result(
        string id = "result-1",
        string file = "tests/auth.e2e.ts",
        IReadOnlyList<string>? titlePath = null,
        ResultStatus status = ResultStatus.Passed,
        IReadOnlyList<ReportAttempt>? attempts = null,
        string target = "web",
        string agent = "default",
        bool selected = true,
        ReportSkip? skip = null
    ) =>
        ReportBuilders.Result(id, file, titlePath, status, attempts ?? [Attempt()]) with
        {
            TargetId = target,
            Agent = agent,
            Selected = selected,
            Skip = skip,
            TestId = id.Replace("result", "test", StringComparison.Ordinal),
        };

    /// <summary>A step of the failed test's own, or of the session <paramref name="actor"/>, that failed with <paramref name="error"/>.</summary>
    public static StepRecord FailedStep(ReportError error, string? actor = null) =>
        new()
        {
            Id = "step-0",
            Index = 0,
            Kind = StepKind.Locator,
            Api = "locator.tap",
            Label = "tap Sign in",
            Actor = actor,
            Source = new SourceLocation("tests/auth.e2e.ts", 3, 1),
            Status = StepStatus.Failed,
            StartedAt = ReportBuilders.ReportAt,
            DurationMs = 100,
            Error = new SerializedError(error.Category, error.Code, error.Message, false, null),
        };

    /// <summary>A screenshot artifact; <paramref name="path"/> and <paramref name="url"/> are set only when given.</summary>
    public static ReportArtifact ShotArtifact(string id, string? path = null, string? url = null) =>
        new(id, "screenshot", path, url)
        {
            Producer = new ArtifactProducer { Kind = ArtifactProducerKind.Step, StepId = "step-0" },
        };

    /// <summary>
    /// A report with one failed test whose attempt recorded <c>checkout/001-failure.png</c> as its failure screenshot. <paramref name="attempt"/>
    /// replaces that attempt; <paramref name="extraAttempts"/> follow it, and <paramref name="status"/> is the result's.
    /// </summary>
    public static Report1Document FailedWithShot(
        ReportAttempt? attempt = null,
        ResultStatus status = ResultStatus.Failed,
        IReadOnlyList<ReportAttempt>? extraAttempts = null
    ) =>
        Document([
            Result(
                titlePath: ["checkout"],
                status: status,
                attempts:
                [
                    attempt
                        ?? Attempt(
                            AttemptStatus.Failed,
                            error: Error(message: "heading not visible"),
                            failureScreenshot: "shot-1",
                            artifacts: [ShotArtifact("shot-1", "checkout/001-failure.png")]
                        ),
                    .. extraAttempts ?? [],
                ]
            ),
        ]);
}
