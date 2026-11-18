// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.HtmlReporter;

/// <summary>
/// What the reporter hands the page, and how it rides inside it (upstream's <c>payload.ts</c>). The page is one prebuilt HTML file with a placeholder
/// JSON script; the reporter swaps the run's data in, so the result opens from disk with no server and no fetch. The page reads the same shape
/// (<c>app/payload.ts</c>).
/// </summary>
internal static class Payload
{
    /// <summary>The text the built page holds in place of the payload, until a reporter fills it (upstream's <c>PAYLOAD_PLACEHOLDER</c>).</summary>
    public const string Placeholder = "\"__E2E_REPORT_PAYLOAD__\"";

    /// <summary>
    /// The page with the payload in place of the placeholder (upstream's <c>embedPayload</c>). <c>&lt;</c> is escaped inside the JSON so no value (a test
    /// title, an error message) can close the script element or open a comment; <c>JSON.parse</c> reads it back as <c>&lt;</c>.
    /// </summary>
    public static string Embed(string template, ReportPayload payload)
    {
        var parts = template.Split(Placeholder);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                "the report page template must hold the payload placeholder exactly once"
            );
        }

        return parts[0] + payload.ToJson().Replace("<", "\\u003c", StringComparison.Ordinal) + parts[1];
    }
}

/// <summary>
/// How the page reaches the run's screenshots, videos, and files (upstream's <c>ReportMedia</c>): by relative path under <see cref="Base"/> (<c>link</c>),
/// from <see cref="Files"/>, each artifact path's bytes as a <c>data:</c> URL (<c>embed</c>), or not at all (<c>none</c>).
/// </summary>
internal sealed record ReportMedia(
    HtmlReporterMedia Mode,
    string? Base = null,
    IReadOnlyDictionary<string, string>? Files = null
)
{
    public JsonObject ToJson() =>
        Mode switch
        {
            HtmlReporterMedia.Link => new JsonObject { ["mode"] = "link", ["base"] = Base },
            HtmlReporterMedia.Embed => new JsonObject
            {
                ["mode"] = "embed",
                ["files"] = new JsonObject(Files!.Select(file => KeyValuePair.Create(file.Key, (JsonNode?)file.Value))),
            },
            _ => new JsonObject { ["mode"] = "none" },
        };
}

/// <summary>The data one report page renders (upstream's <c>ReportPayload</c>).</summary>
/// <param name="Report">The run's report-1 document, unchanged.</param>
/// <param name="Media">How the page reaches screenshots, videos, and files.</param>
/// <param name="ResultsDir">The run's results directory from the project root, a POSIX path (<c>.e2e/results</c>). The page names files there in the prompt it copies for a coding agent.</param>
/// <param name="Traces">Each kept <c>trace.md</c> by report result id, as a path from the project root.</param>
/// <param name="Sources">Lines of the project's test files around each step and error, by file (as the report names it) then line number, for the page's code frames.</param>
internal sealed record ReportPayload(
    JsonNode Report,
    ReportMedia Media,
    string ResultsDir,
    IReadOnlyDictionary<string, string> Traces,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Sources
)
{
    public string ToJson() =>
        new JsonObject
        {
            ["report"] = Report.DeepClone(),
            ["media"] = Media.ToJson(),
            ["resultsDir"] = ResultsDir,
            ["traces"] = new JsonObject(Traces.Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)pair.Value))),
            ["sources"] = new JsonObject(
                Sources.Select(file =>
                    KeyValuePair.Create(
                        file.Key,
                        (JsonNode?)
                            new JsonObject(
                                file.Value.Select(line => KeyValuePair.Create(line.Key, (JsonNode?)line.Value))
                            )
                    )
                )
            ),
        }.ToJsonString(
            new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }
        );
}
