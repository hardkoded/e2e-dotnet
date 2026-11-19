// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Reports;

namespace E2E.HtmlReporter;

/// <summary>What the page does with screenshots, videos, and other files (upstream's <c>media</c> option).</summary>
public enum HtmlReporterMedia
{
    /// <summary>Links them from the results directory, so keep the output directory together when you move or upload it. The default.</summary>
    Link,

    /// <summary>Copies them into the page, so the one file is the whole report. Device videos make it large: tens of megabytes per minute.</summary>
    Embed,

    /// <summary>Leaves them out; the page shows text only.</summary>
    None,
}

/// <summary>Options of <see cref="HtmlReporter"/> (upstream's <c>HtmlReporterOptions</c>, <c>reporter.ts</c>).</summary>
public sealed record HtmlReporterOptions
{
    /// <summary>
    /// Where the page goes, from the project root. Defaults to <c>report.html</c> in the run's output directory (<c>.e2e/report.html</c>), beside the
    /// <c>results/</c> directory its screenshots and videos link into.
    /// </summary>
    public string? OutputFile { get; init; }

    /// <summary>What the page does with screenshots, videos, and other files.</summary>
    public HtmlReporterMedia Media { get; init; } = HtmlReporterMedia.Link;
}

/// <summary>
/// The HTML reporter (upstream's <c>htmlReporter()</c>, <c>reporter.ts</c>). List it in the <c>reporters</c> of <c>e2e.config.json</c>:
/// <c>{ "type": "E2E.HtmlReporter.HtmlReporter, E2E.HtmlReporter", "options": { "media": "embed" } }</c>. Once the run is over it writes one page with the
/// run's report-1 document in it, which opens straight from disk. <see cref="HtmlReporterOptions.Media"/> decides whether screenshots and videos are
/// linked from the results directory (the default), embedded in the page, or left out.
/// </summary>
public class HtmlReporter : Reporter, IConfigurableReporter
{
    private const string TemplateResource = "app/index.html";

    private HtmlReporterOptions _options = new();

    /// <summary>A reporter with the default options. The public constructor the config builds it with.</summary>
    public HtmlReporter() { }

    /// <inheritdoc />
    public override string Name => "html";

    /// <summary>
    /// Reads the <c>options</c> of the config entry (.NET-only: upstream's <c>htmlReporter({ media })</c> is called in code). The options are
    /// <c>outputFile</c>, a string, and <c>media</c>, one of <c>"link"</c>, <c>"embed"</c>, or <c>"none"</c>. An unknown option or a value of the wrong
    /// type is an <see cref="ArgumentException"/> that names the option and never its value.
    /// </summary>
    public void Configure(JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("options must be an object");
        }

        var configured = new HtmlReporterOptions();
        foreach (var option in options.EnumerateObject())
        {
            configured = option.Name switch
            {
                "outputFile" when option.Value.ValueKind == JsonValueKind.String => configured with
                {
                    OutputFile = option.Value.GetString(),
                },
                "outputFile" => throw new ArgumentException("option \"outputFile\" must be a string"),
                "media" when option.Value.ValueKind == JsonValueKind.String => configured with
                {
                    Media = option.Value.GetString() switch
                    {
                        "link" => HtmlReporterMedia.Link,
                        "embed" => HtmlReporterMedia.Embed,
                        "none" => HtmlReporterMedia.None,
                        _ => throw new ArgumentException("option \"media\" must be \"link\", \"embed\", or \"none\""),
                    },
                },
                "media" => throw new ArgumentException("option \"media\" must be a string"),
                _ => throw new ArgumentException(
                    $"option \"{option.Name}\" is not supported; the options are \"outputFile\" and \"media\""
                ),
            };
        }

        _options = configured;
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(
        FinishedRun run,
        CancellationToken cancellationToken
    ) => [await WriteReportAsync(run, _options, ReadTemplate(), cancellationToken).ConfigureAwait(false)];

