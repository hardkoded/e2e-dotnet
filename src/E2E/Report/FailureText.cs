// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Internal;
using ArtifactProducerKind = E2E.Reports.ArtifactProducerKind;
using static E2E.MarkdownText;

namespace E2E;

/// <summary>What a trace page needs to know about where it is read and what is beside it. Upstream's <c>TracePageOptions</c>.</summary>
internal sealed record TracePageOptions
{
    /// <summary>The directory the artifact paths are relative to, as the reader should see it.</summary>
    public string? ArtifactsDir { get; init; }

    /// <summary>
    /// The page's own directory, relative to the one the artifact paths are: each file then links from the page, and the screenshot
    /// at failure shows inline, wherever the page is read.
    /// </summary>
    public string? PageDir { get; init; }

    /// <summary>Reads one artifact by its path, for inlining the screen text; null when the files are not at hand.</summary>
    public Func<string, string?>? ReadArtifact { get; init; }

    /// <summary>The replay cache directory as the reader should see it; a step's entry is named as a file there.</summary>
    public string? CacheDir { get; init; }

    /// <summary>The page is the whole record of a hosted session: there is no <c>report.json</c> to point at.</summary>
    public bool Standalone { get; init; }
}

/// <summary>A step that did not pass, with its position on the page.</summary>
internal sealed record FailedStep(int Index, StepRecord Step);

/// <summary>
/// A failed test as text (upstream's <c>report/failure-text.ts</c>): the page that tells the whole story, with every step and the
/// screen at failure inline. The session writes one such page per attempt that kept a trace, as <c>trace.md</c> in the test's directory.
/// The port has no run page, so the facts only that page states are not here. It also leaves out what it cannot fill: a skipped
/// attempt's reason, the cache's <c>recordedBy</c>, and the link to a commit.
/// </summary>
internal static partial class FailureText
{
    /// <summary>Prose fields (the agent's explanation, a detail value) get more room than a cell.</summary>
    public const int MaxDetailChars = 600;

    /// <summary>Screen text inlined in a page past this size is cut, and the page says so.</summary>
    private const int MaxScreenChars = 24_000;

    /// <summary>Result lines of one turn a page quotes; the loop's own notes are quoted past them.</summary>
    private const int MaxTurnLines = 12;

    /// <summary>How the agent loop marks a note it wrote onto a turn: a loop guard, a wind-down, a retry.</summary>
    private const string LoopNotePrefix = "[loop] ";

    /// <summary>Events a page lists under one step before saying how many more there were.</summary>
    private const int MaxStepEvents = 20;

    /// <summary>App log lines the page lists in its own section; errors and warnings take the slots first, then the latest of the rest.</summary>
    private const int MaxAppLogLines = 50;

    private static readonly Dictionary<string, string> CacheReasonText = new(StringComparer.Ordinal)
    {
        ["retry"] = "a retry never replays",
        ["no-entry"] = "nothing recorded for this step yet",
        ["invalid-entry"] = "the entry could not be read",
        ["truncated"] = "the recording is incomplete",
        ["wrong-context"] = "the recording started on another screen",
        ["gap"] = "the recording reached an action it cannot replay",
        ["target-not-found"] = "a recorded target is not on the screen",
        ["target-ambiguous"] = "a recorded target matches several nodes",
        ["viewport-changed"] = "the viewport changed size",
        ["action-failed"] = "a recorded action failed",
        ["action-uncertain"] = "a recorded action may or may not have landed",
        ["end-mismatch"] = "the recorded end state did not show",
    };

    private static readonly Dictionary<CacheWrite, string> CacheWriteText = new()
    {
        [CacheWrite.Saved] = "recording saved",
        [CacheWrite.Kept] = "recording kept",
        [CacheWrite.Unconfirmed] = "recording not saved: no check passed after this step",
        [CacheWrite.Evicted] = "recording deleted: its replay ended in a failure or a repair, or no check confirmed it",
        [CacheWrite.NoChange] = "nothing recorded: the step changed nothing a replay could check",
        [CacheWrite.NotWritten] = "recording not saved: the cache store did not write it",
    };

