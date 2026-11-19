// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.ReportJunit;

// Inside the namespace, so the public records of a reporter win over the internal ones of the same names in E2E.
#pragma warning disable IDE0065 // Inside the namespace on purpose.
using E2E.Reports;
#pragma warning restore IDE0065

/// <summary>
/// The helpers at the top of upstream's <c>tests/unit/report-junit.test.ts</c> (<c>assertWellFormed</c>, <c>render</c>, <c>rootAttributes</c>) and
/// the builders of <c>tests/helpers/report.ts</c> over the public records a reporter receives.
/// </summary>
internal static partial class JunitKit
{
    public static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // XML 1.0 `Char`, over code points: a lone surrogate is a code point of its own and falls outside every range.
    [GeneratedRegex(
        "^(?:[\\u0009\\u000A\\u000D\\u0020-\\uD7FF\\uE000-\\uFFFD]|[\\uD800-\\uDBFF][\\uDC00-\\uDFFF])*\\z"
    )]
    private static partial Regex XmlChars();

    [GeneratedRegex("&(?!(?:amp|lt|gt|quot|apos|#\\d+|#x[0-9a-fA-F]+);)")]
    private static partial Regex EntityOnly();

    [GeneratedRegex("^<([A-Za-z_][\\w.-]*)((?:\\s+[A-Za-z_][\\w.-]*=\"[^\"<]*\")*)\\s*(/?)>\\z")]
    private static partial Regex StartTag();

    [GeneratedRegex("<[^>]*>|[^<]+")]
    private static partial Regex Tokens();

    [GeneratedRegex("<testsuites ([^>]*)>")]
    private static partial Regex Root();

    [GeneratedRegex("(\\w+)=\"([^\"]*)\"")]
    private static partial Regex Attribute();

