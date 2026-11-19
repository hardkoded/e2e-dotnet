// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E2E.Cli;

/// <summary>
/// .NET-only: the part of the <c>yaml</c> npm library that upstream's <c>cli/init/ci.ts</c> uses to edit its pipeline templates
/// (<c>parseDocument</c>, <c>get</c>, <c>set</c>, <c>delete</c>, <c>commentBefore</c>, <c>spaceBefore</c>, <c>toString</c>). No .NET
/// YAML library keeps comments, so this reads the block YAML the templates in <c>templates/ci/</c> are written in: block maps
/// and sequences, plain and quoted scalars, single-line flow sequences, and <c>|</c> block scalars, each with the comments and
/// blank lines around it. A document prints back as it was read, two spaces to a level, until a node changes.
/// </summary>
internal sealed class YamlDocument
{
    private readonly List<string> _header;
    private readonly List<string> _footer;

    private YamlDocument(List<string> header, YamlNode? contents, List<string> footer)
    {
        _header = header;
        Contents = contents;
        _footer = footer;
    }

    /// <summary>The document's top node: a map for a pipeline.</summary>
    public YamlNode? Contents { get; }

    /// <summary>Reads <paramref name="text"/>, or throws a <see cref="FormatException"/> for YAML this reader does not cover.</summary>
    public static YamlDocument Parse(string text)
    {
        var parser = new YamlParser(text);
        var (header, contents, footer) = parser.ParseDocument();
        return new YamlDocument(header, contents, footer);
    }

    /// <summary>The document as text: no wrapping, and a final newline.</summary>
    public override string ToString()
    {
        var lines = new List<string>(_header);
        if (Contents is not null)
        {
            YamlWriter.Write(Contents, 0, lines);
        }

        lines.AddRange(_footer);
        return string.Join("\n", lines) + "\n";
    }
}

/// <summary>A node of a <see cref="YamlDocument"/>, with the comment and blank lines above it (the <c>commentBefore</c> and <c>spaceBefore</c> of the <c>yaml</c> library).</summary>
internal abstract class YamlNode
{
    /// <summary>The lines above the node: <c>""</c> for a blank line, else a comment that starts with <c>#</c>.</summary>
    internal List<string> Before { get; set; } = [];

    /// <summary>Whether a blank line comes before the node (and its comment).</summary>
    public bool SpaceBefore
    {
        get => Before.Count > 0 && Before[0].Length == 0;
        set
        {
            while (Before.Count > 0 && Before[0].Length == 0)
            {
                Before.RemoveAt(0);
            }

            if (value)
            {
                Before.Insert(0, "");
            }
        }
    }

    /// <summary>The comment above the node, one line to a line, without the <c>#</c>; <c>null</c> for none. Setting it replaces the comment and keeps a blank line above it.</summary>
    public string? CommentBefore
    {
        get
        {
            var lines = Before
                .Where(line => line.Length > 0)
                .Select(line => line.TrimStart('#').TrimStart(' '))
                .ToList();
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }
        set
        {
            var space = SpaceBefore;
            Before.Clear();
            if (space)
            {
                Before.Add("");
            }

            if (value is not null)
            {
                Before.AddRange(value.Split('\n').Select(line => line.Length == 0 ? "#" : "# " + line));
            }
        }
    }

    /// <summary>A node for a C# value: a node as it is, a string as a scalar, a list of strings as a sequence, name and value pairs as a map.</summary>
    public static YamlNode Create(object value) =>
        value switch
        {
            YamlNode node => node,
            string text => new YamlScalar(text),
            IEnumerable<KeyValuePair<string, object>> pairs => YamlMap.Of(pairs),
            IEnumerable<string> items => new YamlSeq(items.Select(item => (YamlNode)new YamlScalar(item))),
            _ => throw new ArgumentException($"No YAML node for a {value.GetType()}.", nameof(value)),
        };
}

/// <summary>One <c>key: value</c> of a <see cref="YamlMap"/>.</summary>
internal sealed class YamlPair(YamlScalar key, YamlNode? value)
{
    /// <summary>The key, which carries the comment and blank line above the pair.</summary>
    public YamlScalar Key { get; } = key;

