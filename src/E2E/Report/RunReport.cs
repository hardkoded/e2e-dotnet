// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// The attempts of each test, collected over the run, so one trace page tells them all. .NET-only: upstream's runner keeps a
/// <c>ResultRecord</c> per test with every attempt in it (<c>run/records.ts</c>) and builds the report from them (<c>report/build.ts</c>).
/// In the port a test framework runs each attempt as its own session, so the sessions add their attempts here, and the result they
/// get back is the one upstream's <c>buildReport</c> would have for the test: its status is derived from its attempts, and a test
/// is traced when an attempt kept a trace (upstream's <c>tracedResultIds</c>).
/// </summary>
internal static class RunReport
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Entry> Results = new(StringComparer.Ordinal);

    private sealed class Entry
    {
        public List<ReportAttempt> Attempts { get; } = [];

        public List<Func<string, string>> Redactors { get; } = [];

        public bool Traced { get; set; }

        /// <summary>Where the test is declared, as the first attempt that ran a step located it.</summary>
        public (string File, SourceLocation Source)? Located { get; set; }
    }

    /// <summary>
    /// Adds the attempt <paramref name="attempt"/> (one-based) of the single-attempt <paramref name="report"/> to its test's, and
    /// returns the report with every attempt the test has had so far. A first attempt starts the test over: a repeat of it, or a
    /// later run, does not extend the attempts of the one before. <paramref name="kept"/> says whether this attempt keeps a trace;
    /// the result is traced when any attempt did. <paramref name="redact"/> is the attempt's redactor: the page of the test is redacted
    /// by the redactors of all its attempts, since a secret one learned shows in the steps it recorded. An attempt that passed or was skipped ends
    /// the test (a retry follows a failure only), so its entry is dropped once the report is built.
    /// </summary>
    public static (Report1Document Report, bool Traced, Func<string, string> Redact) Add(Report1Document report, string resultsRoot, int attempt, bool kept, Func<string, string> redact)
    {
        var single = report.Run.Results[0];
        var key = resultsRoot + "\n" + single.Id;
        List<ReportAttempt> attempts;
        bool traced;
        List<Func<string, string>> redactors;
        (string File, SourceLocation Source)? located;
        lock (Gate)
        {
            if (attempt <= 1 || !Results.TryGetValue(key, out var entry))
            {
                entry = Results[key] = new Entry();
            }

            entry.Attempts.Add(single.Attempts[0]);
            entry.Redactors.Add(redact);
            entry.Traced |= kept;
            if (entry.Located is null && single.File != "unknown")
            {
                entry.Located = (single.File, single.Source);
            }

            located = entry.Located;
            attempts = [.. entry.Attempts];
            traced = entry.Traced;
            redactors = [.. entry.Redactors];
            if (single.Attempts[0].Status is AttemptStatus.Passed or AttemptStatus.Skipped)
            {
                Results.Remove(key);
            }
        }

        var result = single with { Attempts = attempts, Status = StatusOf(attempts) };
        if (located is { } where)
        {
            result = result with { File = where.File, Source = where.Source };
        }

        return (report with { Run = report.Run with { Results = [result] } }, traced, text => redactors.Aggregate(text, (redacted, each) => each(redacted)));
    }

    /// <summary>How a test ended over its attempts: one that passed after an attempt that did not is flaky.</summary>
    internal static ResultStatus StatusOf(IReadOnlyList<ReportAttempt> attempts)
    {
        var final = attempts.Count == 0 ? AttemptStatus.Skipped : attempts[^1].Status;
        return final switch
        {
            AttemptStatus.Passed when attempts.Take(attempts.Count - 1).Any(earlier => earlier.Status is not (AttemptStatus.Passed or AttemptStatus.Skipped)) => ResultStatus.Flaky,
            AttemptStatus.Passed => ResultStatus.Passed,
            AttemptStatus.TimedOut => ResultStatus.TimedOut,
            AttemptStatus.Interrupted => ResultStatus.Interrupted,
            AttemptStatus.Skipped => ResultStatus.Skipped,
            _ => ResultStatus.Failed,
        };
    }
}