    /// <summary>
    /// Strict well-formedness check standing in for a parser: every code unit is an XML 1.0 character, tags nest and close by name, attributes
    /// are double-quoted without raw <c>&lt;</c>, and every <c>&amp;</c> is an entity.
    /// </summary>
    public static void AssertWellFormed(string xml)
    {
        if (!XmlChars().IsMatch(xml))
        {
            throw new InvalidOperationException($"XML contains a forbidden character:\n{xml}");
        }

        const string declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
        if (!xml.StartsWith(declaration, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("missing XML declaration");
        }

        var body = xml[declaration.Length..];
        Stack<string> stack = [];
        var roots = 0;
        foreach (var token in Tokens().Matches(body).Select(match => match.Value))
        {
            if (token.StartsWith("</", StringComparison.Ordinal))
            {
                var name = token[2..^1].Trim();
                if (stack.Count == 0 || stack.Pop() != name)
                {
                    throw new InvalidOperationException($"unbalanced close tag {token}");
                }

                continue;
            }

            if (token.StartsWith('<'))
            {
                var match = StartTag().Match(token);
                if (!match.Success)
                {
                    throw new InvalidOperationException($"malformed tag {token}");
                }

                if (EntityOnly().IsMatch(match.Groups[2].Value))
                {
                    throw new InvalidOperationException($"raw ampersand in {token}");
                }

                if (stack.Count == 0)
                {
                    roots++;
                }

                if (match.Groups[3].Value != "/")
                {
                    stack.Push(match.Groups[1].Value);
                }

                continue;
            }

            if (EntityOnly().IsMatch(token))
            {
                throw new InvalidOperationException($"raw ampersand in text \"{token}\"");
            }

            if (stack.Count == 0 && token.Trim().Length != 0)
            {
                throw new InvalidOperationException($"text outside the root: \"{token}\"");
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidOperationException($"unclosed elements: {string.Join(", ", stack)}");
        }

        if (roots != 1)
        {
            throw new InvalidOperationException($"expected one root element, found {roots}");
        }

        // A real parser also rejects what the token checks cannot see, such as a duplicate attribute.
        System.Xml.Linq.XDocument.Parse(xml);
    }

    /// <summary>Renders and asserts well-formedness, so every case also parses.</summary>
    public static string Render(Report document, JunitReportOptions? options = null)
    {
        var xml = JunitReport.RenderJunitReport(document, options);
        AssertWellFormed(xml);
        return xml;
    }

    public static Dictionary<string, string> RootAttributes(string xml)
    {
        var match = Root().Match(xml);
        if (!match.Success)
        {
            throw new InvalidOperationException($"no <testsuites> root in:\n{xml}");
        }

        return Attribute().Matches(match.Groups[1].Value).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    public static ReportError Error(
        string code = "ASSERTION_FAILED",
        string message = "expected \"Sign in\" to be visible",
        ErrorCategory category = ErrorCategory.Test,
        ErrorPhase? phase = null
    ) =>
        new()
        {
            Category = category,
            Code = code,
            Message = message,
            Phase = phase,
        };

    public static ReportAttempt Attempt(
        string id = "attempt-1",
        int index = 0,
        AttemptStatus status = AttemptStatus.Passed,
        long durationMs = 1234,
        ReportError? error = null,
        ExpectedFailureRecord? expectedFailure = null,
        FailureEvidence? failure = null,
        IReadOnlyList<ArtifactRecord>? artifacts = null
    ) =>
        new()
        {
            Id = id,
            Index = index,
            Status = status,
            StartedAt = At,
            DurationMs = durationMs,
            Error = error,
            ExpectedFailure = expectedFailure,
            Failure = failure,
            Artifacts = artifacts ?? [],
        };

    public static ReportResult Result(
        string id = "result-1",
        string testId = "test-1",
        int declarationIndex = 0,
        IReadOnlyList<string>? titlePath = null,
        string file = "tests/auth.e2e.ts",
        string targetId = "web",
        string agent = "default",
        bool selected = true,
        ResultStatus status = ResultStatus.Passed,
        SkipInfo? skip = null,
        IReadOnlyList<ReportAttempt>? attempts = null
    ) =>
        new()
        {
            Id = id,
            TestId = testId,
            DeclarationIndex = declarationIndex,
            TitlePath = titlePath ?? ["auth", "signs in"],
            File = file,
            Source = new ReportSource
            {
                File = "tests/auth.e2e.ts",
                Line = 3,
                Column = 1,
            },
            TargetId = targetId,
            Platform = "web",
            Agent = agent,
            Selected = selected,
            Status = status,
            Skip = skip,
            Attempts = attempts ?? [Attempt()],
        };

    /// <summary>A whole document; the arguments are what a test is about.</summary>
    public static Report Document(
        IReadOnlyList<ReportResult>? results = null,
        IReadOnlyList<ReportError>? errors = null
    ) =>
        new()
        {
            Run = new ReportRun
            {
                Id = "run-1",
                StartedAt = At,
                FinishedAt = At,
                Results = results ?? [],
                Errors = errors ?? [],
            },
        };

    /// <summary>A screenshot artifact; <paramref name="path"/> and <paramref name="url"/> are set only when given.</summary>
    public static ArtifactRecord ShotArtifact(string id, string? path = null, string? url = null) =>
        new()
        {
            Id = id,
            Kind = ArtifactKind.Screenshot,
            MediaType = "image/png",
            Redaction = ArtifactRedaction.Complete,
            Producer = new ArtifactProducer { Kind = ArtifactProducerKind.Step, StepId = "step-0" },
            Path = path,
            Url = url,
        };

    /// <summary>
    /// A report with one failed test whose attempt recorded <c>checkout/001-failure.png</c> as its failure screenshot. The arguments override that
    /// attempt; <paramref name="extraAttempts"/> follow it, and <paramref name="status"/> sets the result's.
    /// </summary>
    public static Report FailedWithShot(
        ReportError? error = null,
        AttemptStatus attemptStatus = AttemptStatus.Failed,
        FailureEvidence? failure = null,
        IReadOnlyList<ArtifactRecord>? artifacts = null,
        ResultStatus status = ResultStatus.Failed,
        IReadOnlyList<ReportAttempt>? extraAttempts = null
    ) =>
        Document([
            Result(
                titlePath: ["checkout"],
                status: status,
                attempts:
                [
                    Attempt(
                        index: 0,
                        status: attemptStatus,
                        error: error ?? Error(message: "heading not visible"),
                        failure: failure ?? new FailureEvidence { Screenshot = "shot-1" },
                        artifacts: artifacts ?? [ShotArtifact("shot-1", "checkout/001-failure.png")]
                    ),
                    .. extraAttempts ?? [],
                ]
            ),
        ]);
}