    /// <summary>The value, or <c>null</c> for <c>key:</c> alone.</summary>
    public YamlNode? Value { get; set; } = value;
}

/// <summary>A block map.</summary>
internal sealed class YamlMap : YamlNode
{
    /// <summary>The pairs, in order.</summary>
    public List<YamlPair> Items { get; } = [];

    /// <summary>A map of <paramref name="pairs"/> in order; each value goes through <see cref="YamlNode.Create"/>.</summary>
    public static YamlMap Of(IEnumerable<KeyValuePair<string, object>> pairs)
    {
        var map = new YamlMap();
        foreach (var (key, value) in pairs)
        {
            map.Items.Add(new YamlPair(new YamlScalar(key), Create(value)));
        }

        return map;
    }

    /// <summary>Whether the map has <paramref name="key"/>.</summary>
    public bool Has(string key) => Find(key) >= 0;

    /// <summary>The value of <paramref name="key"/>, or <c>null</c> when the key is missing.</summary>
    public YamlNode? Get(string key) => Find(key) is var at and >= 0 ? Items[at].Value : null;

    /// <summary>The value of <paramref name="key"/>, which the template guarantees.</summary>
    public T Get<T>(string key)
        where T : YamlNode =>
        Get(key) as T ?? throw new InvalidOperationException($"The CI template has no {typeof(T).Name} at {key}.");

    /// <summary>Sets <paramref name="key"/>: in place when it is there, else at the end.</summary>
    public void Set(string key, object value)
    {
        var node = Create(value);
        if (Find(key) is var at and >= 0)
        {
            // A scalar that takes a scalar's place keeps the comment after it, as the yaml library does.
            if (
                Items[at].Value is YamlScalar { LineComment: { } comment }
                && node is YamlScalar { LineComment: null } next
            )
            {
                next.LineComment = comment;
            }

            Items[at].Value = node;
        }
        else
        {
            Items.Add(new YamlPair(new YamlScalar(key), node));
        }
    }

    /// <summary>Adds <paramref name="key"/> at <paramref name="index"/>, or at the end for -1.</summary>
    public void Insert(int index, string key, object value)
    {
        var pair = new YamlPair(new YamlScalar(key), Create(value));
        if (index < 0)
        {
            Items.Add(pair);
        }
        else
        {
            Items.Insert(index, pair);
        }
    }

    /// <summary>The position of <paramref name="key"/>, or -1.</summary>
    public int IndexOf(string key) => Find(key);

    /// <summary>Removes <paramref name="key"/>; whether it was there.</summary>
    public bool Delete(string key)
    {
        var at = Find(key);
        if (at >= 0)
        {
            Items.RemoveAt(at);
        }

        return at >= 0;
    }

    private int Find(string key) => Items.FindIndex(pair => pair.Key.Value == key);
}

/// <summary>A block sequence.</summary>
internal sealed class YamlSeq : YamlNode
{
    /// <summary>An empty sequence.</summary>
    public YamlSeq() { }

    /// <summary>A sequence of <paramref name="items"/>.</summary>
    public YamlSeq(IEnumerable<YamlNode> items) => Items.AddRange(items);

    /// <summary>The items, in order.</summary>
    public List<YamlNode> Items { get; } = [];
}

/// <summary>How a <see cref="YamlScalar"/> is written.</summary>
internal enum YamlScalarStyle
{
    /// <summary>Bare text.</summary>
    Plain,

    /// <summary>In single or double quotes.</summary>
    Quoted,

    /// <summary>A single-line <c>[a, b]</c> list, kept as written.</summary>
    Flow,

    /// <summary>A <c>|</c> block of lines.</summary>
    Literal,
}

