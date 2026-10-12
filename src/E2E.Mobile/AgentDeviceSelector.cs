// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>Picks the nodes one compiled selector matches, in document order.</summary>
public delegate IReadOnlyList<ProjectedNode> CompiledSelector(IReadOnlyList<ProjectedNode> entries);

/// <summary>
/// agent-device selector support for the <c>selector</c> locator expression:
/// <c>screen.Locator("id=save role=button")</c> and the structural hint on every observed node. The
/// grammar is agent-device's own (<c>parseSelectorChain</c>), so a selector that works on the agent-device
/// CLI works here unchanged; matching runs against the projected snapshot, one immediate pass, every match
/// returned.
/// </summary>
public static partial class AgentDeviceSelector
{
    private static readonly HashSet<string> TextKeys = ["id", "role", "text", "label", "value", "appname", "windowtitle"];

    private static readonly HashSet<string> FlagKeys = ["visible", "hidden", "editable", "selected", "focused", "enabled", "hittable"];

    /// <summary>
    /// Compiles one selector string. Alternatives (agent-device's fallback chain) are tried in order and
    /// the first that matches anything wins, mirroring how agent-device itself resolves a chain; a term's
    /// text compares exactly after whitespace collapsing, like every other text rule in the runner.
    /// </summary>
    public static CompiledSelector Compile(string raw)
    {
        List<List<Term>> chain;
        try
        {
            chain = ParseChain(raw);
        }
        catch (AgentDeviceError cause)
        {
            throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"invalid agent-device selector {JsonText.Quote(raw)}: {DeviceErrors.Message(cause)}",
                retryable: false,
                cause);
        }

