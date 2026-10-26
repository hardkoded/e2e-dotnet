// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;

namespace E2E.Tests.ReportFailureText;

/// <summary>The helpers upstream's <c>unit/report-failure-text.test.ts</c> declares at the top of the file.</summary>
internal static class FailureTextKit
{
    /// <summary>A step as the runner records it, with no known source unless one is given: upstream's <c>step()</c>, whose source is <c>unknown</c>.</summary>
    public static StepRecord Step(
        int index,
        StepKind kind = StepKind.Locator,
        string api = "locator.tap",
        StepStatus status = StepStatus.Passed,
        ReportError? error = null,
        SourceLocation? source = null,
        StepPhase? phase = null,
        long durationMs = 100,
        StepMetrics? metrics = null,
        string? label = null,
        string? explanation = null,
        IReadOnlyList<StepTurn>? turns = null,
        IEnumerable<string>? artifacts = null)
    {
        var step = new StepRecord
        {
            Id = "s" + index,
            Index = index,
            Kind = kind,
            Api = api,
            Label = label ?? "step " + index,
            Source = source,
            Status = status,
            StartedAt = ReportBuilders.ReportAt,
            DurationMs = durationMs,
            Phase = phase,
            Error = error is null ? null : new SerializedError("test", error.Code, error.Message, false, error.Source, error.Details),
            Metrics = metrics,
            Explanation = explanation,
            Turns = turns,
        };
        step.Artifacts.AddRange(artifacts ?? []);
        return step;
    }

    /// <summary>A failed attempt as the runner records it: the step that ended it carries its error.</summary>
    public static ReportAttempt Failed(ReportError? error = null, IReadOnlyList<StepRecord>? steps = null, IReadOnlyList<ReportArtifact>? artifacts = null, E2E.FailureEvidence? failure = null, ReportSkip? skip = null)
    {
        error ??= ReportBuilders.Error();
        return new ReportAttempt
        {
            Status = AttemptStatus.Failed,
            DurationMs = 1234,
            Error = error,
            Steps = steps ?? [Step(0), Step(1, status: StepStatus.Failed, error: error)],
            Artifacts = artifacts ?? [],
            Failure = failure,
            Skip = skip,
        };
    }

    public static ReportResult Result(ResultStatus status = ResultStatus.Failed, IReadOnlyList<ReportAttempt>? attempts = null) =>
        ReportBuilders.Result(status: status, attempts: attempts ?? [Failed()]);
}
