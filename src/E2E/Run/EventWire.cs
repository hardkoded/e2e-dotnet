// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Reports;

namespace E2E;

/// <summary>
/// .NET-only: builds the run events a producer in this process emits from the port's own records. The public events carry the report's public
/// tree, read from the JSON <see cref="ReportWire"/> writes (as <see cref="StepProgressWire"/> does for a step), so what a reporter sees of a
/// result is what <c>report.json</c> says of it.
/// </summary>
internal static class EventWire
{
    /// <summary>
    /// The <c>test-finished</c> result of <paramref name="result"/>, redacted by <paramref name="redact"/>. <paramref name="identity"/> says who the test is;
    /// <paramref name="resultsRoot"/> is the directory the attempts' artifact paths start at.
    /// </summary>
    public static RunEventResult Result(ReportResult result, TestIdentity identity, string resultsRoot, Func<string, string> redact)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(identity);
        var node = ReportWire.Result(result, result.Platform, resultsRoot);
        ReportWire.Redact(node, redact);
        var wire = node.Deserialize<Reports.ReportResult>(ReportSerializer.Options) ?? throw new InvalidOperationException("The report result is empty.");
        return new RunEventResult
        {
            Test = identity,
            Target = (result.TargetId, result.Platform),
            ResultId = result.Id,
            Agent = result.Agent,
            Repeat = result.Repeat,
            Status = wire.Status,
            Selected = result.Selected,
            Skip = wire.Skip,
            Attempts = wire.Attempts,
            Recorded = result,
        };
    }

    /// <summary>The error of a <c>run-error</c> event, as the report tells an error.</summary>
    public static Reports.ReportError Error(ReportError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return ReportWire.Error(error).Deserialize<Reports.ReportError>(ReportSerializer.Options) ?? throw new InvalidOperationException("The report error is empty.");
    }

    /// <summary>The error of a failure that ended the run, serialized without a stack and redacted by <paramref name="redact"/>.</summary>
    public static Reports.ReportError Error(Exception failure, string projectRoot, Func<string, string> redact)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Error(ReportError.Of(ErrorSerializer.Serialize(failure, projectRoot, redact)));
    }

    /// <summary>The step's progress, as a <c>step</c> event carries it.</summary>
    public static StepProgressEvent Step(StepProgress progress, string testId, string agent, string target) =>
        new(testId, agent, 0, target, StepProgressWire.Public(progress));
}
