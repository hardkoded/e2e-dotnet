// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// What the step showed the model, which a typed value may have been read off (upstream's <c>StepEvidence</c>).
/// </summary>
/// <param name="Shown">
/// What the screens displayed, one string per node (<see cref="ObservationFeed.ShownText"/>): the fields that echo a typed value are
/// already left out of it.
/// </param>
/// <param name="Pixels">
/// Whether the model received a screenshot in this step. Text drawn in pixels is not in <paramref name="Shown"/>, so a value that is
/// not literal may have been read off the image and counts as derived.
/// </param>
/// <param name="InputPurpose">
/// The purpose of the field the value went into: the one the engine reported for it (<c>SemanticNode.InputPurpose</c>), or, for typing
/// into the focused field, which names none, <c>one-time-code</c> once a screen of the step listed such a field
/// (<see cref="ObservationFeed.OneTimeCodeShown"/>).
/// </param>
internal sealed record StepEvidence(
    IEnumerable<string>? Shown = null,
    bool? Pixels = null,
    string? InputPurpose = null
);

/// <summary>
/// Upstream's <c>agent/derived.ts</c>: whether a value the executor typed was derived at run time, and by which rule, so the trace must
/// not replay it.
/// <para>
/// A value the instruction or the params spell out ("Nimbus Paper Co", 4.25, a param's string) is the step's literal input and
/// replays verbatim. A value the step read off the screen (a code, a reference the app minted), or reckoned from the calendar,
/// belongs to THIS run: the next run's app may issue another, and a replay that typed the old one would act out a stale flow and
/// self-finalize on the wrong state. Such an action is recorded as a gap instead: replay performs everything before it and hands
/// the step over, so the executor derives the value afresh from what it sees and remembers. A value the model composed itself (a
/// plausible name, an email, a note) is the flow's data as much as a literal would be, and replays.
/// </para>
/// <para>
/// Each rule answers with its own <see cref="DerivedReason"/>, so the report can say which one fired and each is pinned on its own:
/// <c>Pixels</c> (the model was shown a screenshot this step, whose text the tree does not list, so any value that is not literal
/// may have been read off the image), <c>WholeNode</c> (the value is the whole of what some node said, its name or its text,
/// whitespace-normalized), <c>MintedToken</c> (the value is data-shaped, one token with a digit, and some node showed it as a word of
/// its own; the digit is a glyph test standing in for provenance, so "1" typed beside a "Row 1" label is flagged too and the executor
/// runs that part live; also any value typed into a field the app marks as a one-time code: the app issues that code per run, and a code
/// split over one box per digit puts a single digit in each fill, which no screen shows as a word of its own; typing into the focused field names no field, so in a step whose screens listed a one-time-code field its value is judged as if it went into one), and <c>Date</c> (a date or a clock time, which the model reckons from today).
/// </para>
/// <para>
/// Values typed in a row into several fields are also judged joined (<see cref="DerivedJoinReason"/>), after each piece is judged on its own above: a code the screen showed and the model typed three and three, or one digit per box, is the whole code once its pieces join, even where no one piece is a word of the screen. The recording becomes a gap from the first piece on.
/// </para>
/// <para>
/// A bare word inside a sentence is none of these: "one" typed on a page that lists "Row one" is the model's own choice, and the
/// recording replays it. So is a name inside a greeting ("Jane" under "Welcome back, Jane"); when that name was in fact read off the
/// screen, the replay types last run's value and self-finalizes, backstopped only when the value resurfaces in the end anchors.
/// </para>
/// </summary>
internal static partial class Derived
{
    /// <summary>
    /// Upstream's <c>derivedReason</c>: the rule that makes a typed value this run's data, or null for a value that replays: one the
    /// instruction or the params spell out, or one the model composed itself.
    /// </summary>
    public static DerivedReason? DerivedReason(
        string value,
        string instruction,
        JsonObject? parameters,
        StepEvidence? evidence = null
    )
    {
        evidence ??= new StepEvidence();
        var needle = Normalize(value);
        if (needle.Length == 0)
        {
            return null;
        }

        if (evidence.InputPurpose == "one-time-code")
        {
            // One box per digit puts a single character in each fill, and "the 6-digit code" spells no code: only a longer value the
            // step spells out whole replays, so neither a lone digit nor a chunk of some other number ever replays as last run's code.
            return needle.EnumerateRunes().Count() > 1 && SpellsOutWhole(needle, instruction, parameters)
                ? null
                : E2E.DerivedReason.MintedToken;
        }

        if (IsLiteral(needle, instruction, parameters))
        {
            return null;
        }

        if (evidence.Pixels == true)
        {
            return E2E.DerivedReason.Pixels;
        }

        return ShownReason(needle, evidence.Shown ?? []) ?? (DateOrTime.IsMatch(value) ? E2E.DerivedReason.Date : null);
    }