/// <summary>A scalar: its text, and the comment after it on its line.</summary>
internal sealed partial class YamlScalar : YamlNode
{
    private static readonly JsonSerializerOptions Quoting = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Plain text a CI service's YAML reads as a number, a date, or null: hex and octal integers, <c>.inf</c>, <c>.nan</c>, dates, <c>~</c>.</summary>
    private static readonly Regex NonString = new(
        @"^(?:[-+]?0[xob][0-9a-fA-F_]+|[-+]?\.(?:inf|nan)|~|\d{4}-\d{2}-\d{2}.*|[-+]?[0-9][0-9_,]*(?:\.[0-9_]*)?(?:[eE][-+]?[0-9]+)?|[-+]?\.[0-9_]+)$",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1)
    );

    private static readonly string[] Reserved = ["true", "false", "yes", "no", "on", "off", "null", "y", "n", "~"];
    private string _raw;
    private string _value;

    /// <summary>A scalar of <paramref name="value"/>, written plain when that reads back the same, else quoted, and as a block when it has several lines.</summary>
    public YamlScalar(string value)
    {
        _value = value;
        _raw = Render(value, out var style);
        Style = style;
    }

    private YamlScalar(string raw, string value, YamlScalarStyle style)
    {
        _raw = raw;
        _value = value;
        Style = style;
    }

    /// <summary>How the scalar is written.</summary>
    public YamlScalarStyle Style { get; private set; }

    /// <summary>The comment after the scalar on its line, with its <c>#</c>.</summary>
    public string? LineComment { get; set; }

    /// <summary>The text the scalar stands for. Setting it writes the new text the way a new scalar is.</summary>
    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            _raw = Render(value, out var style);
            Style = style;
        }
    }

    /// <summary>The scalar as written on its line (a block scalar's lines are in <see cref="Value"/>).</summary>
    internal string Raw => _raw;

    internal static YamlScalar FromSource(string raw, YamlScalarStyle style) =>
        new(raw, style == YamlScalarStyle.Quoted ? Unquote(raw) : raw, style);

    internal static YamlScalar Literal(string value) => new(string.Empty, value, YamlScalarStyle.Literal);

    private static string Render(string value, out YamlScalarStyle style)
    {
        if (
            value.Contains('\n', StringComparison.Ordinal)
            && !value.EndsWith("\n\n", StringComparison.Ordinal)
            && value != "\n"
        )
        {
            style = YamlScalarStyle.Literal;
            return string.Empty;
        }

        if (IsPlain(value))
        {
            style = YamlScalarStyle.Plain;
            return value;
        }

        style = YamlScalarStyle.Quoted;
        return JsonSerializer.Serialize(value, Quoting);
    }

    private static bool IsPlain(string value) =>
        value.Length > 0
        && value.Trim() == value
        && "-?:,[]{}#&*!|>'\"%@`".IndexOf(value[0], StringComparison.Ordinal) < 0
        && !value.Contains(": ", StringComparison.Ordinal)
        && !value.Contains(" #", StringComparison.Ordinal)
        && !value.EndsWith(':')
        && !Reserved.Contains(value, StringComparer.OrdinalIgnoreCase)
        && !value.Contains('\n', StringComparison.Ordinal)
        && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
        && !NonString.IsMatch(value);

    private static string Unquote(string raw) =>
        raw[0] == '"'
            ? JsonSerializer.Deserialize<string>(raw) ?? string.Empty
            : raw[1..^1].Replace("''", "'", StringComparison.Ordinal);
}

/// <summary>Reads block YAML into nodes, keeping the comments and blank lines.</summary>
internal sealed partial class YamlParser
{
    private static readonly Regex Key = KeyPattern();
    private readonly List<(int Indent, string Text)> _lines;
    private readonly List<string> _raw;
    private int _at;

    public YamlParser(string text)
    {
        _raw = [.. text.TrimEnd('\n').Split('\n').Select(line => line.TrimEnd('\r'))];
        _lines =
        [
            .. _raw.Select(line =>
            {
                var trimmed = line.TrimEnd();
                var content = trimmed.TrimStart(' ');
                return (trimmed.Length - content.Length, content);
            }),
        ];
    }

