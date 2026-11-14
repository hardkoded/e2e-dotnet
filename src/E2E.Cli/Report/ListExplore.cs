// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;
using E2E.Reports;

namespace E2E.Cli.Report;

/// <summary>
/// The list reporter's view of an <c>e2e explore</c> run, fed by its progress: the exploration steps by title as they finish, each
/// finding the moment it is reported, and at the end the record, a <c>Findings</c> section with the evidence, and the assessment.
/// An exploration is one synthetic test whose failure is its verdict, so the view stands in for the file block, the test line, and
/// the <c>Failed Tests</c> entry that test would get. <c>e2e explore</c> prints it without the live window and counts no actions in a step
/// line, since its output does not read the run's engine events; <see cref="ListReporter"/> feeds those to <see cref="Action"/> and paints
/// <see cref="LiveRows"/>.
/// </summary>
internal sealed class ExploreView
{
    /// <summary>A finding, in the CI log.</summary>
    private const string FindingGlyph = "⚑";

    /// <summary>Indentation of the exploration's rows under its header.</summary>
    private const string RowIndent = "   ";

    /// <summary>Indentation of the detail under a running exploration in the live window.</summary>
    private const string LiveIndent = "       ";

    /// <summary>Most recent calls of the step in progress the live window shows.</summary>
    private const int MaxEvents = 6;

    /// <summary>Rows the live window spends around the exploration: blank lines and the margin above the prompt.</summary>
    private const int ChromeRows = 6;

    /// <summary>Rows reserved for the exploration from the first paint, so the summary below does not jump.</summary>
    private const int ReservedRowsFloor = 14;

    /// <summary>The tool call a finding stands in for; its own event is not an action.</summary>
    private const string FindingToolEvent = "tool:report_finding";

    private const string DownRight = "↳";

    /// <summary>Width of the label gutter in a finding's block: four for the number, then <c>expected  </c>.</summary>
    private const int DetailLabelWidth = 8;

    private const string DetailIndent = "    ";

    private readonly Colors _pc;
    private readonly string _goal;
    private readonly ExploreBudgets _budgets;
    private readonly List<FinishedStep> _steps = [];
    private readonly List<ReportExploreFinding> _findings = [];
    private OpenStep? _open;
    private ExploreProgress.Planning? _planning;
    private ReportExploreEnded? _ended;
    private string? _summary;
    private readonly TimeProvider _time;
    private readonly long _startedMs;

    /// <summary>The exploration test's artifacts, once its result arrived: where a finding's screenshot is.</summary>
    private IReadOnlyList<(string Id, string? Path)> _artifacts = [];

    /// <summary>High-water mark of the live rows; the window pads up to it.</summary>
    private int _reservedRows = ReservedRowsFloor;

    /// <summary><paramref name="time"/> is the clock the live rows count the exploration's time by (upstream's <c>now</c>).</summary>
    public ExploreView(Colors pc, ExploreProgress.Started started, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(pc);
        ArgumentNullException.ThrowIfNull(started);
        _pc = pc;
        _goal = started.Goal;
        _budgets = started.Budgets;
        _time = time ?? TimeProvider.System;
        _startedMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
    }

    /// <summary>The step that finished last, with what the view counted while it ran.</summary>
    public FinishedStep? LastStep => _steps.Count == 0 ? null : _steps[^1];

    private IEnumerable<ReportExploreFinding> Issues => _findings.Where(finding => finding.Kind == ReportExploreFindingKind.Issue);

    /// <summary>Applies one moment of the exploration.</summary>
    public void Progress(ExploreProgress progress)
    {
        switch (progress)
        {
            case ExploreProgress.Planning planning:
                _planning = planning;
                break;
            case ExploreProgress.StepStarted started:
                _planning = null;
                _open = new OpenStep(started.Step);
                break;
            case ExploreProgress.StepFinished finished:
                _steps.Add(new FinishedStep(finished.Step, _open?.Actions ?? 0, _open?.Findings.Count ?? 0));
                _open = null;
                break;
            case ExploreProgress.FindingReported reported:
                _findings.Add(reported.Finding);
                _open?.Findings.Add(reported.Finding);
                break;
            case ExploreProgress.Finished finished:
                _planning = null;
                _ended = finished.Ended;
                _summary = finished.Summary;
                break;
        }
    }

