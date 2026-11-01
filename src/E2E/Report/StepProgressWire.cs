// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// .NET-only: the public form of the port's internal step records. The recorder keeps its own records (<see cref="StepProgress"/>,
/// <see cref="StepRecord"/>), and a hosted session hands out the report's types, read from the JSON <see cref="ReportWire"/> writes, as
/// <see cref="ReportSerializer"/> does for <see cref="Reports.Report"/>.
/// </summary>
internal static class StepProgressWire
{
    public static Reports.StepProgress Public(StepProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return new Reports.StepProgress
        {
            Phase = Enum.Parse<Reports.StepProgressPhase>(progress.Phase.ToString()),
            Identity = progress.Identity is { } id ? new Reports.StepProgressIdentity(id.AttemptId, id.AttemptIndex, id.StepId, id.StepIndex) : null,
            Api = progress.Api,
            Kind = progress.Kind is { } kind ? Enum.Parse<Reports.StepKind>(kind.ToString()) : null,
            Label = progress.Label,
            Status = progress.Status is { } status ? Enum.Parse<Reports.StepStatus>(status.ToString()) : null,
            DurationMs = progress.DurationMs,
            ModelCalls = progress.ModelCalls,
            Error = progress.Error is { } error ? Read<Reports.ReportError>(ReportWire.Error(ReportError.Of(error))) : null,
            Explanation = progress.Explanation,
            Event = progress.Event is { } stepEvent ? Read<Reports.StepEvent>(ReportWire.Event(stepEvent)) : null,
            Activity = progress.Activity is { } activity ? Enum.Parse<Reports.StepActivity>(activity.ToString()) : null,
            Active = progress.Active,
        };
    }

    /// <summary>The step as the report tells it.</summary>
    public static Reports.ReportStep Public(StepRecord step) => Read<Reports.ReportStep>(ReportWire.Step(step));

    private static T Read<T>(JsonObject node) =>
        node.Deserialize<T>(ReportSerializer.Options) ?? throw new InvalidOperationException("The report document is empty.");
}
