// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;

namespace E2E.Cli.Report;

/// <summary>
/// The list reporter's view of an <c>e2e explore</c> run, fed by its progress: the exploration steps by title as they finish, each
/// finding the moment it is reported, and at the end the record, a <c>Findings</c> section with the evidence, and the assessment.
/// An exploration is one synthetic test whose failure is its verdict, so the view stands in for the file block, the test line, and
/// the <c>Failed Tests</c> entry that test would get. The port has no live window, so the view has no running rows; and no engine
/// events, so a step line counts no actions.
/// </summary>
internal sealed class ExploreView
{
    /// <summary>A finding, in the CI log.</summary>
    private const string FindingGlyph = "⚑";

    /// <summary>Indentation of the exploration's rows under its header.</summary>
    private const string RowIndent = "   ";

    /// <summary>Upstream's <c>WIDTH_MARGIN</c>: columns left free at the right edge.</summary>
    private const int WidthMargin = 2;

    /// <summary>Width of the label gutter in a finding's block: four for the number, then <c>expected  </c>.</summary>
    private const int DetailLabelWidth = 8;

    private const string DetailIndent = "    ";

    private readonly Colors _pc;
    private readonly int _columns;
    private readonly string _goal;
    private readonly ExploreBudgets _budgets;
    private readonly List<FinishedStep> _steps = [];
    private readonly List<ReportExploreFinding> _findings = [];
    private OpenStep? _open;
    private ExploreProgress.Planning? _planning;
    private ReportExploreEnded? _ended;
    private string? _summary;

    public ExploreView(Colors pc, ExploreProgress.Started started, int columns)
    {
        ArgumentNullException.ThrowIfNull(pc);
        ArgumentNullException.ThrowIfNull(started);
        _pc = pc;
        _columns = columns;
        _goal = started.Goal;
        _budgets = started.Budgets;
    }

    /// <summary>The step that finished last, with what the view counted while it ran.</summary>
    public (ReportExploreStep Step, int Findings)? LastStep => _steps.Count == 0 ? null : (_steps[^1].Step, _steps[^1].Findings);

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
                _steps.Add(new FinishedStep(finished.Step, _open?.Findings ?? 0));
                _open = null;
                break;
            case ExploreProgress.FindingReported reported:
                _findings.Add(reported.Finding);
                _open?.AddFinding();
                break;
            case ExploreProgress.Finished finished:
                _planning = null;
                _ended = finished.Ended;
                _summary = finished.Summary;
                break;
        }
    }

    /// <summary>
    /// Whether the exploration's error is the verdict the findings already express: issues were found, so the run failed.
    /// Anything else (a provider failure, a run that explored nothing) is a failure worth its own entry.
    /// </summary>
    public bool IsVerdict(E2EException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code == "ASSERTION_FAILED" && Issues.Any();
    }

    /// <summary>` ❯ |web| Exploring  goal`.</summary>
    public string Header(string badge) =>
        " " + _pc.Bold(_pc.Yellow(Format.Pointer)) + " " + badge + " " + _pc.Bold("Exploring") + "  " + Format.Bounded(TextRules.Collapse(_goal));

    /// <summary>One finished step: glyph, number, title, then duration, findings, and a non-passed status; its explanation under it.</summary>
    public IReadOnlyList<string> StepLines((ReportExploreStep Step, int Findings) finished, string indent = RowIndent)
    {
        var (step, findings) = finished;
        var glyph = step.Status switch
        {
            ReportExploreStepStatus.Passed => _pc.Green(Format.Check),
            ReportExploreStepStatus.Failed => _pc.Red(Format.Cross),
            _ => _pc.Yellow(Format.Cross),
        };
        var tail = string.Join(" · ", new[] { Format.FormatTime(step.Duration) }.Concat(findings > 0 ? [Count(findings, "finding")] : []));
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
            var room = _columns - WidthMargin - indent.Length - 5;
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
    /// The permanent record once the run is over. The steps streamed as they finished, so only why the exploration ended is left
    /// to say.
    /// </summary>
    public IReadOnlyList<string> Record()
    {
        List<string> lines = [];
        if (_steps.Count == 0)
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
    public IReadOnlyList<string> FindingsSection()
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

            if (finding.ArtifactId is not null)
            {
                lines.Add(DetailIndent + _pc.Dim("evidence".PadRight(DetailLabelWidth)) + "  " + _pc.Cyan(Format.Bounded(finding.ArtifactId)));
            }

            lines.Add("");
        }

        return lines;
    }

    /// <summary>The agent's closing assessment, wrapped under its heading.</summary>
    public IReadOnlyList<string> Assessment()
    {
        if (_summary is null)
        {
            return [];
        }

        var width = Math.Max(24, _columns - RowIndent.Length - WidthMargin);
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
        return Wrap(TextRules.Collapse(Format.Bounded(text)), Math.Max(24, _columns - gutterWidth)).Select((line, index) =>
            (index == 0 ? DetailIndent + _pc.Dim(label.PadRight(DetailLabelWidth)) + "  " : blank) + line);
    }

    private string Banner(string message, Func<string, string> color)
    {
        var label = _pc.Bold(_pc.Inverse(" " + message + " "));
        var (before, after) = Format.Rule(label, _columns);
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

    private sealed record FinishedStep(ReportExploreStep Step, int Findings);

    private sealed class OpenStep(OpenedStep step)
    {
        public OpenedStep Step { get; } = step;

        public int Findings { get; private set; }

        public void AddFinding() => Findings++;
    }
}