    /// <summary>
    /// Fills the page template with one finished run, writes it, and returns the row naming where it went (upstream's <c>writeReport</c>). Once
    /// <paramref name="cancellationToken"/> is cancelled (the runner gave up on its reporters), it stops reading files and writes nothing.
    /// </summary>
    internal static async Task<ReporterSummaryRow> WriteReportAsync(
        FinishedRun run,
        HtmlReporterOptions options,
        string template,
        CancellationToken cancellationToken = default
    )
    {
        var outputFile = options.OutputFile is null
            ? Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(run.ArtifactsRoot))!, "report.html")
            : Path.GetFullPath(options.OutputFile, run.ProjectRoot);
        var report = JsonNode.Parse(
            await File.ReadAllTextAsync(run.ReportPath, cancellationToken).ConfigureAwait(false)
        )!;
        var media = await PageMediaAsync(options.Media, run, report, outputFile, cancellationToken)
            .ConfigureAwait(false);
        var relative = ToPosix(Path.GetRelativePath(run.ProjectRoot, run.ArtifactsRoot));
        var resultsDir = relative.Length == 0 ? "." : relative;
        var traces = run.Traces.ToDictionary(trace => trace.Key, trace => ToPosix(trace.Value), StringComparer.Ordinal);
        var sources = await SourceFramesAsync(report, run.ProjectRoot, run.Redact, cancellationToken)
            .ConfigureAwait(false);
        var page = Payload.Embed(template, new ReportPayload(report, media, resultsDir, traces, sources));
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        var temp = $"{outputFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temp, page, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temp, outputFile, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }

        var shown = Path.GetRelativePath(Directory.GetCurrentDirectory(), outputFile);
        var note = options.Media switch
        {
            HtmlReporterMedia.Embed => $" (one file, {FormatSize(Encoding.UTF8.GetByteCount(page))})",
            HtmlReporterMedia.None => " (text only)",
            _ => "",
        };
        return new ReporterSummaryRow("HTML report", shown + note);
    }

    // The built page, filled per run; the build embeds it (upstream reads `app/index.html` beside the module).
    private static string ReadTemplate()
    {
        using var stream = typeof(HtmlReporter).Assembly.GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException("the report page is not part of this build of E2E.HtmlReporter");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // What the page holds for screenshots and videos in each `media` mode.
    private static async Task<ReportMedia> PageMediaAsync(
        HtmlReporterMedia mode,
        FinishedRun run,
        JsonNode report,
        string outputFile,
        CancellationToken cancellationToken
    ) =>
        mode switch
        {
            HtmlReporterMedia.Link => new ReportMedia(
                mode,
                Base: ToPosix(Path.GetRelativePath(Path.GetDirectoryName(outputFile)!, run.ArtifactsRoot))
            ),
            HtmlReporterMedia.Embed => new ReportMedia(
                mode,
                Files: await EmbedArtifactsAsync(report, run.ArtifactsRoot, cancellationToken).ConfigureAwait(false)
            ),
            _ => new ReportMedia(mode),
        };

    // Media types an embedded file keeps. An embedded file opens as a blob URL in the page's own origin, so a type a browser would run (HTML, SVG, a
    // download from the app under test) is stored as `application/octet-stream`, which the browser saves instead of rendering.
    private static bool IsInertType(string mediaType) =>
        mediaType
            is "image/png"
                or "image/jpeg"
                or "image/webp"
                or "image/gif"
                or "video/mp4"
                or "video/webm"
                or "video/quicktime"
                or "text/plain"
                or "application/json";

    // Every artifact file the report names, read into a `data:` URL keyed by its report path. A path that leaves the results directory is never read, and a
    // file that is gone (deleted by `video: 'retain-on-failure'`, say) is left out, so the page shows it as missing. So is a download the runner could not
    // scan for secrets (`redaction: 'incomplete'`): the page is a file to send.
    private static async Task<Dictionary<string, string>> EmbedArtifactsAsync(
        JsonNode report,
        string artifactsRoot,
        CancellationToken cancellationToken
    )
    {
        var run = report["run"]!;
        var attempts = run["results"]!
            .AsArray()
            .SelectMany(result => result!["attempts"]!.AsArray())
            .Concat(run["serialGroups"]!.AsArray().SelectMany(group => group!["attempts"]!.AsArray()));
        // One entry per file: a serial group's attempt and its members name the same artifacts.
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var artifact in attempts.SelectMany(attempt => attempt!["artifacts"]!.AsArray()))
        {
            var path = artifact!["path"]?.GetValue<string>();
            if (
                path is null
                || (
                    artifact["kind"]?.GetValue<string>() == "download"
                    && artifact["redaction"]?.GetValue<string>() == "incomplete"
                )
            )
            {
                continue;
            }

            types[path] = artifact["mediaType"]!.GetValue<string>();
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (artifactPath, mediaType) in types)
        {
            var file = Path.GetFullPath(artifactPath, artifactsRoot);
            if (!IsInside(artifactsRoot, file))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }

            var type = IsInertType(mediaType) ? mediaType : "application/octet-stream";
            files[artifactPath] = $"data:{type};base64,{Convert.ToBase64String(bytes)}";
        }

        return files;
    }

    /// <summary>Lines a code frame shows before and after the line it points at.</summary>
    private const int FrameBefore = 2;

    private const int FrameAfter = 2;

    /// <summary>A longer line is cut: a frame shows code, not a minified bundle.</summary>
    private const int MaxLineChars = 400;

    // The lines around every source location in the report (each step's, each error's, each test's), read from the project's files and redacted, since a
    // test file can hold a secret's value. A file outside the project root, or one that is gone, is left out, and so is its frame.
    private static async Task<Dictionary<string, IReadOnlyDictionary<string, string>>> SourceFramesAsync(
        JsonNode report,
        string projectRoot,
        Func<string, string> redact,
        CancellationToken cancellationToken
    )
    {
        var wanted = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        CollectSources(report["run"], (file, line) =>
        {
            if (!wanted.TryGetValue(file, out var lines))
            {
                wanted[file] = lines = [];
            }

            for (long at = Math.Max(1L, (long)line - FrameBefore); at <= (long)line + FrameAfter; at++)
            {
                lines.Add((int)at);
            }
        });
        var frames = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (file, lines) in wanted)
        {
            var absolute = Path.GetFullPath(file, projectRoot);
            if (!IsInside(projectRoot, absolute))
            {
                continue;
            }

            string text;
            try
            {
                text = await File.ReadAllTextAsync(absolute, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var all = text.Split(["\r\n", "\n"], StringSplitOptions.None);
            var frame = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in lines.Where(line => line <= all.Length))
            {
                var content = redact(all[line - 1]);
                frame[line.ToString(CultureInfo.InvariantCulture)] =
                    content.Length > MaxLineChars ? content[..MaxLineChars] : content;
            }

            frames[file] = frame;
        }

        return frames;
    }

    // Calls `found` for every `{ file, line, column }` source object anywhere under `value`.
    private static void CollectSources(JsonNode? value, Action<string, int> found)
    {
        switch (value)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectSources(item, found);
                }

                break;
            case JsonObject obj:
                foreach (var (key, child) in obj)
                {
                    if (
                        key == "source"
                        && child is JsonObject { } source
                        && source["file"] is JsonValue file
                        && file.TryGetValue<string>(out var path)
                        && source["line"] is JsonValue line
                        && line.TryGetValue<int>(out var number)
                    )
                    {
                        found(path, number);
                    }
                    else
                    {
                        CollectSources(child, found);
                    }
                }

                break;
        }
    }

    private static bool IsInside(string root, string file)
    {
        var inside = Path.GetRelativePath(root, file);
        return inside != "." && !inside.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(inside);
    }

    private static string ToPosix(string relative) => relative.Replace(Path.DirectorySeparatorChar, '/');

    private static string FormatSize(long bytes) =>
        bytes < 1024 * 1024
            ? $"{Math.Ceiling(bytes / 1024.0).ToString(CultureInfo.InvariantCulture)} KB"
            : $"{(bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture)} MB";
}