    /// <summary>Counts one engine event of the step in progress as an action; the finding tool's own call is the finding.</summary>
    public void Action(Reports.StepEvent stepEvent)
    {
        ArgumentNullException.ThrowIfNull(stepEvent);
        if (_open is null || stepEvent.Kind != Reports.StepEventKind.Engine || stepEvent.Name == FindingToolEvent)
        {
            return;
        }

        _open.Actions++;
    }

    /// <summary>The exploration test's result arrived: keep its artifacts, so findings can name their screenshots.</summary>
    public void Result(IEnumerable<(string Id, string? Path)> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = [.. artifacts];
    }

    /// <summary>
    /// Whether the exploration's error is the verdict the findings already express: issues were found, so the run failed.
    /// Anything else (a provider failure, a run that explored nothing) is a failure worth its own entry.
    /// </summary>
    public bool IsVerdict(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return code == "ASSERTION_FAILED" && Issues.Any();
    }

    /// <summary>
    /// <c> ❯ |web| Exploring  goal</c>; the goal clipped to <paramref name="maxWidth"/> when given. Once over, the glyph is the verdict: a cross when
    /// issues were found or the exploration never concluded, a check otherwise.
    /// </summary>
    public string Header(string badge, ExploreVerb verb = ExploreVerb.Exploring, int? maxWidth = null)
    {
        var glyph = verb == ExploreVerb.Exploring
            ? _pc.Yellow(Format.Pointer)
            : _ended is null || Issues.Any() ? _pc.Red(Format.Cross) : _pc.Green(Format.Check);
        var head = " " + _pc.Bold(glyph) + " " + (badge.Length == 0 ? "" : badge + " ") + _pc.Bold(verb.ToString()) + "  ";
        var goal = TextRules.Collapse(_goal);
        return head + (maxWidth is not { } width ? Format.Bounded(goal) : Format.FitColumns(goal, Math.Max(24, width - Format.VisibleWidth(head))));
    }

    /// <summary>One finished step: glyph, number, title, then duration, actions, findings, and a non-passed status; its explanation under it.</summary>
    public IReadOnlyList<string> StepLines(FinishedStep finished, string indent = RowIndent)
    {
        var (step, actions, findings) = finished;
        var glyph = step.Status switch
        {
            ReportExploreStepStatus.Passed => _pc.Green(Format.Check),
            ReportExploreStepStatus.Failed => _pc.Red(Format.Cross),
            _ => _pc.Yellow(Format.Cross),
        };
        var tail = string.Join(
            " · ",
            new[] { Format.FormatTime(step.Duration) }.Concat(actions > 0 ? [Count(actions, "action")] : []).Concat(findings > 0 ? [Count(findings, "finding")] : []));
        var status = step.Status switch
        {
            ReportExploreStepStatus.Failed => "failed",
            ReportExploreStepStatus.Blocked => "blocked",
            ReportExploreStepStatus.Exhausted => "ended at its limit",
            _ => "",
        };
        List<string> lines =
        [
            indent + glyph + " " + _pc.Dim(step.Index.ToString(CultureInfo.InvariantCulture)) + "  " + Format.Bounded(step.Title) + " " + _pc.Dim(tail)
                + (status.Length == 0 ? "" : " " + _pc.Yellow(status)),
        ];

        // A step that did not pass explains itself; a passed step's summary is the planner's, not the reader's.
        if (step.Status != ReportExploreStepStatus.Passed && step.Summary is not null)
        {
            var room = Format.TerminalColumns() - LiveWindow.WidthMargin - indent.Length - 5;
            lines.Add(indent + "     " + _pc.Dim(Format.FitColumns(TextRules.Collapse(step.Summary), Math.Max(24, room))));
        }

        return lines;
    }

