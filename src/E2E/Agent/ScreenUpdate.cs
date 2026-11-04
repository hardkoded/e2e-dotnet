// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>How a screen update reads: the lead line names the action it follows (upstream's <c>ScreenUpdateOptions</c>).</summary>
internal sealed record ScreenUpdateOptions
{
    /// <summary>Prose placed before the screen, such as <c>Tapped #n16.</c>.</summary>
    public string? Lead { get; init; }

    /// <summary>
    /// Whether the update follows an action that should have changed the screen. An unchanged screen is then reported as the
    /// action having had no visible effect, which is what the model needs to stop repeating it.
    /// </summary>
    public bool ExpectChange { get; init; }

    /// <summary>
    /// Whether an on-screen keyboard leaving with the action is pointed out, since on a touch screen the tap that closes it may
    /// have done nothing else. Default on; off for the dismissal whose whole point that was, and for a plain look, which acted on nothing.
    /// </summary>
    public bool KeyboardNote { get; init; } = true;
}

/// <summary>
/// What the screenshot going out with an update says against the one the model held (upstream's <c>PixelEvidence</c>): <see cref="None"/> when
/// there is no pair to compare (no screenshot, or the step's first), <see cref="Unavailable"/> when the step can get no screenshot at all.
/// </summary>
internal enum PixelEvidence
{
    /// <summary>No pair of screenshots to compare.</summary>
    None,

    /// <summary>The screenshot moved.</summary>
    Changed,

    /// <summary>The same image again.</summary>
    Unchanged,

    /// <summary>The step can get no screenshot.</summary>
    Unavailable,
}

/// <summary>
/// What a session tells a coding agent about the screen, and how much (upstream's <c>ScreenPresenter</c>, <c>agent/screen-update.ts</c>).
/// The first look goes out whole. Every look after it is compared with the screen the agent already holds and rendered as the
/// difference: the lines that appeared, the lines that changed, the lines that went away. Node ids are stable for as long as an
/// element exists, so a line the agent saw earlier still names the same element, and a diff is a complete update rather than a
/// hint. A screen that changed mostly goes out whole again. Not ported: the screenshot half (<c>open</c>, <c>present</c>,
/// <c>latestScreenshot</c>). The built-in agent keeps that state in <see cref="PixelMode"/> and prints whole screens; it reads an unchanged
/// listing through <see cref="RenderUnchanged"/> too.
/// </summary>
internal sealed partial class ScreenPresenter
{
    /// <summary>A diff past this many lines goes out as the full screen instead.</summary>
    private const int MaxDiffLines = 60;

    /// <summary>A diff touching more than this share of the new screen goes out whole.</summary>
    private const double MaxDiffShare = 0.5;

    /// <summary>
    /// What the agent needs to hear when an action closed the keyboard: on a touch screen the tap that closes it is often spent on
    /// closing it, so the control under the finger may not have reacted.
    /// </summary>
    private const string KeyboardClosedNote =
        "The on-screen keyboard closed with this action. On a touch screen a tap made while the keyboard is up can be spent on closing it: if the control you acted on shows no effect above, act on it again now that the keyboard is down.";

    private ShownScreen? _shown;

    /// <summary>The screen as the agent last received it, indexed for comparison.</summary>
    private sealed record ShownScreen(string Revision, List<string> Order, Dictionary<string, string> ById, bool Truncated, bool Keyboard);

    /// <summary>The first screen, whole.</summary>
    public string Initial(ExecutorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _shown = observation.TreeUnavailable ? null : Index(observation);
        return RenderFull(observation, _shown);
    }

    /// <summary>A later screen, as the difference from the one the agent holds: nothing, the changed lines, or the whole screen when most of it changed.</summary>
    public string Update(ExecutorObservation observation, ScreenUpdateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        options ??= new ScreenUpdateOptions();
        var lead = options.Lead is null ? "" : options.Lead + "\n\n";
        if (observation.TreeUnavailable)
        {
            return lead + Initial(observation);
        }

        var previous = _shown;
        var next = Index(observation);
        _shown = next;
        if (previous is null)
        {
            return lead + RenderFull(observation, next);
        }

        var closed = !options.KeyboardNote || options.Lead is null ? "" : KeyboardNoteFor(previous, next);
        var diff = Diff(previous, next);
        var changes = diff.Count;

        // A truncated screen is never called unchanged: what it left out is unknown.
        if (changes == 0 && !observation.Truncated)
        {
            return lead + RenderUnchanged(previous.Revision, observation, options.ExpectChange, PixelEvidence.None) + closed;
        }

        if (changes >= MaxDiffLines || changes > MaxDiffShare * next.Order.Count)
        {
            return lead + "The screen changed substantially since revision " + previous.Revision + ". " + RenderFull(observation, next) + closed;
        }

        var assurance = observation.Truncated
            ? "The screen listing is truncated: nodes past the cut are not listed and none is reported removed; every listed node keeps the id you have."
            : "Every node not listed as removed is still on screen under the id you have.";
        var head = lead + "Screen changes since revision " + previous.Revision + " (now revision " + observation.Revision + DescribeLocation(observation) + ", "
            + next.ById.Count.ToString(CultureInfo.InvariantCulture) + " nodes): " + DescribeCounts(diff) + ". " + assurance;
        return string.Join('\n', [head, .. diff]) + closed;
    }

    // `2 added, 1 changed, 5 removed`, omitting the kinds that did not happen.
    private static string DescribeCounts(List<string> diff)
    {
        var counts = new[] { "added", "changed", "removed" }
            .Select(kind => (Count: diff.Count(line => line.StartsWith(kind + " ", StringComparison.Ordinal)), Kind: kind))
            .Where(entry => entry.Count > 0)
            .Select(entry => entry.Count.ToString(CultureInfo.InvariantCulture) + " " + entry.Kind);
        var joined = string.Join(", ", counts);
        return joined.Length == 0 ? "no listed node changed" : joined;
    }

