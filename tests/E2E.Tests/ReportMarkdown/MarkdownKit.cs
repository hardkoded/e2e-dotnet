// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.ReportFailureText;
using E2E.Tests.Traces;

namespace E2E.Tests.ReportMarkdown;

/// <summary>The helpers upstream's <c>unit/report-markdown.test.ts</c> declares at the top of the file, over the port's records.</summary>
internal static class MarkdownKit
{
    public static readonly DateTimeOffset FinishedAt = ReportBuilders.ReportAt.AddMilliseconds(8_400);

    public static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 3, TimeSpan.Zero);


    /// <summary>Upstream's <c>reportTarget()</c>: a web target.</summary>
    public static ReportTarget Target(string id = "web", int index = 0) => new(id, index, "web", BuildReport.DescribeTarget(null));

    /// <summary>A document the page has something to say about: a version, a duration, one target.</summary>
    public static Report1Document Page(
        ReportRunStatus status = ReportRunStatus.Passed,
        IReadOnlyList<ReportResult>? results = null,
        IReadOnlyList<ReportTarget>? targets = null,
        IReadOnlyList<ReportError>? errors = null,
        IReadOnlyList<ReportFilter>? filters = null,
        ReportChanges? changes = null,
        DateTimeOffset? finishedAt = null,
        ReportUsage? usage = null,
        ReportExplore? explore = null) =>
        new(new ReportRun("run-1", "0.9.0", targets ?? [Target()], results ?? [], explore)
        {
            Status = status,
            ExitCode = status == ReportRunStatus.Passed ? 0 : 1,
            StartedAt = ReportBuilders.ReportAt,
            FinishedAt = finishedAt ?? FinishedAt,
            Errors = errors ?? [],
            Filters = filters ?? [],
            Changes = changes,
            Usage = usage,
        });

    /// <summary>A result named by its title path and file, so the page's names read as a user's would.</summary>
    public static ReportResult Named(
        IReadOnlyList<string> title,
        ResultStatus status,
        string file = "tests/example.e2e.ts",
        int line = 3,
        string target = "web",
        IReadOnlyList<ReportAttempt>? attempts = null,
        ReportSkip? skip = null) =>
        new()
        {
            Id = file + "|" + string.Join(">", title) + "|" + target,
            File = file,
            TitlePath = title,
            Source = new SourceLocation(file, line, 1),
            TargetId = target,
            Status = status,
            Attempts = attempts ?? [],
            Skip = skip,
        };

    public static ReportResult Named(string title, ResultStatus status, string file = "tests/example.e2e.ts", int line = 3, string target = "web", IReadOnlyList<ReportAttempt>? attempts = null, ReportSkip? skip = null) =>
        Named([title], status, file, line, target, attempts, skip);

    /// <summary>An attempt of <paramref name="status"/>; its artifacts are named by kind, as upstream's fixture does.</summary>
    public static ReportAttempt Attempt(
        AttemptStatus status = AttemptStatus.Passed,
        long durationMs = 1_200,
        ReportError? error = null,
        IReadOnlyList<string>? artifacts = null,
        IReadOnlyList<StepRecord>? steps = null) =>
        new()
        {
            Status = status,
            DurationMs = durationMs,
            Steps = steps ?? [],
            Artifacts = [.. (artifacts ?? []).Select((kind, index) => new ReportArtifact("attempt-1:artifact:" + index, kind, "t/attempt-0/" + kind + "-" + index + ".bin"))],
            Error = error,
        };

    /// <summary>A step as most fixtures need it; its source is unknown unless a test says otherwise, so the block's location stays the test's own.</summary>
    public static StepRecord Step(
        int index,
        string label,
        StepKind kind = StepKind.Screen,
        string api = "screen.tap",
        StepStatus status = StepStatus.Passed,
        long durationMs = 900,
        SourceLocation? source = null,
        StepMetrics? metrics = null,
        CacheInfo? cache = null,
        string? explanation = null,
        ReportError? error = null)
    {
        var step = FailureTextKit.Step(index, kind, api, status, error, source, durationMs: durationMs, metrics: metrics, label: label, explanation: explanation);
        step.Cache = cache;
        return step;
    }

    public static StepMetrics Metrics(int modelCalls) => new() { ModelCalls = modelCalls, ActionSteps = 3, ObservationBytes = 1, ContextBytes = 1, LedgerBytes = 1 };

    /// <summary>A run that used the agent, with the usage the runner sums.</summary>
    public static ReportUsage Spent { get; } = new() { ModelTokens = 128_400, ModelCachedTokens = 79_600, EstimatedCostUsd = 0.31 };

    public static readonly MarkdownReportOptions Links = new()
    {
        ArtifactsUrl = "https://github.com/o/r/actions/runs/9",
        SourceUrl = (file, line) => "https://github.com/o/r/blob/abc/" + file + "#L" + line,
    };

    public static ReportResult Failing { get; } = Named(
        ["members", "an email invitation is accepted by the invited account only"],
        ResultStatus.Failed,
        file: "tests/members.e2e.ts",
        line: 41,
        attempts:
        [
            Attempt(
                AttemptStatus.Failed,
                error: new ReportError("ASSERTION_FAILED", "expected heading \"Welcome, Ada\" to be visible"),
                artifacts: ["screenshot", "video"],
                steps:
                [
                    Step(0, "open /", StepKind.App, "app.open"),
                    Step(1, "Sign in as the owner", StepKind.Agent, "agent.act", metrics: Metrics(4), cache: new CacheInfo { Mode = "self-finalized", ReplayedActions = 3, TotalActions = 3 }),
                    Step(2, "tap Send invitation"),
                    Step(
                        3,
                        "Accept the invitation from the email",
                        StepKind.Agent,
                        "agent.act",
                        StepStatus.Failed,
                        38_000,
                        metrics: Metrics(12),
                        explanation: "The Accept button opened a page that still shows Sign in.",
                        error: ReportBuilders.Error(message: "expected heading \"Welcome, Ada\" to be visible")),
                    Step(4, "tap Accept", status: StepStatus.Cancelled),
                    Step(5, "heading visible", StepKind.Assertion, "expect", StepStatus.Cancelled),
                ]),
        ]);

    public static ReportResult Flaky { get; } = Named(
        "todos survive a filter round-trip",
        ResultStatus.Flaky,
        file: "tests/todos.e2e.ts",
        attempts:
        [
            Attempt(
                AttemptStatus.Failed,
                error: new ReportError("STEP_TIMEOUT", "slow"),
                artifacts: ["screenshot"],
                steps:
                [
                    Step(0, "open /todos"),
                    Step(1, "the filtered list", StepKind.Agent, "agent.waitFor", StepStatus.TimedOut, 30_000, error: ReportBuilders.Error("STEP_TIMEOUT", "slow")),
                ]),
            Attempt(AttemptStatus.Passed, 6_400, steps: [Step(0, "open /todos"), Step(1, "the filtered list", StepKind.Agent, "agent.waitFor")]),
        ]);

    public static ReportResult Skipped { get; } = Named("not ready yet", ResultStatus.Skipped, file: "tests/todos.e2e.ts", skip: new ReportSkip("explicit", "waiting on the API"));

    public static ReportResult Passing { get; } = Named("opens the app", ResultStatus.Passed, file: "tests/smoke.e2e.ts", attempts: [Attempt(durationMs: 850)]);

    public static ReportResult Judged { get; } = Named(
        ["dashboard", "opens directly"],
        ResultStatus.Passed,
        file: "tests/smoke.e2e.ts",
        attempts: [Attempt(durationMs: 2_100, steps: [Step(0, "the dashboard is shown", StepKind.Agent, "agent.assert", metrics: Metrics(1))])]);

    public static ReportExploreFinding Finding(
        int index,
        string title,
        ReportExploreFindingKind kind = ReportExploreFindingKind.Issue,
        int severity = 4,
        int? step = 1,
        IReadOnlyList<string>? reproduction = null,
        string? path = "/cart",
        string? artifactId = null) =>
        new("finding-" + index, index, step, kind, severity, title, "The total reflects the cart", "Total: $0.00", reproduction ?? ["Add two items", "Open the cart"], At, path, artifactId);

    /// <summary>An exploration whose one test failed with the verdict its findings express.</summary>
    public static Report1Document Explored(
        IReadOnlyList<ReportExploreFinding>? findings = null,
        string? summary = "The cart is the weak spot.",
        IReadOnlyList<ReportExploreStep>? steps = null,
        ReportExploreEnded ended = ReportExploreEnded.Finished,
        ReportRunStatus status = ReportRunStatus.Failed)
    {
        findings ??= [Finding(0, "Cart total ignores quantity", artifactId: "a:artifact:0")];
        var issues = findings.Any(entry => entry.Kind == ReportExploreFindingKind.Issue);
        var evidence = Attempt(issues ? AttemptStatus.Failed : AttemptStatus.Passed) with
        {
            Id = "a",
            Artifacts = [new ReportArtifact("a:artifact:0", "screenshot", "web/explore-checkout-d287e8aead677d36/default/attempt-0/finding-0.png")],
            Error = issues ? new ReportError("ASSERTION_FAILED", "1 issue found") : null,
        };
        var explore = new ReportExplore(
            "Explore checkout",
            new ExploreBudgets(8, TimeSpan.FromMilliseconds(600_000)),
            ended,
            steps ?? [new ReportExploreStep(1, "Cart", "Add items and change quantities", ReportExploreStepStatus.Passed, At, TimeSpan.FromMilliseconds(20_000))],
            findings,
            summary);
        return Page(status, [Named("Explore checkout", issues ? ResultStatus.Failed : ResultStatus.Passed, file: "explore", attempts: [evidence])], explore: explore);
    }

    /// <summary>A result <paramref name="filter"/> left out on <paramref name="target"/>, unselected as the runner writes it.</summary>
    public static ReportResult LeftOut(string title, string filter, string reason, string target = "web") =>
        Named(title, ResultStatus.Skipped, file: "tests/billing.e2e.ts", target: target, skip: new ReportSkip("filtered", reason, filter)) with { Selected = false };

    /// <summary>A filter's report record, <c>areas</c> keeping 1 of 3 tests unless the arguments say otherwise.</summary>
    public static ReportFilter Record(string name = "areas", int candidates = 3, int excluded = 2, string? note = null, FilterOutcome? outcome = null) =>
        new(name, candidates, excluded, note, outcome);

    /// <summary>Whether <paramref name="text"/> holds half of a surrogate pair.</summary>
    public static bool HasLoneSurrogate(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(text[index]))
            {
                return true;
            }
        }

        return false;
    }
}