    public (List<string> Header, YamlNode? Contents, List<string> Footer) ParseDocument()
    {
        var header = new List<string>();
        var trivia = Trivia();
        // A comment block followed by a blank line belongs to the document; one right above the first key belongs to the key.
        var lastBlank = trivia.FindLastIndex(line => line.Length == 0);
        if (lastBlank >= 0)
        {
            header.AddRange(trivia.Take(lastBlank + 1));
            trivia.RemoveRange(0, lastBlank + 1);
        }

        YamlNode? contents = null;
        if (_at < _lines.Count)
        {
            contents = ParseBlock(_lines[_at].Indent, trivia);
        }
        else
        {
            header.AddRange(trivia);
        }

        var footer = Trivia();
        if (_at < _lines.Count)
        {
            throw Fail("YAML this reader does not cover", _at);
        }

        return (header, contents, footer);
    }

    private FormatException Fail(string message, int line) => new($"{message} (line {line + 1})");

    /// <summary>Consumes the blank and comment lines at the cursor.</summary>
    private List<string> Trivia()
    {
        var trivia = new List<string>();
        while (_at < _lines.Count && (_lines[_at].Text.Length == 0 || _lines[_at].Text[0] == '#'))
        {
            trivia.Add(_lines[_at].Text);
            _at++;
        }

        return trivia;
    }

    private static bool IsDash(string text) => text == "-" || text.StartsWith("- ", StringComparison.Ordinal);

    /// <summary>Whether the cursor is on a content line at <paramref name="indent"/> that is (or is not) a sequence item.</summary>
    private bool AtItem(int indent, bool seq) =>
        _at < _lines.Count && _lines[_at].Indent == indent && IsDash(_lines[_at].Text) == seq;

    private YamlNode ParseBlock(int indent, List<string> trivia) =>
        IsDash(_lines[_at].Text) ? ParseSeq(indent, trivia) : ParseMap(indent, trivia);

    private YamlMap ParseMap(int indent, List<string> firstTrivia)
    {
        var map = new YamlMap();
        var trivia = firstTrivia;
        while (true)
        {
            var line = _lines[_at];
            var match = Key.Match(line.Text);
            if (!match.Success)
            {
                throw Fail("Not a block map key", _at);
            }

            var keyText = match.Groups[1].Value;
            var key = YamlScalar.FromSource(
                keyText,
                keyText[0] is '"' or '\'' ? YamlScalarStyle.Quoted : YamlScalarStyle.Plain
            );
            key.Before = trivia;
            _at++;
            map.Items.Add(new YamlPair(key, ParseValue(indent, line.Text[match.Length..], key)));
            var mark = _at;
            trivia = Trivia();
            if (!AtItem(indent, seq: false))
            {
                // The comments and blank lines belong to whatever comes next, in this block or the one around it.
                _at = mark;
                return map;
            }
        }
    }

    private YamlNode? ParseValue(int keyIndent, string rest, YamlScalar key)
    {
        if (rest.Length > 0 && rest[0] == '#')
        {
            // `key: # comment` over a block: the comment stays on the key's line.
            key.LineComment = rest;
            rest = string.Empty;
        }

        if (rest.Length > 0)
        {
            return ParseInline(rest, keyIndent);
        }

        var mark = _at;
        var trivia = Trivia();
        if (_at < _lines.Count)
        {
            var next = _lines[_at];
            if (next.Indent > keyIndent || (next.Indent == keyIndent && IsDash(next.Text)))
            {
                return ParseBlock(next.Indent, trivia);
            }
        }

        _at = mark;
        return null;
    }

    private YamlSeq ParseSeq(int indent, List<string> firstTrivia)
    {
        var seq = new YamlSeq();
        var trivia = firstTrivia;
        while (true)
        {
            var line = _lines[_at];
            var rest = line.Text.Length == 1 ? string.Empty : line.Text[2..].TrimStart(' ');
            if (rest.Length == 0 || rest[0] == '#')
            {
                throw Fail("A block sequence item that starts on the next line", _at);
            }

            YamlNode item;
            if (Key.IsMatch(rest))
            {
                // `- key: value` is a map whose first pair sits on the dash's line.
                _lines[_at] = (indent + (line.Text.Length - rest.Length), rest);
                item = ParseMap(_lines[_at].Indent, []);
            }
            else
            {
                _at++;
                item = ParseInline(rest, indent)!;
            }

            item.Before = trivia;
            seq.Items.Add(item);
            var mark = _at;
            trivia = Trivia();
            if (!AtItem(indent, seq: true))
            {
                _at = mark;
                return seq;
            }
        }
    }

