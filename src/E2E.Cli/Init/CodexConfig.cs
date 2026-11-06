// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace E2E.Cli;

/// <summary>
/// Upstream's <c>cli/init/codex-config.ts</c>: reads and updates Codex's project settings (<c>.codex/config.toml</c>). The
/// port parses with Tomlyn, which gives the source spans upstream gets from <c>toml-eslint-parser</c>, so an update replaces or
/// inserts only the launch values and keeps comments, formatting, and every other setting.
/// </summary>
internal static class CodexConfig
{
    /// <summary>Reads Codex project settings as TOML 1.0. Throws when the text is not TOML or <c>mcp_servers</c> / <c>mcp_servers.e2e</c> is not a table.</summary>
    public static JsonObject ReadCodexConfig(string text)
    {
        var document = ToJson(TomlSerializer.Deserialize<TomlTable>(text)) as JsonObject ?? new JsonObject();
        if (document["mcp_servers"] is { } servers)
        {
            if (servers is not JsonObject table)
            {
                throw new InvalidOperationException("mcp_servers must be a TOML table");
            }

            if (table["e2e"] is { } server && server is not JsonObject)
            {
                throw new InvalidOperationException("mcp_servers.e2e must be a TOML table");
            }
        }

        return document;
    }

    /// <summary>Updates only the launch values, preserving comments, formatting, and other settings.</summary>
    public static string UpdateCodexConfig(string text, string command, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(args);
        var content = text;
        if (content.Trim().Length == 0)
        {
            content += (content.EndsWith('\n') || content.Length == 0 ? "" : "\n") + "[mcp_servers.e2e]\n";
        }

        content = SetLaunchValue(content, ["mcp_servers", "e2e", "command"], JsonValue.Create(command));
        content = SetLaunchValue(content, ["mcp_servers", "e2e", "args"], new JsonArray([.. args.Select(arg => (JsonNode?)JsonValue.Create(arg))]));
        _ = ReadCodexConfig(content);
        return content;
    }

    /// <summary>Replaces a value or inserts a dotted key into its nearest existing table.</summary>
    private static string SetLaunchValue(string text, string[] keys, JsonNode value)
    {
        var root = Parse(text);
        SyntaxNode container = root;
        var depth = 0;
        ValueSyntax? existing = null;

        // Finds the value and the deepest table that can hold a missing key.
        void Visit(SyntaxNode node, IReadOnlyList<string> prefix)
        {
            if (prefix.Count > keys.Length || !Matches(prefix))
            {
                return;
            }

            if (prefix.Count >= depth)
            {
                container = node;
                depth = prefix.Count;
            }

            foreach (var child in KeyValuesOf(node))
            {
                var resolved = new List<string>(prefix);
                resolved.AddRange(KeyParts(child.Key!));
                if (!Matches(resolved))
                {
                    continue;
                }

                if (resolved.Count == keys.Length)
                {
                    existing = child.Value;
                }
                else if (child.Value is InlineTableSyntax inline)
                {
                    Visit(inline, resolved);
                }
                else
                {
                    throw new InvalidOperationException("Codex setting " + string.Join('.', resolved) + " must be a TOML table");
                }
            }

            if (node is DocumentSyntax document)
            {
                foreach (var table in document.Tables.OfType<TableSyntax>())
                {
                    Visit(table, KeyParts(table.Name!));
                }
            }
        }

        bool Matches(IReadOnlyList<string> path) => path.Count <= keys.Length && path.Select((key, index) => key == keys[index]).All(match => match);

        Visit(root, []);
        var encoded = value.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (existing is not null)
        {
            return string.Concat(text.AsSpan(0, existing.Span.Offset), encoded, text.AsSpan(existing.Span.Offset + existing.Span.Length));
        }

        var assignment = string.Join('.', keys.Skip(depth)) + " = " + encoded;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        int offset;
        string insertion;
        if (container is InlineTableSyntax inlineTable)
        {
            offset = inlineTable.CloseBrace!.Span.Offset;
            // A table that already ends with a comma (TOML 1.1) keeps it; a second one would be invalid.
            var before = text.AsSpan(0, offset).TrimEnd();
            var trailingComma = inlineTable.Items.ChildrenCount > 0 && before.EndsWith(',');
            if (trailingComma)
            {
                offset = before.Length;
            }

            insertion = (inlineTable.Items.ChildrenCount > 0 ? trailingComma ? " " : ", " : "") + assignment;
        }
        else
        {
            // Upstream moves an offset of 0 to the end of the text, which also sends a first table at offset 0 there; only a missing newline does that here.
            if (container is TableSyntaxBase table)
            {
                var headerEnd = text.IndexOf('\n', table.Name!.Span.Offset + table.Name.Span.Length);
                offset = headerEnd < 0 ? text.Length : headerEnd + 1;
            }
            else
            {
                offset = root.Tables.FirstOrDefault()?.Span.Offset ?? text.Length;
            }

            insertion = (offset > 0 && text[offset - 1] != '\n' ? newline : "") + assignment + newline;
        }

        return string.Concat(text.AsSpan(0, offset), insertion, text.AsSpan(offset));
    }

    private static DocumentSyntax Parse(string text)
    {
        var document = SyntaxParser.Parse(text);
        if (document.HasErrors)
        {
            throw new InvalidOperationException(string.Join("; ", document.Diagnostics.Select(message => message.ToString())));
        }

        return document;
    }

    private static IEnumerable<KeyValueSyntax> KeyValuesOf(SyntaxNode node) => node switch
    {
        DocumentSyntax document => document.KeyValues,
        TableSyntaxBase table => table.Items.OfType<KeyValueSyntax>(),
        InlineTableSyntax inline => inline.Items.Select(item => item.KeyValue!),
        _ => [],
    };

    /// <summary>The static parts of a key, <c>a."b".c</c> being <c>a</c>, <c>b</c>, <c>c</c>.</summary>
    private static List<string> KeyParts(KeySyntax key)
    {
        List<string> parts = [KeyText(key.Key!)];
        parts.AddRange(key.DotKeys.Select(dotted => KeyText(dotted.Key!)));
        return parts;
    }

    private static string KeyText(SyntaxNode key) => key switch
    {
        BareKeySyntax bare => bare.Key!.Text!,
        StringValueSyntax text => text.Value!,
        _ => throw new InvalidOperationException("unsupported TOML key"),
    };

    /// <summary>.NET-only: the Tomlyn model as JSON, the shape <see cref="McpConfig"/> merges and compares (upstream reads plain objects).</summary>
    private static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        TomlTable table => new JsonObject(table.Select(pair => KeyValuePair.Create(pair.Key, ToJson(pair.Value)))),
        TomlTableArray tables => new JsonArray([.. tables.Select(ToJson)]),
        TomlArray array => new JsonArray([.. array.Select(ToJson)]),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        long number => JsonValue.Create(number),
        double number when !double.IsFinite(number) => JsonValue.Create(number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        double number => JsonValue.Create(number),
        _ => JsonValue.Create(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)),
    };
}