    /// <summary>
    /// Upstream's <c>derivedJoinReason</c>: the rule that makes values typed in a row into several fields this run's data once joined, or
    /// null when the join replays. <paramref name="joined"/> is the values with no separator, as a code split one digit per box (or three
    /// and three) reads whole. No piece of such a code is a word the screen showed, so every piece passes <see cref="DerivedReason"/>,
    /// which already applied the date, pixel, and field-purpose rules to each. The join meets only the screen rules, and a join the
    /// instruction or the params spell out is literal.
    /// </summary>
    public static DerivedReason? DerivedJoinReason(
        string joined,
        string instruction,
        JsonObject? parameters,
        IEnumerable<string> shown
    )
    {
        var needle = Normalize(joined);
        if (needle.Length == 0 || IsLiteral(needle, instruction, parameters))
        {
            return null;
        }

        return ShownReason(needle, shown);
    }

    // Upstream's shownReason: whether a screen of the step showed the value, as the whole of a node or, for a data-shaped value, as a word
    // of its own inside one. A node that says exactly the value wins over one that holds it as a word.
    private static DerivedReason? ShownReason(string needle, IEnumerable<string> shown)
    {
        var dataShaped = IsDataShaped(needle);
        var mintedToken = false;
        foreach (var text in shown)
        {
            var normalized = Normalize(text);
            if (string.Equals(normalized, needle, StringComparison.Ordinal))
            {
                return E2E.DerivedReason.WholeNode;
            }

            if (dataShaped && HasWord(normalized, needle))
            {
                mintedToken = true;
            }
        }

        return mintedToken ? E2E.DerivedReason.MintedToken : null;
    }

    // Whether the instruction or a param spells the value out: the step's literal input, which replays verbatim.
    private static bool IsLiteral(string needle, string instruction, JsonObject? parameters) =>
        Normalize(instruction).Contains(needle, StringComparison.Ordinal)
        || ParamStrings(parameters).Any(text => Normalize(text).Contains(needle, StringComparison.Ordinal));

    // Whether the instruction carries the value as a word of its own, or a param is that value: the stricter reading of a literal a
    // one-time-code field gets, where a chunk of a longer number is no spelling of the code.
    private static bool SpellsOutWhole(string needle, string instruction, JsonObject? parameters) =>
        HasWord(Normalize(instruction), needle)
        || ParamStrings(parameters).Any(text => string.Equals(Normalize(text), needle, StringComparison.Ordinal));

    // Whether a value looks like data rather than words: one token, no whitespace, with a digit in it. `TK-4972`, `123`, `n20123`
    // qualify; `Jane` and `one` do not, so those count as read off the screen only when a node says exactly that and nothing more.
    private static bool IsDataShaped(string needle) => !Whitespace().IsMatch(needle) && Number().IsMatch(needle);

    // Whether the text carries the value as a whole token: "123" inside "n20123" is not the screen showing it.
    private static bool HasWord(string text, string needle)
    {
        var from = 0;
        while (true)
        {
            var at = text.IndexOf(needle, from, StringComparison.Ordinal);
            if (at == -1)
            {
                return false;
            }

            var end = at + needle.Length;
            if ((at == 0 || !IsWordChar(text, at - 1)) && (end >= text.Length || !IsWordChar(text, end)))
            {
                return true;
            }

            from = at + 1;
        }
    }

    private static bool IsWordChar(string text, int index) => WordChar().IsMatch(text.AsSpan(index, 1));

    // A date in ISO, numeric, or month-name form, or a clock time: the model reckons these from today, which moves.
    // ECMAScript semantics, as upstream's regular expression has: \b and \d see ASCII only.
    private static readonly Regex DateOrTime = new(
        string.Join(
            '|',
            Dates.IsoDate,
            @"\b\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}\b",
            Dates.ClockTime,
            Dates.MonthDayDate,
            Dates.DayMonthDate
        ),
        RegexOptions.IgnoreCase | RegexOptions.ECMAScript
    );

    [GeneratedRegex(@"[\p{L}\p{N}]")]
    private static partial Regex WordChar();

    [GeneratedRegex(@"\s")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\p{N}")]
    private static partial Regex Number();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // Every string and number a params object carries, at any depth.
    private static List<string> ParamStrings(JsonObject? parameters)
    {
        var output = new List<string>();
        if (parameters is not null)
        {
            Visit(parameters, output);
        }

        return output;
    }

    private static void Visit(JsonNode? value, List<string> output)
    {
        switch (value)
        {
            case JsonObject map:
                foreach (var item in map)
                {
                    Visit(item.Value, output);
                }

                break;
            case JsonArray list:
                foreach (var item in list)
                {
                    Visit(item, output);
                }

                break;
            case JsonValue leaf when leaf.GetValueKind() == JsonValueKind.String:
                output.Add(leaf.GetValue<string>());
                break;
            case JsonValue leaf
                when leaf.GetValueKind() is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                output.Add(leaf.ToJsonString());
                break;
        }
    }

    private static string Normalize(string text) =>
        Spaces().Replace(text.ToLower(CultureInfo.InvariantCulture), " ").Trim();
}
