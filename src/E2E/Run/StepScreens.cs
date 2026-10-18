// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>One node of a screen as the trace compares it: the engine's id, and the node as a line describes it, without the id or focus.</summary>
internal sealed record ScreenLine(string Id, string Text);

/// <summary>One screen as the trace keeps it: where it was, and its nodes in screen order.</summary>
internal sealed record ScreenText(string? Location, IReadOnlyList<ScreenLine> Lines);

/// <summary>How the screen a step saw differs from the one before it. Upstream's <c>StepScreen</c>.</summary>
internal sealed record StepScreen
{
    /// <summary>Where the screen was, as the engine says.</summary>
    public string? Location { get; init; }

    /// <summary>Nodes the screen listed.</summary>
    public required int Nodes { get; init; }

    /// <summary>The step whose screen this one is compared with; null for the first screen of the attempt.</summary>
    public int? Since { get; init; }

    /// <summary><c>added</c>, <c>removed</c>, and <c>changed</c> node lines, without node ids, in screen order; empty when nothing changed.</summary>
    public required IReadOnlyList<string> Changes { get; init; }

    /// <summary>Change lines past <see cref="Changes"/>; null when none.</summary>
    public int? More { get; init; }
}

/// <summary>What <see cref="StepScreens.TraceScreen"/> redacts with and bounds by. Upstream's <c>TraceScreenOptions</c>.</summary>
internal sealed record TraceScreenOptions(Redactor Redactor, int MaxBytes, string? AppOrigin);

/// <summary>
/// How the screen changed from one step to the next, for the trace (upstream <c>run/step-screens.ts</c>). Each step keeps the last
/// screen it saw (an observation, or a screen the engine handed over), compared with the last screen of the step before that saw
/// one. Lines are compared without their node ids: a device engine mints new ids on every capture, so only the text says whether a
/// node stayed. Where one id names a node on both screens (a browser keeps them), a node whose text moved reads as changed rather
/// than as removed and added.
/// </summary>
internal static class StepScreens
{
    /// <summary>Change lines a step keeps; the rest are counted.</summary>
    private const int MaxScreenChanges = 12;

    /// <summary>Characters of one change line.</summary>
    private const int MaxChangeChars = 200;

    /// <summary>The observation byte budget upstream's <c>agent.maxObservationBytes</c> defaults to. The port has no such key.</summary>
    internal const int DefaultObservationBytes = 262_144;

    /// <summary>The step's screen against the one <c>since</c> saw, or as the first screen when there was none.</summary>
    public static StepScreen CompareScreens((int Step, ScreenText Screen)? previous, ScreenText next)
    {
        var after = next.Lines;
        if (previous is not { } before)
        {
            return new StepScreen { Location = next.Location, Nodes = after.Count, Changes = [] };
        }

        var changes = DiffLines(before.Screen.Lines, after);
        return new StepScreen
        {
            Location = next.Location,
            Nodes = after.Count,
            Since = before.Step,
            Changes = [.. changes.Take(MaxScreenChanges).Select(line => TextRules.Bound(line, MaxChangeChars))],
            More = changes.Count > MaxScreenChanges ? changes.Count - MaxScreenChanges : null,
        };
    }

    // The lines that differ: a node one id names on both screens with other text is changed; past those, text is counted, so a line
    // the new screen lists more often than the old is added, and one it lists less often is removed.
    private static List<string> DiffLines(IReadOnlyList<ScreenLine> before, IReadOnlyList<ScreenLine> after)
    {
        var beforeById = new Dictionary<string, ScreenLine>(StringComparer.Ordinal);
        foreach (var line in before)
        {
            beforeById[line.Id] = line;
        }

        var paired = new HashSet<ScreenLine>(ReferenceEqualityComparer.Instance);
        var changed = new Dictionary<ScreenLine, ScreenLine>(ReferenceEqualityComparer.Instance);
        foreach (var line in after)
        {
            if (!beforeById.TryGetValue(line.Id, out var old))
            {
                continue;
            }

            paired.Add(old);
            if (!string.Equals(old.Text, line.Text, StringComparison.Ordinal))
            {
                changed[line] = old;
            }
        }

        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in before)
        {
            if (!paired.Contains(line))
            {
                remaining[line.Text] = remaining.GetValueOrDefault(line.Text) + 1;
            }
        }

        var lines = new List<string>();
        foreach (var line in after)
        {
            if (changed.TryGetValue(line, out var old))
            {
                lines.Add($"changed {line.Text} (was: {old.Text})");
                continue;
            }

            if (beforeById.ContainsKey(line.Id))
            {
                continue;
            }

            var left = remaining.GetValueOrDefault(line.Text);
            if (left > 0)
            {
                remaining[line.Text] = left - 1;
            }
            else
            {
                lines.Add($"added {line.Text}");
            }
        }

        foreach (var line in before)
        {
            if (paired.Contains(line))
            {
                continue;
            }

            var left = remaining.GetValueOrDefault(line.Text);
            if (left == 0)
            {
                continue;
            }

            remaining[line.Text] = left - 1;
            lines.Add($"removed {line.Text}");
        }

        return lines;
    }

    /// <summary>
    /// One observation as the trace keeps it: every node redacted the way the screen at failure is, then described the way its line
    /// reads, in screen order, up to <see cref="TraceScreenOptions.MaxBytes"/> of text. Nodes the page does not show are left out,
    /// as the lines the model reads leave them out. Focus is not compared: it moves with every action.
    /// </summary>
    public static ScreenText TraceScreen(Observation observation, TraceScreenOptions options)
    {
        var lines = new List<ScreenLine>();
        var bytes = 0;
        bool Walk(SemanticNode node)
        {
            var shown = !(node.States.Hidden || node.States.AriaHidden);
            if (shown)
            {
                var text = SnapshotText.Describe(node, options.Redactor, options.AppOrigin, listing: false);
                bytes += Encoding.UTF8.GetByteCount(text) + 1;
                if (lines.Count > 0 && bytes > options.MaxBytes)
                {
                    return false;
                }

                lines.Add(new ScreenLine(node.Ref, text));
            }

            return node.Children.All(Walk);
        }

        _ = observation.Roots.All(Walk);
        return new ScreenText(options.Redactor.Redact(observation.Route), lines);
    }
}
