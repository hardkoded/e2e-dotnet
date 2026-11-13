// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Engine;

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

/// <summary>The screenshot the model holds newest, and the viewport it was taken of (upstream's <c>ShownScreenshot</c>).</summary>
internal sealed record ShownScreenshot(ExecutorPixels Pixels, TraceViewport? Viewport);

/// <summary>A rendered screen: the text, and the screenshot the model receives alongside it (upstream's <c>ScreenOutput</c>).</summary>
internal sealed record ScreenOutput(string Text, ExecutorPixels? Pixels = null);

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
/// hint. A screen that changed mostly goes out whole again.
/// A screen may carry its pixels too. The presenter is where a screenshot joins the text (<see cref="Open"/>, <see cref="Present"/>): it
/// notes the image's size and coordinate space under the screen, remembers that the model now holds pixels (from then on the step is in
/// pixel mode, and an unchanged tree under a screenshot that moved is reported as that rather than as a failed action), and explains once
/// why pixels the step asked for were withheld. One per step: the built-in agent's opening prompt and its tools share it
/// (<c>DefaultAgent.PresenterFor</c>, upstream's <c>presenterFor</c>).
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
    private ShownScreenshot? _screenshot;
    private string? _screenshotDigest;

    // True once the engine said it captures no screenshots at all.
    private bool _pixelsDenied;

    /// <summary>The screen as the agent last received it, indexed for comparison.</summary>
    private sealed record ShownScreen(string Revision, List<string> Order, Dictionary<string, string> ById, bool Truncated, bool Keyboard, ExecutorTabs? Tabs);

    /// <summary>
    /// What an exploring assertion's looks show (<see cref="AssertOptions.Vision"/>): <see cref="VisionMode.On"/> asks every look for pixels beside the tree,
    /// <see cref="VisionMode.Only"/> shows the pixels without the tree. <see cref="VisionMode.Off"/> for every other step.
    /// </summary>
    public VisionMode Vision { get; set; }

    /// <summary>True when every look asks for pixels, whether or not a screenshot went to the model yet.</summary>
    public bool WantsPixels => Vision != VisionMode.Off;

    /// <summary>
    /// A look as a step with <paramref name="vision"/> reads it (upstream's <c>withVision</c>): under <see cref="VisionMode.Only"/> the pixels without the tree,
    /// which fails <c>POLICY_DENIED</c> when no pixels are available. Any other mode keeps the look as it is.
    /// </summary>
    internal static ExecutorObservation ForVision(ExecutorObservation observation, VisionMode vision)
    {
        if (vision != VisionMode.Only)
        {
            return observation;
        }

        if (observation.Pixels is null)
        {
            throw new AgentException("POLICY_DENIED", "agent.assert vision: 'only' requires available pixel evidence", blocked: true);
        }

        return observation with { Text = "Judge from the screenshots only.", Tree = null, TreeUnavailable = true };
    }

    /// <summary>True once a screenshot went to the model in this step.</summary>
    public bool ShowingPixels => _screenshot is not null;

    /// <summary>The newest screenshot the model holds; the coordinate space of <c>tap_at</c> (upstream's <c>latestScreenshot</c>).</summary>
    public ShownScreenshot? LatestScreenshot => _screenshot;

    /// <summary>The first screen, whole.</summary>
    public string Initial(ExecutorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _shown = observation.TreeUnavailable ? null : Index(observation);
        return RenderFull(observation, _shown);
    }

    /// <summary>A later screen, as the difference from the one the agent holds: nothing, the changed lines, or the whole screen when most of it changed.</summary>
    public string Update(ExecutorObservation observation, ScreenUpdateOptions? options = null) => Render(observation, options ?? new ScreenUpdateOptions(), PixelEvidence.None);

    // The update, told what the screenshot going out with it says against the one the model held.
    private string Render(ExecutorObservation observation, ScreenUpdateOptions options, PixelEvidence pixels)
    {
        ArgumentNullException.ThrowIfNull(observation);
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
        var tabs = TabNote(previous.Tabs, next.Tabs);

        // Another tab is another document: its ids say nothing about the tab the model held.
        if (tabs is { Moved: true })
        {
            return lead + tabs.Note + " " + RenderFull(observation, next) + closed;
        }

        // A tab opened is the action's effect, so an unchanged screen blames no control.
        var expectChange = options.ExpectChange && tabs is null;
        if (tabs is not null)
        {
            lead = lead + tabs.Note + " ";
        }

        var diff = Diff(previous, next);
        var changes = diff.Count;

        // A truncated screen is never called unchanged: what it left out is unknown.
        if (changes == 0 && !observation.Truncated)
        {
            return lead + RenderUnchanged(previous.Revision, observation, expectChange, pixels) + closed;
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

    /// <summary>The step's first screen, whole, with its screenshot when the observation carries one.</summary>
    public ScreenOutput Open(ExecutorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation = ForVision(observation, Vision);
        (_screenshot, _screenshotDigest, _pixelsDenied) = (null, null, false);
        Attach(observation);
        return WithScreenshot(observation, Initial(observation));
    }

    /// <summary>
    /// A later screen as the model reads it: the changes since the screen it holds and, once the step is showing pixels, the screenshot too.
    /// On a screen the tree cannot describe (nothing interactive listed) an unchanged tree is not a failed action: what the action did may
    /// be drawn, not listed, so the screenshot is the evidence and no action is blamed. On a screen the tree does describe, an unchanged tree
    /// after an action means the control had no visible effect only when an identical screenshot says so too. A moved screenshot means what
    /// changed is drawn, not listed, and the model is told to read the image; with no screenshot to compare, the model is told only that no
    /// listed node changed.
    /// </summary>
    public ScreenOutput Present(ExecutorObservation observation, ScreenUpdateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation = ForVision(observation, Vision);
        options ??= new ScreenUpdateOptions();
        var compared = Attach(observation);
        var pixels = compared == PixelEvidence.None && _pixelsDenied ? PixelEvidence.Unavailable : compared;
        var text = Render(observation, options with { ExpectChange = ShowingPixels && AgentObservation.InteractiveNodeCount(observation.Text) == 0 ? false : options.ExpectChange }, pixels);
        return WithScreenshot(observation, text);
    }

    /// <summary>
    /// Notes a screenshot that went to the model, with the viewport the screen had: from here on the step is in pixel mode, and a point tool
    /// reads its coordinates against it. Returns what it says against the screenshot the model held, which keeps an unchanged tree from
    /// being read as no effect. A session over MCP notes the shot its <c>screenshot</c> tool showed; its action results carry none.
    /// </summary>
    public PixelEvidence Attach(ExecutorPixels pixels, TraceViewport? viewport)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var digest = AgentObservation.PixelDigest(pixels.Data);
        var previous = _screenshotDigest;
        _screenshot = new ShownScreenshot(pixels, viewport);
        _screenshotDigest = digest;
        return previous is null ? PixelEvidence.None : previous == digest ? PixelEvidence.Unchanged : PixelEvidence.Changed;
    }

    // Masking can be proven again on the next screen; an engine without capture never captures.
    private PixelEvidence Attach(ExecutorObservation observation)
    {
        if (observation.PixelsWithheld == VisionDegradation.UnsupportedCapability)
        {
            _pixelsDenied = true;
        }

        return observation.Pixels is { } pixels ? Attach(pixels, observation.Viewport) : PixelEvidence.None;
    }

    // The rendered screen with its screenshot and the coordinate note, or with the reason the pixels the step asked for did not come, or as is
    // when no pixels were asked for.
    private static ScreenOutput WithScreenshot(ExecutorObservation observation, string text)
    {
        if (observation.Pixels is { } pixels)
        {
            return new ScreenOutput(text + "\n\n" + ScreenshotNote(pixels), pixels);
        }

        return observation.PixelsWithheld is { } withheld ? new ScreenOutput(text + "\n\nNo screenshot: " + WithheldAdvice(withheld)) : new ScreenOutput(text);
    }

    /// <summary>The line under a screenshot: its pixel size, its scale to the viewport's CSS pixels, and the coordinate space the point tools read (upstream's <c>screenshotNote</c>).</summary>
    internal static string ScreenshotNote(ExecutorPixels pixels) =>
        "Screenshot attached: " + pixels.Width.ToString(CultureInfo.InvariantCulture) + " by " + pixels.Height.ToString(CultureInfo.InvariantCulture) + " pixels"
        + (pixels.Scale == 1 ? "" : " (" + pixels.Scale.ToString("R", CultureInfo.InvariantCulture) + " per CSS pixel)")
        + ". Point coordinates (tap_at and the other _at verbs) are pixels of this image: x from the left edge, y from the top edge.";

    /// <summary>Why pixels did not reach the model, and what to do instead (upstream's <c>withheldAdvice</c>).</summary>
    internal static string WithheldAdvice(VisionDegradation code) => code switch
    {
        VisionDegradation.MaskingUnproven => "the engine could not prove every secure field on screen masked (MASKING_UNPROVEN). Work from the tree and tap listed nodes by id.",
        _ => "this engine captures no pixels (UNSUPPORTED_CAPABILITY). Work from the tree and tap listed nodes by id.",
    };

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

    private static string DescribeLocation(ExecutorObservation observation)
    {
        var path = observation.Path is null ? "" : ", path " + observation.Path;
        var tabs = observation.Tabs is null ? "" : ", tab " + observation.Tabs.Active.ToString(CultureInfo.InvariantCulture) + " of " + observation.Tabs.Count.ToString(CultureInfo.InvariantCulture);
        return path + tabs;
    }

    /// <summary>What the model hears when the open tabs changed (upstream's <c>tabNote</c> result): the note, and whether the screen moved to another tab.</summary>
    private sealed record TabChange(string Note, bool Moved);

    /// <summary>
    /// What the model hears when the open tabs changed, or null when they did not (upstream's <c>tabNote</c>). A tab the app opens joins at the
    /// end and stays in the background, so the screen is on the same tab (<c>Moved</c> false). Any other change may have put the screen on
    /// another tab (<c>Moved</c> true): one that closed hands the screen back to another.
    /// </summary>
    private static TabChange? TabNote(ExecutorTabs? previous, ExecutorTabs? next)
    {
        var before = previous ?? new ExecutorTabs(1, 1);
        var after = next ?? new ExecutorTabs(1, 1);
        if (before.Count == after.Count && before.Active == after.Active)
        {
            return null;
        }

        if (after.Count > before.Count && after.Active == before.Active)
        {
            var added = after.Count - before.Count;
            var total = after.Count.ToString(CultureInfo.InvariantCulture);
            var opened = added == 1
                ? "a new tab (tab " + total + " of " + total + ")"
                : added.ToString(CultureInfo.InvariantCulture) + " new tabs (tabs " + (before.Count + 1).ToString(CultureInfo.InvariantCulture) + " to " + total + " of " + total + ")";
            return new TabChange("The app opened " + opened + ". The screen still shows tab " + after.Active.ToString(CultureInfo.InvariantCulture) + "; use switch_tab to see " + (added == 1 ? "it" : "one") + ".", false);
        }

        if (next is null)
        {
            return new TabChange("The other tab" + (before.Count > 2 ? "s" : "") + " closed; the screen shows the one tab still open.", true);
        }

        return new TabChange("The screen shows tab " + after.Active.ToString(CultureInfo.InvariantCulture) + " of " + after.Count.ToString(CultureInfo.InvariantCulture) + " now.", true);
    }

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

        return new ShownScreen(observation.Revision ?? "", order, byId, observation.Truncated, order.Any(line => KeyboardLine().IsMatch(line)), observation.Tabs);
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
