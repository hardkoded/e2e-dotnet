// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Reports;

namespace E2E.Cli.Report;

/// <summary>Where the list reporter writes (upstream's <c>ListReporterOutput</c>). Not ported: <c>raw</c>, the control writes of the live window.</summary>
internal interface IListReporterOutput
{
    void Write(string line);
}

/// <summary>Options of a <see cref="ListReporter"/> (upstream's <c>ListReporterOptions</c>). Not ported: <c>live</c>, the port has no live window.</summary>
internal sealed record ListReporterOptions
{
    /// <summary>Emit ANSI colors; defaults to whether the console is a terminal and <c>NO_COLOR</c> is unset.</summary>
    public bool? Colors { get; init; }
}

/// <summary>
/// Renders the run's event stream as the CLI's human-readable output (upstream's <c>report/list.ts</c>). The reporter is one sink on
/// the run's single event spine, so it can only show what every other sink receives.
/// <para>
/// Ported: the run header, a file block per file with its test lines, the <c>Failed Tests</c> section with its evidence rows, the run
/// errors, and the summary rows (<c>Test Files</c>, <c>Tests</c>, <c>AI</c>, <c>Cache</c>, <c>Errors</c>,
/// <c>Start at</c>, <c>Duration</c>, <c>Report</c>, <c>Traces</c>, <c>Screenshots</c>, <c>AI trace</c>). Not ported: the live window, a
/// test's console output, setup steps, serial groups, step streaming, the exploration view, the <c>Repeats</c> row, the
/// <c>Skipped After Failure</c> section and its <c>Warnings</c> row, the video rows, and the code frame
/// under a failure.
/// </para>
/// </summary>
internal sealed class ListReporter
{
    /// <summary>Indentation under a badge line, matching vitest's banner padding.</summary>
    private const string BadgePadding = "      ";

    private const string FDown = "↓";

    private const string FRight = "→";

    /// <summary>Indentation of the <c>→ first error line</c> under a failed test in a file block.</summary>
    private const int ErrorGlanceIndent = 7;

    /// <summary>How many <c>app.screenshot</c> files the summary names before it counts the rest.</summary>
    private const int ShownScreenshots = 10;

    private readonly IListReporterOutput _output;
    private readonly Colors _pc;
    private readonly TimeProvider _time;
    private readonly string _separator;
    private string? _projectRoot;
    private string? _artifactsRoot;

    /// <summary>Selected targets in declaration order; decides each badge's color.</summary>
    private IReadOnlyList<string> _targets = [];

    /// <summary>When the run was launched (<c>run-started</c>): what a run that never reached <c>plan</c> reports.</summary>
    private DateTimeOffset _launchedAt;

    /// <summary>When the run began executing (<c>plan</c>): the summary's <c>Start at</c> and the origin of <c>Duration</c>.</summary>
    private DateTimeOffset? _startedAt;

    /// <summary>File groups keyed by target and file, in first-seen order.</summary>
    private readonly Dictionary<(string Target, string File), FileGroup> _groups = [];
    private readonly List<FileGroup> _groupOrder = [];
    private readonly List<Failure> _failures = [];

    /// <summary>Run-level errors, printed once at the end.</summary>
    private readonly List<ReportError> _errors = [];

    private bool _interrupted;
    private readonly Format.AiUsage _runUsage = Format.EmptyUsage();
    private readonly Dictionary<string, int> _runModels = new(StringComparer.Ordinal);
    private readonly Format.CacheTally _runCache = Format.EmptyCacheTally();

    /// <summary>Report-relative paths of every <c>app.screenshot</c> file, in result order, for the <c>Screenshots</c> row.</summary>
    private readonly List<string> _screenshots = [];

    /// <summary>The configured models as the header names them; the summary's <c>AI</c> row names the models the steps reported instead.</summary>
    private string? _models;

