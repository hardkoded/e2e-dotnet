// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Internal;

namespace E2E;

/// <summary>One completed step as the ledger reads it: sanitized quoted evidence, nothing else.</summary>
/// <param name="Api">Public API name, e.g. <c>agent.act</c>, <c>locator.tap</c>, <c>app.open</c>.</param>
/// <param name="Label">The step's label.</param>
/// <param name="Status">How the step ended.</param>
/// <param name="Explanation">The step's handoff: an agent verdict summary or judgment explanation.</param>
internal sealed record PriorStep(string Api, string Label, StepStatus Status, string? Explanation = null);

/// <summary>The serialized ledger and its size in bytes.</summary>
internal sealed record LedgerContext(string Text, int Bytes);

/// <summary>
/// Prior-step context for agent prompts (upstream's <c>agent/ledger.ts</c>). The ledger is not stored anywhere: it is derived on demand
/// from the step timeline the session already records, so there is exactly one account of what happened. Entries are untrusted quoted
/// evidence and never carry policy authority.
/// </summary>
internal static class Ledger
{
    /// <summary>Maximum size of one handoff, before ledger-wide compaction.</summary>
    public const int MaxHandoffBytes = 700;

    /// <summary>Bytes the prior-step ledger an act step reads may take. The runner fixes it.</summary>
    public const int MaxLedgerBytes = 8192;

    private const int MaxLabelBytes = 256;

    // Bytes one prior-step field may occupy: sanitized quoted evidence, never policy.
    private const int MaxPriorStepFieldBytes = 8192;

    /// <summary>
    /// Projects recorded steps onto the ledger's shape, sanitized here, the one trust boundary where step records become model
    /// input, and carrying nothing that names runner internals.
    /// </summary>
    public static IReadOnlyList<PriorStep> ProjectPriorSteps(IEnumerable<StepRecord> records) =>
        [.. records.Select(step => new PriorStep(step.Api, BoundedText(step.Label), step.Status, step.Explanation is null ? null : BoundedText(HandoffOf(step))))];

    /// <summary>
    /// Serializes prior steps as prompt context: newest entries first until the byte budget is reached, then emitted chronologically
    /// with the dropped count prepended. Takes the already-sanitized steps and bounds each label and handoff to the ledger's own line budget.
    /// </summary>
    public static LedgerContext SerializeLedger(IReadOnlyList<PriorStep> steps, int maxBytes)
    {
        var lines = new List<string>();
        var bytes = 0;
        var index = steps.Count - 1;
        for (; index >= 0; index--)
        {
            var line = FormatEntry(steps[index], index + 1);
            var size = Encoding.UTF8.GetByteCount(line + "\n");
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            lines.Add(line);
        }

        lines.Reverse();
        var dropped = index + 1;
        if (dropped > 0)
        {
            lines.Insert(0, "[" + dropped.ToString(CultureInfo.InvariantCulture) + " earlier step(s) omitted]");
        }

        var text = string.Join('\n', lines);
        return new LedgerContext(text, Encoding.UTF8.GetByteCount(text));
    }

    // A replayed step's handoff is the verdict its recording run wrote; the replay notice in front of it describes the cache, which the model has no use for.
    private static string HandoffOf(StepRecord step)
    {
        var explanation = step.Explanation ?? "";
        return step.Cache is { Mode: "self-finalized" } ? StepTraceSession.RecordedVerdictOf(explanation) : explanation;
    }

    private static string BoundedText(string text) => TextRules.TruncateUtf8(TextRules.SanitizeText(text), MaxPriorStepFieldBytes);

    private static string FormatEntry(PriorStep step, int position)
    {
        var label = TextRules.TruncateUtf8(step.Label, MaxLabelBytes);
        var head = position.ToString(CultureInfo.InvariantCulture) + ". " + step.Api + " " + StepNames.Of(step.Status) + (label.Length == 0 ? "" : " :: " + label);
        return step.Explanation is null ? head : head + "\n   observed: " + TextRules.TruncateUtf8(step.Explanation, MaxHandoffBytes);
    }
}