    /// <summary>One finding on one line: <c>⚑ high issue  Title (/path)</c>.</summary>
    public string FindingLine(ReportExploreFinding finding, string indent = RowIndent)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var location = finding.Path is null ? "" : _pc.Dim(" (" + Format.Bounded(finding.Path) + ")");
        return indent + SeverityColor(finding, FindingGlyph + " " + Grade(finding)) + "  " + Format.Bounded(TextRules.Collapse(finding.Title)) + location;
    }

    /// <summary>
    /// The live window's running area: the header with its clock, the finished steps, and the step in progress with its latest calls and findings in
    /// time order, or the planner deciding. Sized to the screen as the running tree is: the step in progress has priority, older finished steps fold.
    /// </summary>
    public IReadOnlyList<string> LiveRows(string badge, IReadOnlyList<Reports.StepEvent>? events, IReadOnlyList<string> summary, long now, int room)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var columns = Format.TerminalColumns() - LiveWindow.WidthMargin;
        var clock = _pc.Bold(_pc.Yellow(Format.FormatTime(TimeSpan.FromMilliseconds(Math.Max(0, now - _startedMs)))));
        List<string> lines = [Header(badge, ExploreVerb.Exploring, columns - Format.VisibleWidth(clock) - 1) + " " + clock];
        var capacity = Format.TerminalRows() - summary.Count - ChromeRows;
        var budget = capacity - 1;
        var current = CurrentRows(events, now, budget);
        budget -= current.Count;
        var finished = _steps.SelectMany(step => StepLines(step, LiveIndent)).ToList();
        if (budget > 0)
        {
            var folded = finished.Count - budget + 1;
            lines.AddRange(folded > 1 ? [LiveIndent + _pc.Dim($"… {folded} earlier rows"), .. finished.Skip(folded)] : finished);
        }

        lines.AddRange(current);
        _reservedRows = Math.Min(capacity, Math.Max(_reservedRows, lines.Count));
        RunningTree.PadToScreen(lines, _reservedRows, room - RunningTree.FrameRows - summary.Count);
        return ["", .. lines, "", .. summary, ""];
    }

    /// <summary>The step in progress, or the planner at work, at most <paramref name="budget"/> rows.</summary>
    private List<string> CurrentRows(IReadOnlyList<Reports.StepEvent>? events, long now, int budget)
    {
        if (_ended is not null || budget < 1)
        {
            return [];
        }

        if (_open is null)
        {
            if (_planning is not { } planning)
            {
                return [];
            }

            var what = planning.Closing ? "the closing assessment" : $"step {_steps.Count + 1} of {_budgets.MaxSteps}";
            return [.. new[] { LiveIndent + _pc.Dim($"{DownRight} planning {what}"), LiveIndent + "  " + RunningTree.WaitingRow(_pc, now, "Thinking") }.Take(budget)];
        }

        var step = _open.Step;
        List<string> rows = [LiveIndent + _pc.Dim($"{DownRight} {step.Index}  ") + Format.Bounded(step.Title) + " " + _pc.Dim($"(step {step.Index} of {_budgets.MaxSteps})")];
        var timeline = Timeline(events ?? []);
        var shown = Math.Min(MaxEvents, Math.Max(0, budget - 2));
        var overflow = timeline.Count - shown;
        if (overflow > 0)
        {
            rows.Add(LiveIndent + "  " + _pc.Dim($"… {overflow} earlier call{(overflow == 1 ? "" : "s")}"));
        }

        rows.AddRange((overflow > 0 ? timeline.Skip(overflow) : timeline).Select(row => LiveIndent + "  " + row));
        rows.Add(LiveIndent + "  " + RunningTree.WaitingRow(_pc, now, "Thinking"));
        return [.. rows.Take(budget)];
    }

    /// <summary>The step's calls and its findings, interleaved by time; the finding tool's own call gives way to the finding.</summary>
    private List<string> Timeline(IReadOnlyList<Reports.StepEvent> events)
    {
        var maxWidth = Format.TerminalColumns() - LiveWindow.WidthMargin - LiveIndent.Length - 2;
        return
        [
            .. events
                .Where(stepEvent => stepEvent.Name != FindingToolEvent)
                .Select(stepEvent => (At: stepEvent.StartedAt, Row: ListSteps.EventLine(_pc, stepEvent, maxWidth)))
                .Concat((_open?.Findings ?? []).Select(finding => (At: finding.ReportedAt, Row: FindingLine(finding, ""))))
                .OrderBy(entry => entry.At)
                .Select(entry => entry.Row),
        ];
    }

    /// <summary>The summary's <c>Findings</c> and <c>Steps</c> rows, in place of <c>Test Files</c> and <c>Tests</c>.</summary>
    public IReadOnlyList<string> SummaryRows()
    {
        var issues = Issues.Count();
        var warnings = _findings.Count - issues;
        var findings = _findings.Count == 0
            ? _pc.Dim("none")
            : string.Join(
                _pc.Dim(" | "),
                (issues > 0 ? [_pc.Bold(_pc.Red(Count(issues, "issue")))] : Enumerable.Empty<string>())
                    .Concat(warnings > 0 ? [_pc.Yellow(Count(warnings, "warning"))] : []));
        var max = _budgets.MaxSteps.ToString(CultureInfo.InvariantCulture);
        var done = _steps.Count.ToString(CultureInfo.InvariantCulture);
        List<string> steps = [];
        if (_ended is not { } ended)
        {
            steps.Add(done + " done of " + max);
            if (_open is not null)
            {
                steps.Add("step " + _open.Step.Index.ToString(CultureInfo.InvariantCulture) + " running");
            }
            else if (_planning is { } planning)
            {
                steps.Add(planning.Closing ? "closing" : "planning");
            }
        }
        else
        {
            steps.Add(done + " of " + max);
            steps.AddRange(OutcomeParts());
            steps.Add(_pc.Dim(ExploreText.EndedText(ended)));
        }

        return [Format.PadTitle(_pc, "Findings") + findings, Format.PadTitle(_pc, "Steps") + string.Join(_pc.Dim(" · "), steps)];
    }

    /// <summary>
    /// The permanent record once the run is over. With a live window (<paramref name="withSteps"/>) it was the steps' only home, so the header and
    /// every step print here; without one they streamed as they finished, and only why the exploration ended is left to say.
    /// </summary>
    public IReadOnlyList<string> Record(string badge, bool withSteps)
    {
        List<string> lines = withSteps ? ["", Header(badge, ExploreVerb.Explored), .. _steps.SelectMany(step => StepLines(step))] : [];
        if (_steps.Count == 0 && _ended != ReportExploreEnded.Skipped)
        {
            lines.Add(RowIndent + _pc.Dim("no step ran"));
        }

        lines.Add(RowIndent + _pc.Dim("ended: " + ExploreText.EndedText(_ended ?? ReportExploreEnded.Aborted)));
        return lines;
    }

    /// <summary>
    /// The <c>Findings</c> section: a banner, then one block per finding, issues before warnings and the most severe first, each
    /// with where it was seen, what was expected against what the screen showed, the actions that reach it, and its screenshot.
    /// </summary>
    public IReadOnlyList<string> FindingsSection(Func<string, string> displayPath, string? artifactsRoot)
    {
        if (_findings.Count == 0)
        {
            return [];
        }

        List<string> lines = ["", Banner("Findings " + _findings.Count.ToString(CultureInfo.InvariantCulture), Issues.Any() ? _pc.Red : _pc.Yellow), ""];
        var position = 0;
        foreach (var finding in ExploreText.OrderFindings(_findings))
        {
            position++;
            var number = position.ToString(CultureInfo.InvariantCulture).PadLeft(2) + ". ";
            lines.Add(number + SeverityColor(finding, Grade(finding).PadRight(16)) + " " + _pc.Bold(Format.Bounded(TextRules.Collapse(finding.Title))));
            List<string> where = [];
            if (finding.Path is not null)
            {
                where.Add(Format.Bounded(finding.Path));
            }

            if (finding.Step is { } step)
            {
                where.Add("step " + step.ToString(CultureInfo.InvariantCulture));
            }

            if (where.Count > 0)
            {
                lines.Add(DetailIndent + _pc.Dim(string.Join(" · ", where)));
            }

            lines.AddRange(Detail("expected", finding.Expected));
            lines.AddRange(Detail("actual", finding.Actual));
            for (var i = 0; i < finding.Reproduction.Count; i++)
            {
                lines.AddRange(Detail(i == 0 ? "steps" : "", (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + finding.Reproduction[i]));
            }

            if (Screenshot(finding, displayPath, artifactsRoot) is { } screenshot)
            {
                lines.Add(DetailIndent + _pc.Dim("evidence".PadRight(DetailLabelWidth)) + "  " + _pc.Cyan(Format.Bounded(screenshot)));
            }

            lines.Add("");
        }

        return lines;
    }

    /// <summary>The path of a finding's screenshot as the reporter shows paths; null when no artifact of the run has the finding's id.</summary>
    private string? Screenshot(ReportExploreFinding finding, Func<string, string> displayPath, string? artifactsRoot)
    {
        if (finding.ArtifactId is null || _artifacts.FirstOrDefault(artifact => artifact.Id == finding.ArtifactId).Path is not { } path)
        {
            return null;
        }

        return displayPath(artifactsRoot is null ? path : Path.Combine(artifactsRoot, path));
    }

    /// <summary>The agent's closing assessment, wrapped under its heading.</summary>
    public IReadOnlyList<string> Assessment()
    {
        if (_summary is null)
        {
            return [];
        }

        var width = Math.Max(24, Format.TerminalColumns() - RowIndent.Length - LiveWindow.WidthMargin);
        return [" " + _pc.Bold("Assessment"), .. Wrap(Format.Bounded(_summary), width).Select(line => RowIndent + line)];
    }

    /// <summary>`  1 issue`, `2 findings`.</summary>
    private static string Count(int count, string noun) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? "" : "s");

    /// <summary>`high issue`, `low warning`.</summary>
    private static string Grade(ReportExploreFinding finding) =>
        ExploreText.SeverityWord(finding.Severity) + " " + (finding.Kind == ReportExploreFindingKind.Warning ? "warning" : "issue");

    private string SeverityColor(ReportExploreFinding finding, string text)
    {
        if (finding.Kind == ReportExploreFindingKind.Warning)
        {
            return _pc.Dim(_pc.Yellow(text));
        }

        return finding.Severity switch
        {
            >= 4 => _pc.Bold(_pc.Red(text)),
            3 => _pc.Bold(_pc.Yellow(text)),
            _ => _pc.Yellow(text),
        };
    }

    /// <summary>`3 passed`, `1 failed`, ... over the finished steps, zero counts left out, colored by status.</summary>
    private IEnumerable<string> OutcomeParts() =>
        ExploreText.StepCountParts(_steps.Select(step => step.Step)).Select(part => part.Status switch
        {
            ReportExploreStepStatus.Passed => _pc.Green(part.Text),
            ReportExploreStepStatus.Failed => _pc.Red(part.Text),
            _ => _pc.Yellow(part.Text),
        });

    /// <summary>`    expected  text…`, wrapped to the width with continuation lines under the text.</summary>
    private IEnumerable<string> Detail(string label, string text)
    {
        var gutterWidth = DetailIndent.Length + DetailLabelWidth + 2;
        var blank = new string(' ', gutterWidth);
        return Wrap(TextRules.Collapse(Format.Bounded(text)), Math.Max(24, Format.TerminalColumns() - gutterWidth)).Select((line, index) =>
            (index == 0 ? DetailIndent + _pc.Dim(label.PadRight(DetailLabelWidth)) + "  " : blank) + line);
    }

    private string Banner(string message, Func<string, string> color)
    {
        var label = _pc.Bold(_pc.Inverse(" " + message + " "));
        var (before, after) = Format.Rule(label, RuleAlign.Center);
        return color(before) + color(label) + color(after);
    }

    /// <summary>Greedy word wrap; a word longer than the width stands on its own line.</summary>
    private static List<string> Wrap(string text, int width)
    {
        List<string> lines = [];
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length == 0)
                {
                    line = word;
                }
                else if (Format.VisibleWidth(line) + 1 + Format.VisibleWidth(word) <= width)
                {
                    line = line + " " + word;
                }
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>One finished exploration step with what the view counted while it ran (upstream's <c>FinishedStep</c>).</summary>
    internal sealed record FinishedStep(ReportExploreStep Step, int Actions, int Findings);

    private sealed class OpenStep(OpenedStep step)
    {
        public OpenedStep Step { get; } = step;

        public int Actions { get; set; }

        public List<ReportExploreFinding> Findings { get; } = [];
    }
}

/// <summary>The verb of an exploration's header (upstream's <c>'Exploring' | 'Explored'</c>).</summary>
internal enum ExploreVerb
{
    Exploring,
    Explored,
}