    public ListReporter(IListReporterOutput? output = null, ListReporterOptions? options = null, TimeProvider? timeProvider = null)
    {
        _output = output ?? DefaultOutput;
        _time = timeProvider ?? TimeProvider.System;
        _pc = new Colors(options?.Colors ?? (!Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))));
        _separator = _pc.Dim(" > ");
        _launchedAt = _time.GetLocalNow();
    }

    /// <summary>The reporter as an event sink: one dispatch per fact (upstream's <c>handle</c>).</summary>
    public void Handle(RunEventFact fact)
    {
        switch (fact)
        {
            case RunStartedEvent started:
                RunStarted(started);
                break;
            case PlanEvent plan:
                Plan(plan);
                break;
            case NoticeEvent notice:
                Print(_pc.Dim("ℹ") + " " + Format.Bounded(notice.Message));
                break;
            case TestFinishedEvent finished:
                TestFinished(finished.Result);
                break;
            case RunErrorEvent error:
                _errors.Add(error.Error);
                break;
            case RunInterruptedEvent interrupted:
                RunInterrupted(interrupted);
                break;
            case RunStoppedEvent stopped:
                Print(_pc.Yellow(
                    $"stopped after {stopped.Failures} {(stopped.Failures == 1 ? "failure" : "failures")} (--max-failures {stopped.Limit}): the running tests end as interrupted, the rest are skipped"));
                break;
            case RunFinishedEvent finished:
                RunFinished(finished);
                break;
        }
    }

    /// <summary>Prints the rows reporters resolved with under the summary, in the summary's layout.</summary>
    public void Rows(IReadOnlyList<(string Label, string Text)> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var (label, text) in rows)
        {
            Print(Format.PadTitle(_pc, Format.Bounded(label)) + Format.Bounded(text));
        }

        Print(string.Empty);
    }

    private void Print(string line) => _output.Write(line);

    private string TitledAs(string title, string agent, int repeat)
    {
        var bare = Format.Bounded(title) + (repeat == 0 ? string.Empty : _pc.Dim(Format.RepeatSuffix(repeat)));
        return agent == "default" ? bare : bare + " " + _pc.Dim($"[{Format.Bounded(agent)}]");
    }

    /// <summary>A target's colored badge, <c>|web|</c> when colors are off (vitest's convention).</summary>
    private string Badge(string target)
    {
        var name = Format.Bounded(target);
        if (!_pc.Enabled)
        {
            return $"|{name}|";
        }

        var index = Math.Max(0, IndexOfTarget(target));
        return _pc.Black(_pc.BgBright(index, $" {name} "));
    }

    private int IndexOfTarget(string target)
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            if (_targets[i] == target)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A path relative to the project root when inside it, else as given.</summary>
    private string DisplayPath(string target)
    {
        var inside = _projectRoot is not null && target.StartsWith(_projectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return Format.Bounded(inside ? Path.GetRelativePath(_projectRoot!, target) : target);
    }

    private FileGroup Group(string file, string target)
    {
        if (!_groups.TryGetValue((target, file), out var group))
        {
            group = new FileGroup(file, target);
            _groups[(target, file)] = group;
            _groupOrder.Add(group);
        }

        return group;
    }

    private void RunStarted(RunStartedEvent started)
    {
        _projectRoot = started.ProjectRoot;
        _artifactsRoot = started.ArtifactsRoot;
        _targets = started.Targets;
        _launchedAt = _time.GetLocalNow();
        var version = typeof(ListReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
        Print(string.Empty);
        Print($"{_pc.Bold(_pc.Black(_pc.Cyan(" RUN ")))} {_pc.Cyan($"e2e v{version}")} {_pc.Gray(started.ProjectRoot)}");
        List<string> details = [$"run {started.RunId}", $"targets: {string.Join(", ", started.Targets)}"];
        if (started.Agents is { } agents)
        {
            details.Add($"{(agents.Count == 1 ? "agent" : "agents")}: {string.Join(", ", agents.Select(Format.Bounded))}");
        }

        if (started.Ci)
        {
            details.Add("CI");
        }

        Print(BadgePadding + _pc.Dim(string.Join(" · ", details)));
        if (started.Model is { } model)
        {
            _models = Format.Bounded(model);
            if (started.Judge is { } judge)
            {
                _models += " · judge " + Format.Bounded(judge);
            }

            Print(BadgePadding + _pc.Dim("model " + _models));
        }

        Print(string.Empty);
    }

    /// <summary>
    /// The plan is settled once collection and every target's setup are done, so its arrival is when the run starts executing and the clock
    /// starts: a browser downloaded moments earlier is not on it.
    /// </summary>
    private void Plan(PlanEvent plan)
    {
        _startedAt = _time.GetLocalNow();
        foreach (var planned in plan.Files)
        {
            Group(planned.File, planned.Target).Planned = planned.Tests;
        }
    }

    /// <summary>Acknowledged immediately, so a bounded teardown is not mistaken for a hang.</summary>
    private void RunInterrupted(RunInterruptedEvent interrupted)
    {
        _interrupted = true;
        Print(_pc.Yellow(
            interrupted.Mode == InterruptMode.Forced
                ? "interrupted again: tearing every worker down now"
                : "interrupted: stopping the running test and tearing down (interrupt again to force)"));
    }

    /// <summary>The details of an ordinary result: summed over its attempts, the error from the told one.</summary>
    private static ResultDetails DetailsOf(IReadOnlyList<ReportAttempt> attempts)
    {
        var steps = attempts.Select(attempt => (IEnumerable<StepRecord>)attempt.Steps).ToList();

        // A retry the run interrupted tells nothing; the failure before it is the verdict.
        var told = attempts.LastOrDefault(attempt => attempt.Status != E2E.Internal.AttemptStatus.Interrupted) ?? attempts.LastOrDefault();
        var screen = told?.Failure?.Screen is { } screenId ? told.Artifacts.FirstOrDefault(artifact => artifact.Id == screenId)?.Path : null;
        return new ResultDetails
        {
            DurationMs = attempts.Sum(attempt => attempt.DurationMs),
            Usage = Format.StepsUsage(steps),
            Models = Format.StepsModelTally(steps),
            Cache = Format.StepsCacheTally(steps),
            Error = told?.Error,
            Screenshots = [.. attempts.SelectMany(attempt => RequestedScreenshots(attempt.Steps, attempt.Artifacts))],
            Failure = told?.Failure,
            ScreenPath = screen,
            AppLog = AppLogSummaryOf(told?.AppLog ?? []),
            SkipReason = told?.Status == E2E.Internal.AttemptStatus.Skipped ? null : told?.Skip?.Reason,
        };
    }

    /// <summary>Report-relative paths of the screenshots the <c>app.screenshot</c> steps among <paramref name="steps"/> attached, in step order (upstream's <c>requestedScreenshots</c>).</summary>
    private static IEnumerable<string> RequestedScreenshots(IReadOnlyList<StepRecord> steps, IReadOnlyList<ReportArtifact> artifacts) =>
        steps.Where(step => step.Api == "app.screenshot")
            .SelectMany(step => step.Artifacts)
            .Select(id => artifacts.FirstOrDefault(artifact => artifact.Id == id && artifact.Kind == "screenshot")?.Path)
            .OfType<string>();

    /// <summary>What went wrong in the app log, or null when nothing did (upstream's <c>appLogSummary</c>).</summary>
    private static AppLogSummary? AppLogSummaryOf(IReadOnlyList<AppLogRecord> appLog)
    {
        var appErrors = FailureText.AppErrorsOf(appLog);
        var errors = appErrors.Count;
        var warnings = appLog.Count(entry => entry.Level == AppLogLevel.Warning);
        List<string> parts = [];
        if (errors > 0)
        {
            parts.Add($"{errors} {(errors == 1 ? "error" : "errors")}");
        }

        if (warnings > 0)
        {
            parts.Add($"{warnings} {(warnings == 1 ? "warning" : "warnings")}");
        }

        return parts.Count == 0 ? null : new AppLogSummary(string.Join(", ", parts), appErrors);
    }

    private void TestFinished(RunEventResult result)
    {
        // Unselected pairs are report-only: they never print and the plan never counted them.
        if (!result.Selected)
        {
            return;
        }

        var group = Group(result.Test.File, result.Target.Name);
        var details = DetailsOf(result.Attempts);
        _screenshots.AddRange(details.Screenshots);
        Format.AddUsage(_runUsage, details.Usage);
        Format.AddModelTally(_runModels, details.Models);
        Format.AddCacheTally(_runCache, details.Cache);
        var title = TitledAs(string.Join(" > ", result.Test.TitlePath), result.Agent, result.Repeat);
        group.Lines.Add(new TestLine
        {
            Title = title,
            DeclarationIndex = result.Test.DeclarationIndex,
            Status = result.Status,
            DurationMs = details.DurationMs,
            Usage = details.Usage,
            FirstErrorLine = details.Error is null ? null : Format.Bounded(details.Error.Message).Split('\n')[0],
            SkipReason = result.Skip is null ? null : Format.Bounded(result.Skip.Reason),
        });
        if (Format.StatusBucket(result.Status) == "failed")
        {
            _failures.Add(new Failure(result.ResultId, group, title, result.Status, details.Error, details.Failure, details.ScreenPath, details.AppLog, details.SkipReason));
        }

        if (group.Planned is { } planned && group.Lines.Count >= planned)
        {
            PrintGroup(group);
        }
    }

    /// <summary>Counters over every reported result; the total is the plan's once it has arrived.</summary>
    private Format.Counters TestCounters()
    {
        var counters = Format.Tally(_groupOrder.SelectMany(group => group.Lines).Select(line => line.Status));
        counters.Total = _groupOrder.Sum(group => group.Planned ?? group.Lines.Count);
        return counters;
    }

    /// <summary>Counters over test files; a file lands in an outcome once its block has printed.</summary>
    private Format.Counters FileCounters()
    {
        var counters = Format.EmptyCounters();
        foreach (var group in _groupOrder)
        {
            counters.Total++;
            if (group.Printed)
            {
                counters.Add(Format.FileOutcome(Format.Tally(group.Lines.Select(line => line.Status))));
            }
        }

        return counters;
    }

    /// <summary>
    /// Prints one file's block: the file line with its counts, then, when the file failed, was interrupted, had a flaky pass, or is the
    /// run's only file, one line per test.
    /// </summary>
    private void PrintGroup(FileGroup group)
    {
        if (group.Printed)
        {
            return;
        }

        group.Printed = true;
        var counts = Format.Tally(group.Lines.Select(line => line.Status));
        var outcome = Format.FileOutcome(counts);
        var symbol = outcome switch
        {
            "failed" => _pc.Red(Format.Pointer),
            "interrupted" => _pc.Yellow(Format.Pointer),
            "skipped" => _pc.Dim(_pc.Gray(FDown)),
            _ => _pc.Green(Format.Check),
        };
        List<string> stateParts = [_pc.Dim($"{counts.Total} test{(counts.Total == 1 ? string.Empty : "s")}")];
        if (counts.Failed > 0)
        {
            stateParts.Add(_pc.Red($"{counts.Failed} failed"));
        }

        if (counts.Interrupted > 0)
        {
            stateParts.Add(_pc.Yellow($"{counts.Interrupted} interrupted"));
        }

        if (counts.Flaky > 0)
        {
            stateParts.Add(_pc.Yellow($"{counts.Flaky} flaky"));
        }

        if (counts.Skipped > 0)
        {
            stateParts.Add(_pc.Yellow($"{counts.Skipped} skipped"));
        }

        var durationMs = group.Lines.Sum(line => line.DurationMs);
        var ai = Format.AiSegment(Format.SumUsage(group.Lines.Select(line => line.Usage)));
        List<string> parts =
        [
            $" {symbol} {Badge(group.Target)} {Format.Bounded(group.File)}",
            $"{_pc.Dim("(")}{string.Join(_pc.Dim(" | "), stateParts)}{_pc.Dim(")")}",
        ];

        // A file whose tests all skipped never ran; `0ms` would only invite a question.
        if (durationMs > 0)
        {
            parts.Add(_pc.Dim(Format.FormatTime(TimeSpan.FromMilliseconds(durationMs))));
        }

        if (ai is not null)
        {
            parts.Add(_pc.Dim(ai));
        }

        Print(string.Join(" ", parts));
        var verbose = counts.Failed > 0 || counts.Interrupted > 0 || counts.Flaky > 0 || _groups.Count == 1;
        if (!verbose)
        {
            return;
        }

        foreach (var line in group.Lines.OrderBy(line => line.DeclarationIndex))
        {
            foreach (var rendered in TestLineRows(line))
            {
                Print(rendered);
            }
        }
    }

    /// <summary>One test row under its file line, plus the first error line for a failure.</summary>
    private List<string> TestLineRows(TestLine line)
    {
        var duration = _pc.Dim(Format.FormatTime(TimeSpan.FromMilliseconds(line.DurationMs)));
        var segment = Format.AiSegment(line.Usage);
        var ai = segment is null ? string.Empty : " " + _pc.Dim(segment);
        switch (line.Status)
        {
            case ResultStatus.Passed:
                return [$"   {_pc.Green(Format.Check)} {line.Title} {duration}{ai}"];
            case ResultStatus.Flaky:
                return [$"   {_pc.Yellow(Format.Check)} {line.Title} {_pc.Yellow("(flaky)")} {duration}{ai}"];
            case ResultStatus.Skipped:
            {
                // `test.skip` without a message records the bare word; repeating it adds nothing.
                var reason = line.SkipReason is null or "skipped" ? string.Empty : _pc.Dim(_pc.Gray($" [{line.SkipReason}]"));
                return [$"   {_pc.Dim(_pc.Gray(FDown))} {line.Title}{reason}"];
            }

            case ResultStatus.Interrupted:
                return [$"   {_pc.Yellow(Format.Cross)} {line.Title} {_pc.Yellow("(interrupted)")} {duration}{ai}"];
            default:
            {
                var status = line.Status == ResultStatus.Failed ? string.Empty : _pc.Red($" ({StatusName(line.Status)})");
                List<string> rows = [$"   {_pc.Red($"{Format.Cross} {line.Title}")}{status} {duration}{ai}"];
                if (line.FirstErrorLine is { } first)
                {
                    // A glance line; the `Failed Tests` section carries the whole message.
                    var glance = Format.FitColumns(first, Format.TerminalColumns() - ErrorGlanceIndent);
                    rows.Add($"     {_pc.Red($"{FRight} {glance}")}");
                }

                return rows;
            }
        }
    }

    /// <summary>A result status as the report spells it: <c>timed-out</c> for a timeout.</summary>
    private static string StatusName(ResultStatus status) => status == ResultStatus.TimedOut ? "timed-out" : status.ToString().ToLowerInvariant();

    /// <summary>
    /// vitest's padded summary: files, tests, model usage, the trace cache's part, run errors, start time, and
    /// elapsed time. The final summary of a run that never reached <c>plan</c> (a config or collection failure) counts from the launch,
    /// so the time it took to fail is still on record.
    /// </summary>
    private List<string> SummaryRows()
    {
        var files = FileCounters();
        var tests = TestCounters();
        List<string> rows =
        [
            Format.PadTitle(_pc, "Test Files") + (files.Total == 0 ? EmptyState("no test files", "none started") : Format.StateString(_pc, files)),
            Format.PadTitle(_pc, "Tests") + (tests.Total == 0 ? EmptyState("no tests executed", "none executed") : Format.StateString(_pc, tests)),
        ];
        if (Format.UsageText(_runUsage) is { } ai)
        {
            var models = Format.ModelsText(_runModels) ?? _models;
            rows.Add(Format.PadTitle(_pc, "AI") + $"{ai} · {_runUsage.Calls} model calls{(models is null ? string.Empty : " · " + models)}");
        }

        if (Format.CacheText(_pc, _runCache) is { } cache)
        {
            rows.Add(Format.PadTitle(_pc, "Cache") + cache);
        }

        if (_errors.Count > 0)
        {
            rows.Add(Format.PadTitle(_pc, "Errors") + _pc.Bold(_pc.Red($"{_errors.Count} error{(_errors.Count == 1 ? string.Empty : "s")}")));
        }

        var startedAt = _startedAt ?? _launchedAt;
        rows.Add(Format.PadTitle(_pc, "Start at") + Format.FormatClock(startedAt));
        rows.Add(Format.PadTitle(_pc, "Duration") + Format.DurationText(_pc, _time.GetLocalNow() - startedAt, TimeSpan.Zero));
        return rows;
    }

    /// <summary>A zero counter's label: the plain text, or the interrupt when that is why nothing is counted.</summary>
    private string EmptyState(string none, string cut) => _interrupted ? _pc.Yellow($"{cut} (interrupted)") : _pc.Dim(none);

    /// <summary>vitest's <c>Failed Tests</c> section: a banner, then each failure with its evidence.</summary>
    private void PrintFailures(string heading, IReadOnlyList<Failure> failures, IReadOnlyDictionary<string, string> pages)
    {
        if (failures.Count == 0)
        {
            return;
        }

        Print(string.Empty);
        Print(ErrorBanner($"{heading} {failures.Count}"));
        Print(string.Empty);
        for (var index = 0; index < failures.Count; index++)
        {
            var failure = failures[index];
            Print($"{_pc.Bold(_pc.BgRed(failure.Status == ResultStatus.Skipped ? " SKIP " : " FAIL "))} {Badge(failure.Group.Target)} {Format.Bounded(failure.Group.File)}{_separator}{failure.Title}");
            if (failure.Error is null)
            {
                Print(_pc.Red($"{_pc.Bold(StatusName(failure.Status))}: no error was recorded"));
            }
            else
            {
                var lines = Format.Bounded(failure.Error.Message).Split('\n');
                Print(_pc.Red($"{_pc.Bold(Format.Bounded(failure.Error.Code))}: {lines[0]}"));
                foreach (var line in lines.Skip(1))
                {
                    Print(_pc.Red(line));
                }
            }

            if (failure.SkipReason is { } skipReason)
            {
                Print(_pc.Yellow($" {_pc.Dim(Format.Pointer)} {_pc.Dim("skipped")} {Format.Bounded(skipReason)}"));
            }

            PrintEvidence(failure.Evidence, failure.ScreenPath, pages.GetValueOrDefault(failure.Id), failure.AppLog);
            var marker = $"[{index + 1}/{failures.Count}]";
            var (before, after) = Format.Rule(marker, RuleAlign.Right);
            Print(string.Empty);
            Print(_pc.Red(_pc.Dim($"{before}{marker}{after}")));
            Print(string.Empty);
        }
    }

    /// <summary>A full-width red rule with a centered <c>FAIL</c>-style label.</summary>
    private string ErrorBanner(string message)
    {
        var label = _pc.Bold(_pc.BgRed($" {message} "));
        var (before, after) = Format.Rule(label, RuleAlign.Center);
        return $"{_pc.Red(before)}{label}{_pc.Red(after)}";
    }

    /// <summary>
    /// What the runner saw when the failure landed: the location, the nodes closest to what a failed locator asked for, and where the
    /// trace page is (else the screen text).
    /// </summary>
    private void PrintEvidence(FailureEvidence? failure, string? screenPath, string? page, AppLogSummary? appLog)
    {
        void Row(string label, string text) => Print(_pc.Cyan($" {_pc.Dim(Format.Pointer)} {_pc.Dim(label)} {text}"));
        if (failure?.Url is { } url)
        {
            Row("at", Format.Bounded(url));
        }

        foreach (var candidate in failure?.Candidates ?? [])
        {
            Row("on screen", Format.Bounded(candidate));
        }

        if (appLog is not null)
        {
            Row("app", $"{appLog.Tally} logged");
            foreach (var (label, text) in appLog.Errors.Shown)
            {
                Print($"   {_pc.Red($"{Format.Cross} {label}:")} {Format.Bounded(text)}");
            }

            if (appLog.Errors.More is { } more)
            {
                Print(_pc.Dim($"   {more}"));
            }
        }

        // The page tells the screen too, and every step before it.
        if (page is not null)
        {
            Row("trace", Format.Bounded(page));
        }
        else if (screenPath is not null)
        {
            Row("screen", DisplayPath(_artifactsRoot is null ? screenPath : Path.Combine(_artifactsRoot, screenPath)));
        }
    }

    /// <summary>
    /// Run-level errors never arrive as results: they abort before, between, or after test execution. Without this the console shows
    /// only a bare exit code and the reason lives solely in report.json.
    /// </summary>
    private void PrintErrors()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        Print(string.Empty);
        Print(ErrorBanner($"Run Errors {_errors.Count}"));
        Print(string.Empty);
        foreach (var error in _errors)
        {
            var phase = error.Phase is null ? string.Empty : _pc.Dim($" ({Format.Bounded(error.Phase)})");
            Print($"{_pc.Bold(_pc.BgRed(" ERROR "))} {Format.Bounded(error.Category)} error {_pc.Dim(Format.Bounded(error.Code))}{phase}");
            foreach (var line in Format.Bounded(error.Message).Split('\n'))
            {
                Print(_pc.Red(line));
            }

            Print(string.Empty);
        }
    }

    private void RunFinished(RunFinishedEvent finished)
    {
        if (finished.Status == ReportRunStatus.Interrupted)
        {
            _interrupted = true;
        }

        // An interrupted or crashed run leaves files without their full result set; print what they have so nothing that ran goes unreported.
        foreach (var group in _groupOrder.Where(group => !group.Printed && group.Lines.Count > 0).ToList())
        {
            PrintGroup(group);
        }

        var pages = finished.Traces ?? new Dictionary<string, string>();
        PrintFailures("Failed Tests", _failures, pages);
        PrintErrors();
        Print(string.Empty);
        foreach (var row in SummaryRows())
        {
            Print(row);
        }

        // The report is for scripts, so a run names it only when it is missing.
        if (finished.ReportPath is null)
        {
            Print(Format.PadTitle(_pc, "Report") + _pc.Dim("(not written)"));
        }

        if (pages.Count > 0)
        {
            // A page is `<results>/<test>/trace.md`; the row names the results directory.
            var page = pages.Values.First().Replace('\\', '/');
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(page) ?? string.Empty)?.Replace('\\', '/') ?? string.Empty;
            Print(Format.PadTitle(_pc, "Traces") + $"{Format.Bounded(dir)}/ {_pc.Dim($"({pages.Count} {(pages.Count == 1 ? "page" : "pages")}, trace.md in each test's directory)")}");
        }

        PrintScreenshots();
        if (finished.AiTracePath is { } aiTrace)
        {
            var shown = DisplayPath(aiTrace);
            Print(Format.PadTitle(_pc, "AI trace") + $"{shown} {_pc.Dim($"(open with: npx unbox-ai {shown})")}");
        }

        Print(string.Empty);
    }

    /// <summary>The <c>Screenshots</c> row: each <c>app.screenshot</c> file, so a caller never has to search the results directory.</summary>
    private void PrintScreenshots()
    {
        var shown = _screenshots.Take(ShownScreenshots).Select(relative => DisplayPath(_artifactsRoot is null ? relative : Path.Combine(_artifactsRoot, relative))).ToList();
        for (var index = 0; index < shown.Count; index++)
        {
            Print(Format.PadTitle(_pc, index == 0 ? "Screenshots" : string.Empty) + shown[index]);
        }

        var rest = _screenshots.Count - shown.Count;
        if (rest > 0)
        {
            Print(Format.PadTitle(_pc, string.Empty) + _pc.Dim($"and {rest} more (the report lists every artifact)"));
        }
    }

    /// <summary>What a test line and a failure entry need from a result's execution.</summary>
    private sealed record ResultDetails
    {
        public required long DurationMs { get; init; }

        public required Format.AiUsage Usage { get; init; }

        /// <summary>The models its steps reported, with their call counts.</summary>
        public required Dictionary<string, int> Models { get; init; }

        public required Format.CacheTally Cache { get; init; }

        public ReportError? Error { get; init; }

        /// <summary>Report-relative paths of the screenshots <c>app.screenshot</c> took, in attempt order.</summary>
        public required IReadOnlyList<string> Screenshots { get; init; }

        /// <summary>What the runner saw when the last failure landed.</summary>
        public FailureEvidence? Failure { get; init; }

        /// <summary>The report path of the screen text the failure points at.</summary>
        public string? ScreenPath { get; init; }

        /// <summary>What the app logged during the told attempt that went wrong.</summary>
        public AppLogSummary? AppLog { get; init; }

        /// <summary>Why the told attempt skipped itself, when it failed anyway.</summary>
        public string? SkipReason { get; init; }
    }

    /// <summary>The app log lines that say something went wrong: the tally, and the errors a failure block quotes (upstream's <c>AppLogSummary</c>).</summary>
    /// <param name="Tally"><c>2 errors, 1 warning</c>.</param>
    /// <param name="Errors">The errors the block quotes.</param>
    private sealed record AppLogSummary(string Tally, FailureText.AppErrors Errors);

    /// <summary>One failed pair, held for the <c>Failed Tests</c> section.</summary>
    private sealed record Failure(string Id, FileGroup Group, string Title, ResultStatus Status, ReportError? Error, FailureEvidence? Evidence, string? ScreenPath, AppLogSummary? AppLog, string? SkipReason);

    /// <summary>Upstream's <c>DEFAULT_OUTPUT</c>: a line to stdout. A C# interface needs a class to implement it, hence <see cref="StdoutOutput"/>.</summary>
    private static readonly IListReporterOutput DefaultOutput = new StdoutOutput();

    private sealed class StdoutOutput : IListReporterOutput
    {
        public void Write(string line) => Console.Out.WriteLine(line);
    }
}