    /// <summary>A scalar after <c>key: </c> or <c>- </c>, on the line before the cursor; the cursor is just past that line.</summary>
    private YamlNode? ParseInline(string rest, int parentIndent)
    {
        if (rest[0] is '|' or '>')
        {
            if (rest[0] == '>' || rest.Length > 2 || (rest.Length == 2 && rest[1] != '-'))
            {
                throw Fail("A block scalar header this reader does not cover", _at - 1);
            }

            return ParseLiteral(parentIndent, strip: rest.Length == 2);
        }

        var (text, comment) = SplitComment(rest);
        var style = text[0] switch
        {
            '"' or '\'' => YamlScalarStyle.Quoted,
            '[' => YamlScalarStyle.Flow,
            _ => YamlScalarStyle.Plain,
        };
        if (style == YamlScalarStyle.Flow && !text.EndsWith(']'))
        {
            throw Fail("A flow list over several lines", _at - 1);
        }

        var scalar = YamlScalar.FromSource(text, style);
        scalar.LineComment = comment;
        return scalar;
    }

    private YamlScalar ParseLiteral(int parentIndent, bool strip)
    {
        var lines = new List<(int Indent, string Text)>();
        while (_at < _lines.Count && (_lines[_at].Text.Length == 0 || _lines[_at].Indent > parentIndent))
        {
            // The block's own text keeps its trailing spaces.
            lines.Add(
                _lines[_at].Text.Length == 0 ? _lines[_at] : (_lines[_at].Indent, _raw[_at][_lines[_at].Indent..])
            );
            _at++;
        }

        // Blank lines after the last line of the block belong to what follows.
        while (lines.Count > 0 && lines[^1].Text.Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
            _at--;
        }

        if (lines.Count == 0)
        {
            throw Fail("An empty block scalar", _at - 1);
        }

        var indent = lines.Where(line => line.Text.Length > 0).Min(line => line.Indent);
        var text = string.Join(
            "\n",
            lines.Select(line => line.Text.Length == 0 ? "" : new string(' ', line.Indent - indent) + line.Text)
        );
        return YamlScalar.Literal(strip ? text : text + "\n");
    }

    /// <summary>A scalar's text and the comment after it: a quoted scalar ends at its closing quote, a plain one at the first <c> #</c>.</summary>
    private (string Text, string? Comment) SplitComment(string rest)
    {
        if (rest[0] is '"' or '\'')
        {
            var quote = rest[0];
            var end = 1;
            while (end < rest.Length)
            {
                if (quote == '"' && rest[end] == '\\')
                {
                    end += 2;
                    continue;
                }

                if (rest[end] == quote)
                {
                    if (quote == '\'' && end + 1 < rest.Length && rest[end + 1] == '\'')
                    {
                        end += 2;
                        continue;
                    }

                    break;
                }

                end++;
            }

            if (end >= rest.Length)
            {
                throw Fail("A quoted scalar over several lines", _at - 1);
            }

            var tail = rest[(end + 1)..].Trim();
            if (tail.Length > 0 && tail[0] != '#')
            {
                throw Fail("Text after a quoted scalar", _at - 1);
            }

            return (rest[..(end + 1)], tail.Length == 0 ? null : tail);
        }

        if (rest[0] == '[')
        {
            // A flow list ends at its closing bracket; a # inside a quoted element is not a comment.
            char? open = null;
            for (var i = 1; i < rest.Length; i++)
            {
                if (open is { } q)
                {
                    open = rest[i] == q ? null : open;
                }
                else if (rest[i] is '"' or '\'')
                {
                    open = rest[i];
                }
                else if (rest[i] == ']')
                {
                    var tail = rest[(i + 1)..].Trim();
                    if (tail.Length > 0 && tail[0] != '#')
                    {
                        throw Fail("Text after a flow list", _at - 1);
                    }

                    return (rest[..(i + 1)], tail.Length == 0 ? null : tail);
                }
            }

            throw Fail("A flow list over several lines", _at - 1);
        }

        var hash = rest.IndexOf(" #", StringComparison.Ordinal);
        return hash < 0 ? (rest.TrimEnd(), null) : (rest[..hash].TrimEnd(), rest[(hash + 1)..]);
    }

