// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>
/// <c>E2E_DEBUG</c> presentation of agent steps (upstream's <c>report/debug-steps.ts</c>), derived entirely from the step records
/// every attempt already keeps. There is no separate debug telemetry channel for agent steps: the timeline is the single account of
/// what happened.
/// </summary>
internal static class DebugSteps
{
    // JSON.stringify keeps a character it need not escape, so a label reads as the author wrote it.
    private static readonly JsonSerializerOptions Quoting = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Renders one aligned table of every agent step, in execution order, with per-phase splits summed from the step's child events.
    /// Returns the empty string when the run performed no agent steps. Upstream's <c>agentStepTable</c>, which takes the results of a
    /// run; the port hands it the steps of the attempts it has.
    /// </summary>
    public static string AgentStepTable(IEnumerable<IReadOnlyList<StepRecord>> attempts)
    {
        var steps = attempts.SelectMany(attempt => attempt.Where(step => step.Kind == StepKind.Agent)).ToList();
        if (steps.Count == 0)
        {
            return "";
        }

        var models = steps.Select(ViaLabel).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var mixedModels = models.Count > 1;
        var rows = steps.Select(step => (string[])
        [
            Truncate(step.Label.Length == 0 ? step.Api : step.Api + " " + JsonSerializer.Serialize(step.Label, Quoting), 64),
            .. mixedModels ? new[] { ViaLabel(step) ?? "-" } : [],
            DebugTrace.FormatMs(step.DurationMs),
            DebugTrace.FormatMs(EventMs(step, StepEventKind.Model)),
            DebugTrace.FormatMs(EventMs(step, StepEventKind.Observation)),
            DebugTrace.FormatMs(EventMs(step, StepEventKind.Engine)),
            (step.Model?.Calls ?? 0).ToString(CultureInfo.InvariantCulture),
            (step.Model?.InputTokens ?? 0).ToString(CultureInfo.InvariantCulture) + "/" + (step.Model?.OutputTokens ?? 0).ToString(CultureInfo.InvariantCulture),
            CachedShare(step),
            step.Model?.EstimatedCostUsd is { } cost ? FormatUsd(cost) : "-",
        ]).ToList();

        var knownCosts = steps.Select(step => step.Model?.EstimatedCostUsd).OfType<double>().ToList();
        var total = knownCosts.Count == 0 ? "" : ", total " + FormatUsd(knownCosts.Sum()) + (knownCosts.Count < steps.Count ? "+" : "");
        var sharedModel = mixedModels || models.Count == 0 ? "" : ", model " + models[0];
        return DebugTrace.Table(
            "[e2e debug] agent steps (execution order" + sharedModel + total + ")",
            ["step", .. mixedModels ? new[] { "via" } : [], "total", "model", "observe", "action", "calls", "tokens in/out", "cached", "cost"],
            rows,
            "(no agent steps recorded)");
    }

    // The model a step reported, as the `via` column names it; null for a step with no model or no provenance.
    private static string? ViaLabel(StepRecord step) => step.Model is null ? null : ReportFormat.StepModelLabel(step.Model);

    private static long EventMs(StepRecord step, StepEventKind kind) => step.Events.Where(item => item.Kind == kind).Sum(item => item.DurationMs);

    // The share of the step's input the provider served from its prompt cache, `38% (5700)`; `-` when the provider reports no cache split.
    private static string CachedShare(StepRecord step)
    {
        if (step.Model is not { CacheReadTokens: { } read } model)
        {
            return "-";
        }

        if (model.InputTokens == 0)
        {
            return "0%";
        }

        var share = (int)Math.Round((double)read / model.InputTokens * 100, MidpointRounding.AwayFromZero);
        return share.ToString(CultureInfo.InvariantCulture) + "% (" + read.ToString(CultureInfo.InvariantCulture) + ")";
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private static string FormatUsd(double value)
    {
        var text = value.ToString(value >= 0.1 ? "F2" : "F6", CultureInfo.InvariantCulture);
        if (text.Contains('.', StringComparison.Ordinal))
        {
            text = text.TrimEnd('0').TrimEnd('.');
        }

        return "$" + text;
    }
}
