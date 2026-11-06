// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
internal sealed record StepEvidence(IEnumerable<string>? Shown = null, bool? Pixels = null);

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
/// runs that part live), and <c>Date</c> (a date or a clock time, which the model reckons from today).
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
    public static DerivedReason? DerivedReason(string value, string instruction, JsonObject? parameters, StepEvidence? evidence = null)
    {
        evidence ??= new StepEvidence();
        var needle = Normalize(value);
        if (needle.Length == 0 || IsLiteral(needle, instruction, parameters))
        {
            return null;
        }

        if (evidence.Pixels == true)
        {
            return E2E.DerivedReason.Pixels;
        }

        var dataShaped = IsDataShaped(needle);
        var mintedToken = false;
        foreach (var text in evidence.Shown ?? [])
        {
            var shown = Normalize(text);
            if (string.Equals(shown, needle, StringComparison.Ordinal))
            {
                return E2E.DerivedReason.WholeNode;
            }

            if (dataShaped && HasWord(shown, needle))
            {
                mintedToken = true;
            }
        }

        if (mintedToken)
        {
            return E2E.DerivedReason.MintedToken;
        }

        return DateOrTime().IsMatch(value) ? E2E.DerivedReason.Date : null;
    }

    // Whether the instruction or a param spells the value out: the step's literal input, which replays verbatim.
    private static bool IsLiteral(string needle, string instruction, JsonObject? parameters) =>
        Normalize(instruction).Contains(needle, StringComparison.Ordinal) || ParamStrings(parameters).Any(text => Normalize(text).Contains(needle, StringComparison.Ordinal));

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

    // A month by name or abbreviation, with or without a trailing period.
    private const string Month = @"(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?";
    private const string Day = @"\d{1,2}(?:st|nd|rd|th)?";

    // A date in ISO, numeric, or month-name form, or a clock time: the model reckons these from today, which moves.
    private const string DateOrTimePattern =
        @"\b\d{4}-\d{2}-\d{2}\b"
        + @"|\b\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}\b"
        + @"|\b\d{1,2}:\d{2}(?::\d{2})?\b"
        + @"|\b" + Month + " " + Day + @"(?:,? \d{4})?\b"
        + @"|\b" + Day + " " + Month + @"(?:,? \d{4})?\b";

    // ECMAScript semantics, as upstream's regular expression has: \b and \d see ASCII only.
    [GeneratedRegex(DateOrTimePattern, RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex DateOrTime();

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
            case JsonValue leaf when leaf.GetValueKind() is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                output.Add(leaf.ToJsonString());
                break;
        }
    }

    private static string Normalize(string text) => Spaces().Replace(text.ToLower(CultureInfo.InvariantCulture), " ").Trim();
}