    // One full screen, headed so a reader can find it later.
    private static string RenderFull(ExecutorObservation observation, ShownScreen? shown)
    {
        if (observation.TreeUnavailable)
        {
            return "Current screen (revision " + observation.Revision + DescribeLocation(observation) + ", semantic capture unavailable):\n" + observation.Text;
        }

        return "Current screen (revision " + observation.Revision + DescribeLocation(observation) + ", " + shown!.ById.Count.ToString(CultureInfo.InvariantCulture)
            + " nodes):\n" + observation.Text;
    }

    /// <summary>
    /// An update with no listed line changed (upstream's <c>renderUnchanged</c>). A screenshot that moved while the listing stood still is
    /// said so, whatever was expected: the effect is drawn, and blaming the control would send the model looking for another way to do what
    /// it just did. For the same reason the control is blamed only when an identical screenshot backs the tree: the tree alone cannot tell a
    /// drawn effect (a filled dot, a color) from none.
    /// </summary>
    internal static string RenderUnchanged(string since, ExecutorObservation observation, bool expectedChange, PixelEvidence pixels)
    {
        var looked = "re-observed as revision " + observation.Revision + DescribeLocation(observation);
        if (pixels == PixelEvidence.Changed)
        {
            return "Since revision " + since + " (" + looked + "): listed nodes unchanged; the screenshot changed. Read the effect off the image (a focus ring alone is no effect); the ids you have stay valid.";
        }

        if (!expectedChange)
        {
            return "Screen unchanged since revision " + since + " (" + looked + "); the ids you have stay valid.";
        }

        return pixels switch
        {
            PixelEvidence.Unchanged => "The screen did not change within the wait after this action (" + looked + "); the ids you have stay valid. If a change was expected, the control had no visible effect here: look for another way rather than repeating it.",
            PixelEvidence.Unavailable => "No listed node changed within the wait after this action (" + looked + "); the ids you have stay valid. No screenshot can be taken in this step, so the listing is the only evidence: if a change was expected, look for another way rather than repeating it.",
            _ => "No listed node changed within the wait after this action (" + looked + "); the ids you have stay valid. The listing shows only what the tree describes: a drawn change needs a screenshot to see.",
        };
    }

    // The keyboard note when the action took the keyboard off the screen. A truncated listing may have cut the keyboard off, not closed it.
    private static string KeyboardNoteFor(ShownScreen previous, ShownScreen next) =>
        !previous.Keyboard || next.Keyboard || next.Truncated ? "" : "\n\n" + KeyboardClosedNote;

    private static string DescribeLocation(ExecutorObservation observation) => observation.Path is null ? "" : ", path " + observation.Path;

    // Node id at the head of one rendered line, or null for a marker line.
    private static string? LineId(string line) => LeadingId().Match(line) is { Success: true } match ? match.Groups[1].Value : null;

    // The line without its focus state. Focus moves with every action and on its own, so a diff that reported it would drown the
    // changes that matter and make a control that did nothing look like it did something. The full screen keeps focus.
    private static string WithoutFocus(string line) => TrailingStates().Replace(line, match =>
    {
        var kept = match.Groups[1].Value.Split(' ').Where(state => state != "focused").ToList();
        return kept.Count == 0 ? "" : " [" + string.Join(' ', kept) + "]";
    });

    private static ShownScreen Index(ExecutorObservation observation)
    {
        var order = new List<string>();
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in observation.Text.Split('\n'))
        {
            if (LineId(raw) is not { } id)
            {
                continue;
            }

            // Without indentation: a node that only moved to another depth reads the same, and reporting it as changed would show
            // identical text twice.
            var line = WithoutFocus(raw).TrimStart();
            order.Add(line);
            byId[id] = line;
        }

        return new ShownScreen(observation.Revision ?? "", order, byId, observation.Truncated, order.Any(line => KeyboardLine().IsMatch(line)));
    }

    // The lines that differ between two screens, keyed by node id: additions and changes in the new screen's document order, then
    // removals in the old one's. A changed line is rendered whole with what it read before. Each line is prefixed with a word, not
    // a symbol: a model read `- #n19 button "Delete"` as a button that was there and tapped it twice more.
    private static List<string> Diff(ShownScreen previous, ShownScreen next)
    {
        var lines = new List<string>();
        foreach (var line in next.Order)
        {
            if (LineId(line) is not { } id)
            {
                continue;
            }

            if (!previous.ById.TryGetValue(id, out var before))
            {
                lines.Add("added " + line);
            }
            else if (before != line)
            {
                lines.Add("changed " + line + " (was: " + before + ")");
            }
        }

        // A truncated screen stopped listing nodes before the end; the ones it left out may well still exist, so nothing is called removed on its word.
        if (next.Truncated)
        {
            return lines;
        }

        foreach (var line in previous.Order)
        {
            if (LineId(line) is { } id && !next.ById.ContainsKey(id))
            {
                lines.Add("removed " + line);
            }
        }

        return lines;
    }

    [GeneratedRegex(@"^\s*#(\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingId();

    [GeneratedRegex(@" \[([^\]]*)\]\z", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingStates();

    // A node line whose role is the on-screen keyboard, as device trees list it.
    [GeneratedRegex(@"^#\S+ keyboard\b", RegexOptions.CultureInvariant)]
    private static partial Regex KeyboardLine();
}