    [GeneratedRegex(@"https?://(?:127\.0\.0\.1|localhost|\[::1\])(?::\d+)?(?=/)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LoopbackOrigin();

    /// <summary>
    /// The structured facts of an error, one per line: an assertion's expected, its observed with the match count, what a locator
    /// asked for, how long it waited. The locator as written is the step's label already.
    /// </summary>
    public static List<string> DetailLines(ReportError? error)
    {
        if (error?.Details is not { } details)
        {
            return [];
        }

        List<string> lines = [];
        var matched = details.Matches is { } count ? count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? "match" : "matches") : null;
        if (details.Expected is not null)
        {
            lines.Add("Expected: " + Cell(details.Expected, MaxDetailChars));
        }

        if (details.Observed is not null)
        {
            lines.Add("Observed: " + Cell(details.Observed, MaxDetailChars) + (matched is null ? "" : " (" + matched + ")"));
        }
        else if (matched is not null)
        {
            lines.Add("Matched: " + matched);
        }

        List<string> asked =
        [
            .. details.Role is null ? [] : new[] { Cell(details.Role, MaxIdChars) },
            .. details.Name is null ? [] : new[] { "\"" + Cell(details.Name, MaxLabelChars) + "\"" },
            .. details.TestId is null ? [] : new[] { "test id \"" + Cell(details.TestId, MaxLabelChars) + "\"" },
        ];
        if (asked.Count > 0)
        {
            lines.Add("Asked for: " + string.Join(' ', asked));
        }

        if (details.WaitedMs is > 0)
        {
            lines.Add("Waited: " + FormatDuration(details.WaitedMs.Value));
        }

        return lines;
    }

    /// <summary>A step's label as the reader should see it: an agent step's is a sentence the author wrote, quoted; any other step's is the locator or value it was called with, which reads as code.</summary>
    public static string StepLabel(StepRecord step, int max = MaxLabelChars)
    {
        if (step.Label.Length == 0)
        {
            return "";
        }

        return step.Kind == StepKind.Agent ? "\"" + Cell(step.Label, max) + "\"" : Code(step.Label, max);
    }

    /// <summary>
    /// The attempt whose story a page tells: a flaky test's last failure; otherwise the outcome's verdict attempt, so a retry the
    /// run interrupted tells nothing.
    /// </summary>
    public static ReportAttempt ToldAttempt(ReportResult result, Outcome final)
    {
        if (result.Status == ResultStatus.Flaky)
        {
            return final.LastFailed ?? final.Final;
        }

        return final.Verdict;
    }

    /// <summary>
    /// The step whose error ended the attempt, with its position: the step in the error's phase whose own error carries its code
    /// and line, else the last with its code, since a caught failure comes earlier, else the step a timeout or interrupt cut.
    /// </summary>
    public static FailedStep? FailedStepOf(ReportAttempt attempt)
    {
        if (attempt.Error is not { } error)
        {
            return null;
        }

        var candidates = attempt.Steps
            .Select((step, index) => new FailedStep(index, step))
            .Where(item => item.Step.Status != StepStatus.Passed && (error.Phase is null || string.Equals(PhaseName(item.Step.Phase), error.Phase, StringComparison.Ordinal)))
            .ToList();
        var alike = candidates.Where(item => string.Equals(item.Step.Error?.Code, error.Code, StringComparison.Ordinal)).ToList();
        if (error.Source is { } line && alike.Count > 0)
        {
            // The step's own error, not its source: that is the call site, a line below the `await` of a split call.
            return alike.LastOrDefault(item => item.Step.Error?.Source is { } own && string.Equals(own.File, line.File, StringComparison.Ordinal) && own.Line == line.Line);
        }

        return alike.LastOrDefault() ?? candidates.LastOrDefault(item => item.Step.Status == StepStatus.Cancelled);
    }

    /// <summary>
    /// How the attempts compare, when there was more than one: the same code at the same step every time is a deterministic
    /// failure, not a flake, and the reader should know before opening anything. One sentence, or null for a single attempt.
    /// </summary>
    public static string? AttemptsLine(ReportResult result, Outcome final)
    {
        // An attempt that recorded nothing for this test, or one the run interrupted, says nothing about how the test fails.
        var failed = final.Attempts
            .Where(attempt => attempt.Status is not (AttemptStatus.Passed or AttemptStatus.Skipped or AttemptStatus.Interrupted) && (attempt.Error is not null || attempt.Steps.Count > 0))
            .Select(attempt => (Code: Cell(attempt.Error?.Code ?? AttemptStatusName(attempt.Status), 128), Step: FailedStepOf(attempt)?.Index))
            .ToList();
        if (failed.Count == 0 || final.Attempts.Count < 2 || failed.Count < 2)
        {
            return null;
        }

        var first = failed[0];
        static string Where(int? step) => step is null ? "" : " at step " + (step.Value + 1).ToString(CultureInfo.InvariantCulture);
        var count = failed.Count == final.Attempts.Count
            ? failed.Count == 2 ? "both attempts" : "all " + failed.Count.ToString(CultureInfo.InvariantCulture) + " attempts"
            : Plural(failed.Count, "attempt");

        // Consecutive attempts that failed alike are one run: `at step 8, then at step 20 (3 times)`.
        List<(string Code, int? Step, int Count)> runs = [];
        foreach (var attempt in failed)
        {
            if (runs.Count > 0 && string.Equals(runs[^1].Code, attempt.Code, StringComparison.Ordinal) && runs[^1].Step == attempt.Step)
            {
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
            }
            else
            {
                runs.Add((attempt.Code, attempt.Step, 1));
            }
        }

        if (runs.Count == 1)
        {
            return "Failed the same way on " + count + ": **" + first.Code + "**" + Where(first.Step) + "." + (result.Status == ResultStatus.Flaky ? " The retry that passed is the exception." : "");
        }

        var told = runs.Select((run, index) =>
        {
            var named = index == 0 || !string.Equals(run.Code, runs[index - 1].Code, StringComparison.Ordinal);
            var place = ((named ? "**" + run.Code + "**" : "") + Where(run.Step)).TrimStart();
            return run.Count == 1 ? place : place + " (" + run.Count.ToString(CultureInfo.InvariantCulture) + " times)";
        });
        return "Failed on " + count + ": " + string.Join(", then ", told) + ".";
    }

    /// <summary>The line to look at: the one the failure unwound through when the stack named it, else the failing step's own call, else the test's declaration.</summary>
    public static SourceLocation FailureSource(ReportResult result, ReportAttempt told)
    {
        if (told.Error?.Source is { } fromError)
        {
            return fromError;
        }

        return FailedStepOf(told)?.Step.Source ?? result.Source;
    }

    /// <summary>Artifacts of the told attempt by id, the failure's own first, one per kind.</summary>
    public static IReadOnlyList<ReportArtifact> EvidenceOf(ReportAttempt told)
    {
        var byId = told.Artifacts.GroupBy(artifact => artifact.Id).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<ReportArtifact> own = [];
        foreach (var id in new[] { told.Failure?.Screenshot, told.Failure?.Screen })
        {
            if (id is not null && byId.TryGetValue(id, out var artifact))
            {
                own.Add(artifact);
            }
        }

        HashSet<string> seen = [.. own.Select(artifact => artifact.Id)];
        HashSet<string> kinds = [];
        List<ReportArtifact> rest = [];
        foreach (var artifact in told.Artifacts)
        {
            if (seen.Contains(artifact.Id) || kinds.Contains(artifact.Kind) || string.Equals(artifact.Kind, "log", StringComparison.Ordinal))
            {
                continue;
            }

            kinds.Add(artifact.Kind);
            rest.Add(artifact);
        }

        return [.. own, .. rest];
    }

    /// <summary>A turn's outcome as the page quotes it: the first result lines, then every loop note, which come last and say why the step ended.</summary>
    private static List<string> TurnOutcomeLines(string outcome)
    {
        var lines = outcome.Split('\n');
        var notes = lines.Where(line => line.StartsWith(LoopNotePrefix, StringComparison.Ordinal));
        var results = lines.Where(line => !line.StartsWith(LoopNotePrefix, StringComparison.Ordinal)).ToList();
        return [.. results.Take(MaxTurnLines), .. results.Count > MaxTurnLines ? new[] { "…" } : [], .. notes];
    }

    /// <summary>One line a page lists, when it happened, and whether it outranks the rest when there is no room for all.</summary>
    private sealed record PageItem(DateTimeOffset At, bool Important, string Line);

    /// <summary>
    /// The items a page has room for, in the order they happened: past <paramref name="max"/>, the important ones take the slots
    /// first (the latest of them when they alone pass it), then the latest of the rest. The end is where it went wrong. Also says how
    /// many it left out.
    /// </summary>
    private static (List<PageItem> Shown, int Left) ImportantThenLatest(IEnumerable<PageItem> items, int max)
    {
        var ordered = items.OrderBy(item => item.At).ToList();
        if (ordered.Count <= max)
        {
            return (ordered, 0);
        }

        HashSet<int> kept = [.. Enumerable.Range(0, ordered.Count).Where(index => ordered[index].Important).TakeLast(max)];
        for (var index = ordered.Count - 1; index >= 0 && kept.Count < max; index--)
        {
            kept.Add(index);
        }

        var shown = Enumerable.Range(0, ordered.Count).Where(kept.Contains).Select(index => ordered[index]).ToList();
        return (shown, ordered.Count - shown.Count);
    }

    /// <summary>One app log line as a page shows it: <c>✗ network error: `GET /api 500`</c>.</summary>
    private static string AppLogLine(AppLogRecord entry)
    {
        var glyph = entry.Level switch { AppLogLevel.Error => "✗", AppLogLevel.Warning => "⚠", _ => "ℹ" };
        var text = Code(ShortenLoopback(entry.Text), MaxDetailChars);
        var source = entry.Source switch { AppLogSource.Console => "console", AppLogSource.Error => "uncaught", AppLogSource.Network => "network", _ => "system" };
        return entry.Level == AppLogLevel.Info
            ? glyph + " " + source + ": " + text
            : glyph + " " + source + " " + (entry.Level == AppLogLevel.Error ? "error" : "warning") + ": " + text;
    }

    /// <summary>
    /// The lines under one step on the page, oldest first: the cache's decision for an agent step, then each action it took (the
    /// node it landed on), each poll that waited or failed (the values it read in order), where the app went, and every line the app
    /// logged while it ran.
    /// </summary>
    private static List<string> StepDetailLines(StepRecord step, IReadOnlyList<AppLogRecord> appLog, string? cacheDir)
    {
        List<string> lines = [];
        if (step.Cache is { } cache)
        {
            lines.Add(CacheLine(cache, step.CacheWrite, cacheDir));
        }

        var items = step.Events
            .Where(item => item.Kind is StepEventKind.Engine or StepEventKind.Navigation
                || (item.Kind == StepEventKind.Poll && ((item.Count ?? 0) > 1 || item.Status != StepEventStatus.Passed)))
            .Select(item => new PageItem(item.StartedAt, item.Status != StepEventStatus.Passed, EventLine(item)))
            .Concat(appLog.Where(entry => entry.Step == step.Index).Select(entry => new PageItem(entry.At, entry.Level != AppLogLevel.Info, AppLogLine(entry))));
        var (shown, left) = ImportantThenLatest(items, MaxStepEvents);
        if (left > 0)
        {
            lines.Add(left.ToString(CultureInfo.InvariantCulture) + " " + (left == 1 ? "event" : "events") + " left out");
        }

        lines.AddRange(shown.Select(item => item.Line));
        return lines;
    }

    /// <summary>
    /// The lines the app logged across the attempt (console output, uncaught errors, failed requests), oldest first, each with its
    /// step's number on the page, so a reader finds the log in one place rather than spread across steps. Past the cap, errors and
    /// warnings take the slots first, and a line counts what was left out.
    /// </summary>
    private static List<string> AppLogLines(IReadOnlyList<StepRecord> steps, IReadOnlyList<AppLogRecord> appLog)
    {
        var numbers = steps.Select((step, position) => (step.Index, Number: position + 1)).ToDictionary(item => item.Index, item => item.Number);
        var items = appLog.Select(entry =>
        {
            var where = entry.Step is { } step && numbers.TryGetValue(step, out var number) ? "step " + number.ToString(CultureInfo.InvariantCulture) : "before step 1";
            return new PageItem(entry.At, entry.Level != AppLogLevel.Info, where + " · " + AppLogLine(entry));
        });
        var (shown, left) = ImportantThenLatest(items, MaxAppLogLines);
        return [.. left == 0 ? [] : new[] { left.ToString(CultureInfo.InvariantCulture) + " " + (left == 1 ? "line" : "lines") + " left out" }, .. shown.Select(item => item.Line)];
    }

    private static string CacheLine(CacheInfo cache, CacheWrite? write, string? cacheDir)
    {
        string Reason(string? reason, string fallback)
        {
            var token = reason ?? fallback;
            return CacheReasonText.TryGetValue(token, out var text) ? text : token;
        }

        var how = cache.Mode switch
        {
            "self-finalized" => "replayed all " + Plural(cache.TotalActions, "recorded action") + ", no model call",
            "agent-concluded" => "replayed " + cache.ReplayedActions.ToString(CultureInfo.InvariantCulture) + " of " + Plural(cache.TotalActions, "recorded action") + ", then the agent took over: " + Reason(cache.Reason, "action-failed"),
            _ => "no replay: " + Reason(cache.Reason, "no-entry"),
        };
        List<string> parts = ["cache: " + how];
        if (cache.RecordedBy is not null)
        {
            parts.Add("recorded with " + Code(cache.RecordedBy, MaxDetailChars));
        }

        if (cache.Detail is not null)
        {
            parts.Add(Cell(cache.Detail, MaxDetailChars));
        }

        if (write is { } written)
        {
            parts.Add(CacheWriteText[written]);
        }

        if (string.Equals(cache.NotRecorded, "param-collision", StringComparison.Ordinal))
        {
            parts.Add("nothing recorded: a unique() value collides with another param");
        }

        var entry = cache.Entry is null || cacheDir is null ? "" : " (" + Code(cacheDir + "/" + cache.Entry + ".json", MaxPathChars) + ")";
        return string.Join("; ", parts) + entry;
    }

    private static string EventLine(StepEvent item)
    {
        var detail = item.Detail is null ? null : Cell(ShortenLoopback(item.Detail), MaxDetailChars);
        switch (item.Kind)
        {
            case StepEventKind.Navigation:
                // The page moving on, told in the engine's own words.
                return "↪ " + Cell(ShortenLoopback(item.Detail ?? "navigated"), MaxDetailChars);
            case StepEventKind.Poll:
                var verb = item.Status == StepEventStatus.Passed ? "passed after" : "gave up after";
                return (item.Name is null ? "poll" : Cell(item.Name, MaxIdChars)) + " " + verb + " " + Plural(item.Count ?? 1, "read") + " in " + FormatDuration(item.DurationMs) + (detail is null ? "" : ": " + detail);
            default:
                var what = detail ?? Cell(item.Name ?? EventKindName(item.Kind), MaxIdChars);
                if (item.Status == StepEventStatus.Passed)
                {
                    return what + " (" + FormatDuration(item.DurationMs) + ")";
                }

                return (item.Status == StepEventStatus.Cancelled ? "–" : "✗") + " " + what + (item.Code is null ? "" : ": **" + Cell(item.Code, 128) + "**");
        }
    }

    /// <summary>
    /// How the screen changed by the step, as page lines under it: the first screen's size, <c>unchanged</c>, or the nodes added,
    /// removed, and changed since the step before that saw one. Nothing for a step that saw no screen.
    /// </summary>
    private static List<string> StepScreenLines(StepScreen? screen)
    {
        if (screen is null)
        {
            return [];
        }

        var where = screen.Location is null ? "" : " at " + Code(ShortenLoopback(screen.Location), MaxCellChars);
        if (screen.Since is not { } since)
        {
            return ["   - screen: " + Plural(screen.Nodes, "node") + where];
        }

        var sinceStep = "step " + (since + 1).ToString(CultureInfo.InvariantCulture);
        if (screen.Changes.Count == 0)
        {
            return ["   - screen: unchanged since " + sinceStep + where];
        }

        var total = screen.Changes.Count + (screen.More ?? 0);
        return
        [
            "   - screen: " + Plural(total, "change") + " since " + sinceStep + where + ", " + Plural(screen.Nodes, "node"),
            .. screen.Changes.Select(change => "     - " + Code(ShortenLoopback(change), MaxDetailChars)),
            .. screen.More is { } more ? new[] { "     - " + more.ToString(CultureInfo.InvariantCulture) + " more" } : [],
        ];
    }

    /// <summary>Drops a loopback origin from the URLs in a line: nobody reading the page can open it, and the path says the rest.</summary>
    private static string ShortenLoopback(string text) => LoopbackOrigin().Replace(text, "");

    /// <summary>A video artifact with the span it covers; a recording a step stopped (<c>app.StopRecordingAsync</c>) ends with that step (upstream's <c>TimedVideo</c>).</summary>
    private sealed record TimedVideo(ReportArtifact Artifact, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

    /// <summary>The attempt's videos in the order they began, each with the span it covers (upstream's <c>timedVideos</c>).</summary>
    private static List<TimedVideo> TimedVideos(ReportAttempt told)
    {
        var ends = told.Steps.ToDictionary(step => step.Id, step => step.StartedAt.AddMilliseconds(step.DurationMs), StringComparer.Ordinal);
        return
        [
            .. told.Artifacts
                .Where(artifact => string.Equals(artifact.Kind, "video", StringComparison.Ordinal) && artifact.StartedAt is not null)
                .Select(artifact => new TimedVideo(
                    artifact,
                    artifact.StartedAt!.Value,
                    artifact.Producer is { Kind: ArtifactProducerKind.Step, StepId: { } stepId } && ends.TryGetValue(stepId, out var end) ? end : null))
                .OrderBy(video => video.StartsAt),
        ];
    }

    /// <summary>
    /// <c>, video 0:12</c>: where the step starts in the recording that was running then, the last segment that began before it. An
    /// attempt cut into several segments (a restart opens a new page) names the segment too: <c>, video-part2 0:03</c>. A step before the
    /// attempt's video began is at 0:00; one outside every recording a step stopped gets nothing.
    /// </summary>
    private static string VideoOffset(StepRecord step, IReadOnlyList<TimedVideo> videos)
    {
        var at = step.StartedAt;
        var timed = videos.LastOrDefault(candidate => candidate.StartsAt <= at && (candidate.EndsAt is not { } end || at < end))
            ?? videos.FirstOrDefault(candidate => candidate.EndsAt is null && at < candidate.StartsAt);
        if (timed is null)
        {
            return "";
        }

        var video = timed.Artifact;
        var seconds = Math.Max(0, (long)Math.Floor((at - timed.StartsAt).TotalSeconds));
        var name = videos.Count > 1 && video.Path is not null ? System.IO.Path.GetFileNameWithoutExtension(video.Path) : "video";
        return ", " + name + " " + (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The kind (or <paramref name="label"/>) of an artifact, linked from the page when it is at hand, and the file's path so the reader finds it.</summary>
    private static string ArtifactPath(ReportArtifact artifact, TracePageOptions options, string? label = null)
    {
        var kind = Cell(label ?? artifact.Kind, MaxIdChars);

        // A video a hosted service keeps is its own link, wherever the run's files are.
        if (artifact.Url is not null)
        {
            // A link from outside text is a link only for http and https; anything else shows as the kind alone.
            return Uri.TryCreate(artifact.Url, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" ? Link(kind, artifact.Url) : kind;
        }

        var fromPage = artifact.Path is null ? null : PageHref(artifact.Path, options);
        var named = fromPage is null ? kind : Link(kind, fromPage);
        if (artifact.Path is null)
        {
            return named;
        }

        var shown = options.ArtifactsDir is null ? artifact.Path : options.ArtifactsDir + "/" + artifact.Path;
        return named + " " + Code(shown, MaxPathChars);
    }

    /// <summary>An artifact's path from the page's directory, when the page knows where it is.</summary>
    private static string? PageHref(string reportPath, TracePageOptions options) =>
        options.PageDir is null ? null : System.IO.Path.GetRelativePath(options.PageDir, reportPath).Replace('\\', '/');

    /// <summary>The words a result's status goes by on a page.</summary>
    private static string StatusWord(ResultStatus status) => status switch
    {
        ResultStatus.Passed => "passed",
        ResultStatus.Flaky => "flaky",
        ResultStatus.Failed => "failed",
        ResultStatus.TimedOut => "timed out",
        ResultStatus.Interrupted => "interrupted",
        _ => "skipped",
    };

    private static string AttemptStatusName(AttemptStatus status) => status switch
    {
        AttemptStatus.TimedOut => "timed-out",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string StepGlyph(StepStatus status) => status switch
    {
        StepStatus.Passed => "✓",
        StepStatus.Cancelled => "–",
        _ => "✗",
    };

    /// <summary>A step's hook as upstream spells it: <c>beforeEach</c>, <c>afterEach</c>, <c>body</c> for the test itself.</summary>
    private static string PhaseName(StepPhase? phase) => phase switch
    {
        StepPhase.BeforeEach => "beforeEach",
        StepPhase.AfterEach => "afterEach",
        _ => "body",
    };

    private static string EventKindName(StepEventKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Renders one traced result as its own markdown page: a failed or flaky one tells its failure, a passing one its steps.</summary>
    public static string RenderTracePage(Report1Document report, ReportResult result, Outcome final, TracePageOptions? options = null)
    {
        options ??= new TracePageOptions();
        var run = report.Run;
        var told = ToldAttempt(result, final);
        var manyTargets = run.Targets.Count > 1;
        var title = string.Join(" › ", result.TitlePath.Select(part => Cell(part, MaxTitleChars))) + ReportFormat.RepeatSuffix(result.Repeat);
        List<string> lines = ["# " + (result.Status == ResultStatus.Passed ? "✓" : "✗") + " " + title, ""];
        List<string> about =
        [
            Code(result.File, MaxPathChars),
            .. manyTargets ? new[] { "target " + Code(result.TargetId, MaxIdChars) } : [],
            .. string.Equals(result.Agent, "default", StringComparison.Ordinal) ? [] : new[] { "agent " + Code(result.Agent, MaxIdChars) },
            StatusWord(result.Status),
            FormatDuration(final.DurationMs),
        ];
        lines.Add(string.Join(" · ", about));
        lines.Add("");
        if (told.Environment is { Count: > 0 } ranOn)
        {
            lines.Add("Ran on: " + string.Join(" · ", ranOn.Select(pair => Cell(pair.Key, MaxIdChars) + " " + Code(pair.Value, MaxCellChars))));
            lines.Add("");
        }

        if (told.Error is { } error)
        {
            var phase = error.Phase is null or "body" ? "" : " in " + Cell(error.Phase, MaxIdChars);
            lines.AddRange(["**" + Cell(error.Code, 128) + "**" + phase, "", "```text", error.Message.Replace("```", "'''", StringComparison.Ordinal), "```", ""]);

            // An assertion's message already spells out expected and observed; the facts line would repeat the block above it.
            var spelled = error.Details is { Expected: { } expected, Observed: { } observed }
                && error.Message.Contains(expected, StringComparison.Ordinal)
                && error.Message.Contains(observed, StringComparison.Ordinal);
            if (!spelled)
            {
                lines.AddRange(DetailLines(error).Select(line => line + "  "));
            }
        }
        else if (result.Status != ResultStatus.Passed)
        {
            lines.Add("_" + StatusWord(result.Status) + ": no error was recorded._");
        }

        if (result.Status != ResultStatus.Passed)
        {
            var source = FailureSource(result, told);
            lines.Add("Look at: " + Code(source.File + ":" + source.Line.ToString(CultureInfo.InvariantCulture)) + "  ");
        }

        if (told.Status != AttemptStatus.Skipped && told.Skip is { } skip)
        {
            lines.Add("Also skipped: " + Cell(skip.Reason) + "  ");
        }

        if (AttemptsLine(result, final) is { } attempts)
        {
            lines.Add(attempts + "  ");
        }

        if (result.Status == ResultStatus.Flaky)
        {
            lines.Add("Passed after " + Plural(final.FailedAttempts, "failed attempt") + "; this page tells the last failure.  ");
        }

        if (lines[^1].Length > 0)
        {
            lines.Add("");
        }

        var at = FailedStepOf(told);
        if (told.Steps.Count > 0)
        {
            var videos = TimedVideos(told);
            lines.AddRange(["## Steps", ""]);
            for (var index = 0; index < told.Steps.Count; index++)
            {
                var step = told.Steps[index];
                var own = step.Source is { } location ? " — " + Code(location.File + ":" + location.Line.ToString(CultureInfo.InvariantCulture), MaxPathChars) : "";
                var calls = step.Metrics is null or { ModelCalls: 0 } ? "" : ", " + Plural(step.Metrics.ModelCalls, "model call");
                var failed = step.Status == StepStatus.Passed ? "" : " — **" + Cell(step.Error?.Code ?? StepNames.Of(step.Status), 128) + "**";
                var hook = step.Phase is { } phase ? " in " + PhaseName(phase) : "";
                var label = StepLabel(step, MaxCellChars);
                lines.Add(
                    (index + 1).ToString(CultureInfo.InvariantCulture) + ". " + StepGlyph(step.Status) + " " + Code(step.Api, MaxIdChars) + (label.Length == 0 ? "" : " " + label)
                    + " (" + FormatDuration(step.DurationMs) + calls + VideoOffset(step, videos) + hook + ")" + failed + own);
                if (step.Status != StepStatus.Passed && !string.IsNullOrWhiteSpace(step.Explanation))
                {
                    lines.Add("   > " + Cell(step.Explanation, MaxDetailChars));
                }

                lines.AddRange(StepDetailLines(step, told.AppLog, options.CacheDir).Select(line => "   - " + line));
                foreach (var artifact in told.Artifacts)
                {
                    if (string.Equals(artifact.Kind, "screenshot", StringComparison.Ordinal) && step.Artifacts.Contains(artifact.Id))
                    {
                        lines.Add("   - " + ArtifactPath(artifact, options));
                    }
                }

                lines.AddRange(StepScreenLines(step.Screen));
            }

            lines.Add("");
        }

        var appLog = AppLogLines(told.Steps, told.AppLog);
        if (appLog.Count > 0)
        {
            lines.AddRange(["## App log", "", "What the app logged, oldest first, with the step it happened in.", ""]);
            lines.AddRange(appLog.Select(line => "- " + line));
            lines.Add("");
        }

        if (at?.Step.Turns is { Count: > 0 } turns)
        {
            lines.Add("## Agent turns of step " + (at.Index + 1).ToString(CultureInfo.InvariantCulture));
            lines.Add("");
            lines.Add("The last " + Plural(turns.Count, "turn") + " the model took, oldest first: what it called and what came back.");
            lines.Add("");
            foreach (var turn in turns)
            {
                var calls = turn.Calls.Count == 0 ? "_no tool call_" : string.Join(", ", turn.Calls.Select(call => Code(call, MaxCellChars)));
                lines.Add("**Turn " + turn.Index.ToString(CultureInfo.InvariantCulture) + "** " + calls + "  ");
                lines.AddRange(TurnOutcomeLines(turn.Outcome).Select(line => "> " + Cell(line, MaxCellChars)));
                lines.Add("");
            }
        }

        if (told.Failure is { } failure)
        {
            AddScreenAtFailure(lines, told, failure, options);
        }

        if (told.SecondaryErrors.Count > 0)
        {
            lines.AddRange(["## Also failed", ""]);
            foreach (var secondary in told.SecondaryErrors)
            {
                var phase = secondary.Phase is null ? "" : " in " + Cell(secondary.Phase, MaxIdChars);
                lines.Add("- **" + Cell(secondary.Code, 128) + "**" + phase + ": " + Cell(secondary.Message, MaxDetailChars));
            }

            lines.Add("");
        }

        var evidence = EvidenceOf(told);
        if (evidence.Count > 0)
        {
            lines.AddRange(["## Evidence", ""]);
            lines.AddRange(evidence.Select(artifact => "- " + ArtifactPath(artifact, options, string.Equals(artifact.Id, told.Failure?.Screen, StringComparison.Ordinal) ? "screen" : artifact.Kind)));
            lines.Add("");
        }

        var footer = "e2e " + Cell(run.RunnerVersion, MaxIdChars) + " · run " + Code(run.Id, MaxIdChars);
        lines.Add("<sub>" + (options.Standalone ? footer : footer + " · the whole run is in `report.json`") + "</sub>");
        return string.Join('\n', lines) + "\n";
    }

    private static void AddScreenAtFailure(List<string> lines, ReportAttempt told, FailureEvidence failure, TracePageOptions options)
    {
        lines.AddRange(["## Screen at failure", ""]);
        var screen = failure.Screen is null ? null : told.Artifacts.FirstOrDefault(artifact => string.Equals(artifact.Id, failure.Screen, StringComparison.Ordinal));
        var listing = screen?.Path is null ? null : options.ReadArtifact?.Invoke(screen.Path);
        List<string> where =
        [
            .. failure.Url is null ? [] : new[] { Code(failure.Url, MaxPathChars) },
            .. failure.Viewport is null ? [] : new[] { "viewport " + failure.Viewport.Width.ToString(CultureInfo.InvariantCulture) + "x" + failure.Viewport.Height.ToString(CultureInfo.InvariantCulture) },
            .. failure.Nodes is null ? [] : new[] { Plural(failure.Nodes.Value, "node") },
        ];
        if (where.Count > 0)
        {
            lines.Add(string.Join(" · ", where) + "  ");
        }

        var shot = failure.Screenshot is null ? null : told.Artifacts.FirstOrDefault(artifact => string.Equals(artifact.Id, failure.Screenshot, StringComparison.Ordinal));
        if (shot?.Path is not null && PageHref(shot.Path, options) is { } shotHref)
        {
            lines.AddRange(["", "!" + Link("screenshot at failure", shotHref), ""]);
        }

        if (failure.Candidates is { Count: > 0 } candidates)
        {
            lines.Add("Closest to what the locator asked for:  ");
            lines.AddRange(candidates.Select(candidate => "- " + Code(candidate, MaxCellChars)));
        }

        if (listing is not null)
        {
            var body = listing.Length > MaxScreenChars ? listing[..MaxScreenChars] + "\n…[cut at " + MaxScreenChars.ToString(CultureInfo.InvariantCulture) + " characters; the file has the rest]" : listing;
            if (lines[^1].Length > 0)
            {
                lines.Add("");
            }

            lines.AddRange(["The screen as the agent reads it, one node per line: `#id role \"name\" text=\"…\" [states]`.", "", "```text", body.Replace("```", "'''", StringComparison.Ordinal).TrimEnd(), "```"]);
        }
        else if (screen is not null)
        {
            lines.Add(ArtifactPath(screen, options, "screen") + "  ");
        }

        lines.Add("");
    }
}