        return entries =>
        {
            foreach (var alternative in chain)
            {
                var matches = entries.Where(entry => alternative.All(term => MatchesTerm(entry, term))).ToList();
                if (matches.Count > 0)
                {
                    return matches;
                }
            }

            return [];
        };
    }

    private static List<List<Term>> ParseChain(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            throw Invalid("Selector expression cannot be empty");
        }

        return SplitFallbacks(text).Select(ParseSegment).ToList();
    }

    /// <summary>Splits on <c>||</c> outside quotes.</summary>
    private static List<string> SplitFallbacks(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        for (var position = 0; position < text.Length; position++)
        {
            var character = text[position];
            if (character is '"' or '\'' && !IsEscaped(text, position))
            {
                quote = ToggleQuote(quote, character);
                current.Append(character);
                continue;
            }

            if (quote is null && character == '|' && position + 1 < text.Length && text[position + 1] == '|')
            {
                var part = current.ToString().Trim();
                if (part.Length == 0)
                {
                    throw Invalid($"Invalid selector fallback expression: {text}");
                }

                parts.Add(part);
                current.Clear();
                position++;
                continue;
            }

            current.Append(character);
        }

        var last = current.ToString().Trim();
        if (last.Length == 0)
        {
            throw Invalid($"Invalid selector fallback expression: {text}");
        }

        parts.Add(last);
        return parts;
    }

    private static List<Term> ParseSegment(string segment)
    {
        var text = segment.Trim();
        if (text.Length == 0)
        {
            throw Invalid("Selector segment cannot be empty");
        }

        var tokens = SplitTerms(text);
        if (tokens.Count == 0)
        {
            throw Invalid($"Invalid selector segment: {segment}");
        }

        return tokens.Select(ParseTerm).ToList();
    }

    /// <summary>Splits on whitespace outside quotes.</summary>
    private static List<string> SplitTerms(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        for (var position = 0; position < text.Length; position++)
        {
            var character = text[position];
            if (character is '"' or '\'' && !IsEscaped(text, position))
            {
                quote = ToggleQuote(quote, character);
                current.Append(character);
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                if (current.ToString().Trim().Length > 0)
                {
                    tokens.Add(current.ToString().Trim());
                }

                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (quote is not null)
        {
            throw Invalid($"Unclosed quote in selector: {text}");
        }

        if (current.ToString().Trim().Length > 0)
        {
            tokens.Add(current.ToString().Trim());
        }

        return tokens;
    }

    private static Term ParseTerm(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            throw Invalid("Empty selector term");
        }

        var equals = text.IndexOf('=', StringComparison.Ordinal);
        if (equals == -1)
        {
            var bare = text.ToLowerInvariant();
            return FlagKeys.Contains(bare) ? new Term(bare, "true", true) : throw Invalid($"Invalid selector term \"{raw}\", expected key=value");
        }

        var key = text[..equals].Trim().ToLowerInvariant();
        var value = text[(equals + 1)..].Trim();
        if (!TextKeys.Contains(key) && !FlagKeys.Contains(key))
        {
            throw Invalid($"Unknown selector key: {key}");
        }

        if (value.Length == 0)
        {
            throw Invalid($"Missing selector value for key: {key}");
        }

        if (FlagKeys.Contains(key))
        {
            return Unquote(value).ToLowerInvariant() switch
            {
                "true" => new Term(key, "true", true),
                "false" => new Term(key, "false", false),
                _ => throw Invalid($"Invalid boolean value for {key}: {value}"),
            };
        }

        return new Term(key, Unquote(value), null);
    }

    /// <summary>A term value without its surrounding quotes, with the escapes inside them read.</summary>
    private static string Unquote(string raw)
    {
        var text = raw.Trim();
        var quoted = text.Length > 0 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\''));
        if (!quoted)
        {
            return text;
        }

        return text.Length < 2 ? "" : Unescape(text[1..^1]);
    }

    private static string Unescape(string text)
    {
        var result = new StringBuilder();
        var position = 0;
        while (position < text.Length)
        {
            var character = text[position];
            if (character != '\\' || position + 1 >= text.Length)
            {
                result.Append(character);
                position++;
                continue;
            }

            var next = text[position + 1];
            var simple = next switch
            {
                '"' => '"',
                '\'' => '\'',
                '\\' => '\\',
                '/' => '/',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => default(char?),
            };
            if (simple is { } plain)
            {
                result.Append(plain);
                position += 2;
                continue;
            }

            if (next == 'u' && position + 6 <= text.Length && HexQuad().IsMatch(text.AsSpan(position + 2, 4)))
            {
                result.Append((char)int.Parse(text.AsSpan(position + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                position += 6;
                continue;
            }

            result.Append(character);
            position++;
        }

        return result.ToString();
    }

    private static char? ToggleQuote(char? current, char character) =>
        current is null ? character : current == character ? null : current;

    /// <summary>True when an odd number of backslashes precede the character.</summary>
    private static bool IsEscaped(string text, int position)
    {
        var slashes = 0;
        for (var before = position - 1; before >= 0 && text[before] == '\\'; before--)
        {
            slashes++;
        }

        return slashes % 2 == 1;
    }

    private static AgentDeviceError Invalid(string message) => new("INVALID_ARGS", message);

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    private static bool TextEquals(string? actual, string expected) => actual is not null && Normalize(actual) == Normalize(expected);

    private static bool MatchesTerm(ProjectedNode entry, Term term)
    {
        var states = entry.Node.States;
        var flag = term.Flag == true;
        switch (term.Key)
        {
            case "id":
                return TextEquals(entry.Raw.Identifier, term.Value);
            case "role":
                var wanted = SnapshotProjection.NormalizeKind(term.Value);
                return entry.Kind == wanted || entry.Node.Role == wanted;
            case "text":
                return TextEquals(entry.Raw.Label, term.Value) || TextEquals(entry.Raw.Value, term.Value);
            case "label":
                return TextEquals(entry.Raw.Label, term.Value);
            case "value":
                return TextEquals(entry.Raw.Value, term.Value);
            case "appname":
                return TextEquals(entry.Raw.AppName, term.Value);
            case "windowtitle":
                return TextEquals(entry.Raw.WindowTitle, term.Value);
            case "visible":
                return !states.Hidden == flag;
            case "hidden":
                return states.Hidden == flag;
            case "editable":
                return (entry.Node.Role == "textbox") == flag;
            case "selected":
                return states.Selected == flag;
            case "focused":
                return states.Focused == flag;
            case "enabled":
                return !states.Disabled == flag;
            case "hittable":
                return (entry.Raw.Hittable != false) == flag;
            default:
                return false;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[0-9a-fA-F]{4}$")]
    private static partial Regex HexQuad();

    /// <summary>One selector term. <see cref="Flag"/> is set for the keys that take a boolean.</summary>
    private sealed record Term(string Key, string Value, bool? Flag);
}
