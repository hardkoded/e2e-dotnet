// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;
using E2E.Tests.Traces;
using JunitReport = E2E.Reports.JunitReport;
using JunitReportOptions = E2E.Reports.JunitReportOptions;

namespace E2E.Tests.ReportJunit;

/// <summary>The helpers upstream's <c>unit/report-junit.test.ts</c> declares at the top of the file, over the port's records.</summary>
internal static partial class JunitKit
{
    [GeneratedRegex("""^<([A-Za-z_][\w.-]*)((?:\s+[A-Za-z_][\w.-]*="[^"<]*")*)\s*(/?)>$""")]
    private static partial Regex StartTag();

    [GeneratedRegex(@"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)")]
    private static partial Regex RawAmpersand();

    [GeneratedRegex("<[^>]*>|[^<]+")]
    private static partial Regex Tokens();

    [GeneratedRegex("<testsuites ([^>]*)>")]
    private static partial Regex Root();

    [GeneratedRegex("(\\w+)=\"([^\"]*)\"")]
    private static partial Regex Attribute();

    /// <summary>XML 1.0 <c>Char</c>, over code points: a lone surrogate falls outside every range.</summary>
    private static bool IsXmlText(string xml)
    {
        for (var i = 0; i < xml.Length; i++)
        {
            var c = xml[i];
            if (char.IsHighSurrogate(c) && i + 1 < xml.Length && char.IsLowSurrogate(xml[i + 1]))
            {
                i++;
                continue;
            }

            if (!(c is '\t' or '\n' or '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Strict well-formedness check standing in for a parser: every code unit is an XML 1.0 character, tags nest and close by name,
    /// attributes are double-quoted without raw <c>&lt;</c>, and every <c>&amp;</c> is an entity.
    /// </summary>
    public static void AssertWellFormed(string xml)
    {
        if (!IsXmlText(xml))
        {
            throw new InvalidOperationException("XML contains a forbidden character:\n" + xml);
        }

        const string declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
        if (!xml.StartsWith(declaration, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("missing XML declaration");
        }

        var body = xml[declaration.Length..];
        Stack<string> stack = [];
        var roots = 0;
        foreach (Match match in Tokens().Matches(body))
        {
            var token = match.Value;
            if (token.StartsWith("</", StringComparison.Ordinal))
            {
                var name = token[2..^1].Trim();
                if (!stack.TryPop(out var open) || open != name)
                {
                    throw new InvalidOperationException("unbalanced close tag " + token);
                }

                continue;
            }

            if (token.StartsWith('<'))
            {
                var tag = StartTag().Match(token);
                if (!tag.Success)
                {
                    throw new InvalidOperationException("malformed tag " + token);
                }

                if (RawAmpersand().IsMatch(tag.Groups[2].Value))
                {
                    throw new InvalidOperationException("raw ampersand in " + token);
                }

                if (stack.Count == 0)
                {
                    roots++;
                }

                if (tag.Groups[3].Value != "/")
                {
                    stack.Push(tag.Groups[1].Value);
                }

                continue;
            }

            if (RawAmpersand().IsMatch(token))
            {
                throw new InvalidOperationException("raw ampersand in text " + token);
            }

            if (stack.Count == 0 && token.Trim().Length != 0)
            {
                throw new InvalidOperationException("text outside the root: " + token);
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidOperationException("unclosed elements: " + string.Join(", ", stack));
        }

        if (roots != 1)
        {
            throw new InvalidOperationException("expected one root element, found " + roots);
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
        var match = Root().Match(xml);
        if (!match.Success)
        {
            throw new InvalidOperationException("no <testsuites> root in:\n" + xml);
        }

        return Attribute().Matches(match.Groups[1].Value).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    /// <summary>Upstream's <c>reportDocument({ results, errors })</c>.</summary>
    public static Report1Document Doc(
        IReadOnlyList<ReportResult>? results = null,
        IReadOnlyList<ReportError>? errors = null
    )
    {
        var document = ReportBuilders.Document([.. results ?? []]);
        return errors is null ? document : document with { Run = document.Run with { Errors = errors } };
    }

    /// <summary>A screenshot artifact; <paramref name="path"/> and <paramref name="url"/> are set only when given.</summary>
    public static ReportArtifact ShotArtifact(string id, string? path = null, string? url = null) =>
        new(id, "screenshot", path, url);

    /// <summary>
    /// A report with one failed test whose attempt recorded <c>checkout/001-failure.png</c> as its failure screenshot. <paramref name="attempt"/>
    /// replaces that attempt; <paramref name="extraAttempts"/> follow it, and <paramref name="status"/> sets the result's.
    /// </summary>
    public static Report1Document FailedWithShot(
        Func<ReportAttempt, ReportAttempt>? attempt = null,
        ResultStatus status = ResultStatus.Failed,
        IReadOnlyList<ReportAttempt>? extraAttempts = null
    )
    {
        var first = ReportBuilders.Attempt(
            AttemptStatus.Failed,
            ReportBuilders.Error(message: "heading not visible")
        ) with
        {
            Failure = new E2E.FailureEvidence { Screenshot = "shot-1" },
            Artifacts = [ShotArtifact("shot-1", "checkout/001-failure.png")],
        };
        return Doc([
            ReportBuilders.Result(
                titlePath: ["checkout"],
                status: status,
                attempts: [attempt?.Invoke(first) ?? first, .. extraAttempts ?? []]
            ),
        ]);
    }
}
