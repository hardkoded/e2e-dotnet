// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;
using R = E2E.Reports;

namespace E2E.Tests.ReportJunit;

/// <summary>The helpers upstream's <c>unit/report-junit.test.ts</c> declares at the top of the file, and <c>tests/helpers/report.ts</c>' builders, over the public report.</summary>
internal static partial class JunitKit
{
    public static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [GeneratedRegex(@"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)")]
    private static partial Regex EntityOnly();

    [GeneratedRegex(@"^<([A-Za-z_][\w.-]*)((?:\s+[A-Za-z_][\w.-]*=""[^""<]*"")*)\s*(/?)>$")]
    private static partial Regex StartTag();

    [GeneratedRegex(@"<[^>]*>|[^<]+")]
    private static partial Regex Tokens();

    [GeneratedRegex(@"<testsuites ([^>]*)>")]
    private static partial Regex Root();

    [GeneratedRegex(@"(\w+)=""([^""]*)""")]
    private static partial Regex Attribute();

    /// <summary>XML 1.0 <c>Char</c>, over code points: a lone surrogate is a code point of its own and falls outside every range.</summary>
    private static bool IsXmlChars(string xml)
    {
        for (var i = 0; i < xml.Length; i++)
        {
            var c = xml[i];
            if (char.IsHighSurrogate(c) && i + 1 < xml.Length && char.IsLowSurrogate(xml[i + 1]))
            {
                i++;
                continue;
            }

            var ok = c is '\t' or '\n' or '\r' || (c >= ' ' && c <= '퟿') || (c >= '' && c <= '�');
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Strict well-formedness check standing in for a parser: every code unit is an XML 1.0 character, tags nest and close by name, attributes are
    /// double-quoted without raw <c>&lt;</c>, and every <c>&amp;</c> is an entity.
    /// </summary>
    public static void AssertWellFormed(string xml)
    {
        if (!IsXmlChars(xml))
        {
            throw new InvalidOperationException($"XML contains a forbidden character:\n{xml}");
        }

        const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
        if (!xml.StartsWith(Declaration, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("missing XML declaration");
        }

        var body = xml[Declaration.Length..];
        Stack<string> stack = [];
        var roots = 0;
        foreach (var token in Tokens().Matches(body).Select(match => match.Value))
        {
            if (token.StartsWith("</", StringComparison.Ordinal))
            {
                var name = token[2..^1].Trim();
                if (!stack.TryPop(out var open) || open != name)
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

            if (stack.Count == 0 && token.Trim().Length > 0)
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
    }

    /// <summary>Renders and asserts well-formedness, so every case also parses.</summary>
    public static string Render(R.Report document, JunitReportOptions? options = null)
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

    /// <summary>Upstream's <c>toMatchObject</c> over the root's attributes.</summary>
    public static void AssertRoot(string xml, params (string Key, string Value)[] expected)
    {
        var actual = RootAttributes(xml);
        foreach (var (key, value) in expected)
        {
            Assert.Equal(value, actual[key]);
        }
    }

    public static R.ReportError Error(
        R.ErrorCategory category = R.ErrorCategory.Test,
        string code = "ASSERTION_FAILED",
        string message = "expected \"Sign in\" to be visible",
        R.ErrorPhase? phase = null
    ) =>
        new()
        {
            Category = category,
            Code = code,
            Message = message,
            Phase = phase,
        };

    public static R.ReportStep Step(
        R.StepStatus status = R.StepStatus.Passed,
        R.ReportError? error = null,
        string? actor = null
    ) =>
        new()
        {
            Id = "step-0",
            Kind = R.StepKind.Locator,
            Api = "locator.tap",
            Label = "tap Sign in",
            Status = status,
            StartedAt = At,
            DurationMs = 100,
            Error = error,
            Actor = actor,
        };

    public static R.ReportAttempt Attempt(
        string id = "attempt-1",
        int index = 0,
        R.AttemptStatus status = R.AttemptStatus.Passed,
        long durationMs = 1234,
        R.ReportError? error = null,
        IReadOnlyList<R.ReportStep>? steps = null,
        R.FailureEvidence? failure = null,
        IReadOnlyList<R.ArtifactRecord>? artifacts = null,
        R.ExpectedFailureRecord? expectedFailure = null
    ) =>
        new()
        {
            Id = id,
            Index = index,
            Status = status,
            StartedAt = At,
            DurationMs = durationMs,
            Error = error,
            Steps = steps ?? [],
            Failure = failure,
            Artifacts = artifacts ?? [],
            ExpectedFailure = expectedFailure,
        };

    public static R.ReportResult Result(
        string id = "result-1",
        string file = "tests/auth.e2e.ts",
        IReadOnlyList<string>? titlePath = null,
        string targetId = "web",
        string agent = "default",
        bool selected = true,
        R.ResultStatus status = R.ResultStatus.Passed,
        R.SkipInfo? skip = null,
        IReadOnlyList<R.ReportAttempt>? attempts = null
    ) =>
        new()
        {
            Id = id,
            File = file,
            TitlePath = titlePath ?? ["auth", "signs in"],
            TargetId = targetId,
            Agent = agent,
            Selected = selected,
            Status = status,
            Skip = skip,
            Attempts = attempts ?? [Attempt()],
        };

    public static R.Report Document(
        IReadOnlyList<R.ReportResult>? results = null,
        IReadOnlyList<R.ReportError>? errors = null
    ) =>
        new()
        {
            Run = new R.ReportRun
            {
                Id = "run-1",
                Results = results ?? [],
                Errors = errors ?? [],
            },
        };

    /// <summary>A screenshot artifact; <paramref name="path"/> and <paramref name="url"/> are set only when given.</summary>
    public static R.ArtifactRecord ShotArtifact(string id, string? path = null, string? url = null) =>
        new()
        {
            Id = id,
            Kind = R.ArtifactKind.Screenshot,
            MediaType = "image/png",
            Redaction = R.ArtifactRedaction.Complete,
            Path = path,
            Url = url,
        };

    /// <summary>
    /// A report with one failed test whose attempt recorded <c>checkout/001-failure.png</c> as its failure screenshot. <paramref name="error"/>, <paramref name="failure"/>, and <paramref name="artifacts"/> replace
    /// those of that attempt; <paramref name="extraAttempts"/> follow it, and <paramref name="status"/> sets the result's.
    /// </summary>
    public static R.Report FailedWithShot(
        R.ReportError? error = null,
        R.FailureEvidence? failure = null,
        IReadOnlyList<R.ArtifactRecord>? artifacts = null,
        R.AttemptStatus attemptStatus = R.AttemptStatus.Failed,
        R.ResultStatus status = R.ResultStatus.Failed,
        IReadOnlyList<R.ReportAttempt>? extraAttempts = null
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
                        failure: failure ?? new R.FailureEvidence { Screenshot = "shot-1" },
                        artifacts: artifacts ?? [ShotArtifact("shot-1", "checkout/001-failure.png")]
                    ),
                    .. extraAttempts ?? [],
                ]
            ),
        ]);
}