    [GeneratedRegex(@"^((?:[A-Za-z0-9_.\-]+)|(?:""[^""]*"")|(?:'[^']*')):(?: +|$)", RegexOptions.None, 1000)]
    private static partial Regex KeyPattern();
}

/// <summary>Prints nodes back as block YAML.</summary>
internal static class YamlWriter
{
    public static void Write(YamlNode node, int indent, List<string> lines)
    {
        switch (node)
        {
            case YamlMap map:
                WriteMap(map, indent, lines);
                break;
            case YamlSeq seq:
                WriteSeq(seq, indent, lines);
                break;
            default:
                throw new InvalidOperationException("A document's top node is a map or a sequence.");
        }
    }

    private static void WriteBefore(YamlNode node, int indent, List<string> lines) =>
        lines.AddRange(node.Before.Select(line => line.Length == 0 ? "" : new string(' ', indent) + line));

    private static void WriteMap(YamlMap map, int indent, List<string> lines)
    {
        var pad = new string(' ', indent);
        foreach (var pair in map.Items)
        {
            WriteBefore(pair.Key, indent, lines);
            var head =
                pad
                + (pair.Key.Style == YamlScalarStyle.Quoted ? pair.Key.Raw : pair.Key.Value)
                + ":"
                + (pair.Key.LineComment is null || pair.Value is YamlScalar ? "" : " " + pair.Key.LineComment);
            switch (pair.Value)
            {
                case null:
                    lines.Add(head);
                    break;
                case YamlScalar scalar when scalar.Style == YamlScalarStyle.Literal:
                    lines.Add(head + " " + LiteralHeader(scalar));
                    WriteLiteral(scalar, indent + 2, lines);
                    break;
                case YamlScalar scalar:
                    lines.Add(head + " " + Inline(scalar));
                    break;
                case YamlMap child:
                    lines.Add(head);
                    WriteMap(child, indent + 2, lines);
                    break;
                case YamlSeq child:
                    lines.Add(head);
                    WriteSeq(child, indent + 2, lines);
                    break;
            }
        }
    }

    private static void WriteSeq(YamlSeq seq, int indent, List<string> lines)
    {
        var pad = new string(' ', indent);
        foreach (var item in seq.Items)
        {
            WriteBefore(item, indent, lines);
            switch (item)
            {
                case YamlScalar scalar when scalar.Style == YamlScalarStyle.Literal:
                    lines.Add(pad + "- " + LiteralHeader(scalar));
                    WriteLiteral(scalar, indent + 2, lines);
                    break;
                case YamlScalar scalar:
                    lines.Add(pad + "- " + Inline(scalar));
                    break;
                case YamlMap map:
                    // The map's first line takes the dash's place of its indentation.
                    var inner = new List<string>();
                    WriteMap(map, indent + 2, inner);
                    inner[0] = pad + "- " + inner[0][(indent + 2)..];
                    lines.AddRange(inner);
                    break;
                default:
                    throw new InvalidOperationException("A sequence inside a sequence is not covered.");
            }
        }
    }

    private static string Inline(YamlScalar scalar) =>
        scalar.Raw + (scalar.LineComment is null ? "" : " " + scalar.LineComment);

    private static string LiteralHeader(YamlScalar scalar) => scalar.Value.EndsWith('\n') ? "|" : "|-";

    private static void WriteLiteral(YamlScalar scalar, int indent, List<string> lines)
    {
        var pad = new string(' ', indent);
        var text = scalar.Value.EndsWith('\n') ? scalar.Value[..^1] : scalar.Value;
        lines.AddRange(text.Split('\n').Select(line => line.Length == 0 ? "" : pad + line));
    }
}
