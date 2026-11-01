// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// A recording of one verified <c>act</c>. Its file name is the hash of a trace cache key (<c>TraceCacheKeyHash</c>):
/// the cache schema, the replay policy version, the project, the test, the target, the engine's name and contract version, the app's
/// identity, the instruction's and params' digests, the agent's name, a digest of the agent's redacted context, and which repeat of
/// that same call in the attempt it is. The model id and the engine's package version are not part of the key.
/// </summary>
public sealed class CacheEntry
{
    public int Schema { get; set; } = 1;

    /// <summary>The test title. A secret value in it is masked.</summary>
    public string? Test { get; set; }

    public string? Instruction { get; set; }

    /// <summary>The engine platform the step ran on. Null on an entry recorded before it was stored.</summary>
    public string? Engine { get; set; }

    /// <summary>SHA-256 of the params as the key holds them, a placeholder for each unique value.</summary>
    public string? ParamsDigest { get; set; }

    /// <summary>Zero-based repeat of the step among identical acts in the attempt.</summary>
    public int? CallIndex { get; set; }

    /// <summary>Name of the configured agent the step ran with. A secret value in it is masked.</summary>
    public string? Agent { get; set; }

    /// <summary>
    /// The configured model that recorded the step, as <see cref="IAgentModel.Name"/> spells it (provider and model id together).
    /// Null on an entry recorded before it was stored, or by an agent with no configured model. It never keys the entry.
    /// </summary>
    [JsonConverter(typeof(OptionalTextConverter))]
    public string? RecordedBy { get; set; }

    /// <summary>
    /// Upstream's <c>keyedBy</c>: the parts of the key outside the step itself, as they were when it was recorded. Provenance, never read
    /// by a replay: when a step's key no longer finds this entry, comparing it with the run's says what changed. Null on an older entry.
    /// </summary>
    [JsonConverter(typeof(KeyContextConverter))]
    public TraceKeyContext? KeyedBy { get; set; }

    public string? Route { get; set; }

    public string? EndRoute { get; set; }

    public List<RecordedAction> Actions { get; set; } = [];

    /// <summary>Nodes on screen when the step passed and not when it began (<see cref="Anchors"/>).</summary>
    public List<RecordedTarget> Appeared { get; set; } = [];

    /// <summary>Nodes on screen when the step began and gone when it passed.</summary>
    public List<RecordedTarget> Gone { get; set; } = [];

    /// <summary>
    /// Upstream's <c>endWaitMs</c>: how long the recorded run took from its last action to its passing verdict, plus a margin. A replay
    /// waits up to this long for the recorded end state to show: the recorded run waited for its effect too (a report that takes half a
    /// minute), and the click alone proves nothing. Null on an entry recorded before it was stored.
    /// </summary>
    public int? EndWaitMs { get; set; }

    /// <summary>
    /// Whether <see cref="EndWaitMs"/> is well formed: absent, or between zero and <see cref="Trace.MaxTraceEndWaitMs"/>. Upstream's
    /// <c>readTrace</c>, which makes a malformed entry a miss.
    /// </summary>
    internal bool HasValidEndWait() => EndWaitMs is null or >= 0 and <= Trace.MaxTraceEndWaitMs;

    private const int MaxRecordedByChars = 300;

    /// <summary>
    /// Upstream's <c>readRecordedModel</c>: the model attribution when it is nonblank text within its budget, else null. A malformed
    /// attribution never makes a recording unreadable.
    /// </summary>
    internal static string? ReadRecordedBy(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > MaxRecordedByChars ? null : value;

    /// <summary>Sets the fields that name the step this entry is recorded for, from <paramref name="step"/>.</summary>
    internal void SetStep(CacheEntry step)
    {
        Test = step.Test;
        Instruction = step.Instruction;
        Engine = step.Engine;
        ParamsDigest = step.ParamsDigest;
        CallIndex = step.CallIndex;
        Agent = step.Agent;
        KeyedBy = step.KeyedBy;
    }
}

/// <summary>Reads a string and takes any other JSON value for no text, so a malformed optional field never makes a recording unreadable.</summary>
internal sealed class OptionalTextConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

public sealed class RecordedAction
{
    public string Kind { get; set; } = "";

    public string? Role { get; set; }

    public string? Name { get; set; }

    public string? TestId { get; set; }

    public string? Value { get; set; }

    public string? Key { get; set; }

    public string? Url { get; set; }

    /// <summary><c>up</c>, <c>down</c>, <c>left</c>, or <c>right</c> for <c>scroll</c> and <c>scrollUntil</c>.</summary>
    public string? Direction { get; set; }

    /// <summary>Consecutive identical scrolls folded into one action. Null means one.</summary>
    public int? Times { get; set; }

    /// <summary>The text a <c>scrollUntil</c> paged toward.</summary>
    public string? Text { get; set; }

    /// <summary>
    /// Upstream's <c>spans</c>: the share of the viewport the scrolled list covered when recorded, 0 to 1. A list that filled the screen
    /// (<see cref="CacheKeys.MainListShare"/>) scrolls as the viewport does when a replay cannot re-find it; a smaller region never does.
    /// Null for a scroll with no list, and for a list on an engine that does not measure the viewport.
    /// </summary>
    public double? Spans { get; set; }

    /// <summary>Whether <see cref="Spans"/> is well formed: absent, or between zero and one on a <c>scroll</c> or <c>scrollUntil</c>. Upstream's <c>readAction</c>.</summary>
    internal bool HasValidSpans() => Spans is null || (Kind is "scroll" or "scrollUntil" && Spans is >= 0 and <= 1);

    /// <summary>
    /// Upstream's <c>paths</c> of an <c>upload</c>: the files attached, by project-relative path as the executor gave them. A replay input like a
    /// typed value, expanded with the call's params and authorized against the project root again before it reaches the engine.
    /// </summary>
    public List<string>? Paths { get; set; }

    /// <summary>
    /// Whether <see cref="Paths"/> is well formed: an <c>upload</c> holds one to <see cref="Trace.MaxTraceUploadPaths"/> non-empty paths and
    /// every other action none. Upstream's <c>readInputPaths</c>, which makes a malformed entry a miss.
    /// </summary>
    internal bool HasValidPaths() =>
        Kind == "upload"
            ? Paths is { Count: > 0 and <= Trace.MaxTraceUploadPaths } && Paths.All(path => !string.IsNullOrEmpty(path) && path.Length <= Trace.MaxTraceInputChars)
            : Paths is null;

    /// <summary>A copy of this action that names its target by <paramref name="name"/> and <paramref name="testId"/>. The role is vocabulary, left alone.</summary>
    internal RecordedAction WithDescriptor(string? name, string? testId)
    {
        var copy = (RecordedAction)MemberwiseClone();
        copy.Name = name;
        copy.TestId = testId;
        return copy;
    }

    /// <summary>Keys a <c>tap</c> or <c>doubleTap</c> held for the click, replayed with it. Null holds none.</summary>
    public List<string>? Modifiers { get; set; }

    /// <summary>
    /// Whether <see cref="Modifiers"/> is well formed: absent, or on a <c>tap</c> or <c>doubleTap</c> as a non-empty list of
    /// distinct key modifier names. Upstream's <c>readModifiers</c>, which makes a malformed entry a miss.
    /// </summary>
    internal bool HasValidModifiers() =>
        Modifiers is null
        || (Kind is "tap" or "doubleTap"
            && Modifiers.Count > 0
            && Modifiers.Distinct(StringComparer.Ordinal).Count() == Modifiers.Count
            && Modifiers.All(name => KeyModifiers.TryParse(name, out _)));
}

public sealed class RecordedTarget
{
    public string? Role { get; set; }

    public string? Name { get; set; }

    public string? TestId { get; set; }

    /// <summary>An anchor's text when it differs from its name, with secrets redacted.</summary>
    public string? Text { get; set; }

    /// <summary>An anchor's placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>An anchor's input purpose, such as <c>password</c>. Null for <c>none</c>.</summary>
    public string? InputPurpose { get; set; }

    /// <summary>An anchor's value, with secrets redacted. Null for an empty or secure field.</summary>
    public string? Value { get; set; }

    /// <summary>An anchor's <c>checked</c>, <c>expanded</c>, <c>pressed</c>, and <c>selected</c> states that are on. Null for none.</summary>
    public List<string>? States { get; set; }

    /// <summary>A node as every report line that names one reads it (upstream's <c>targetLabel</c>): its role, then its label in quotes.</summary>
    internal static string TargetLabel(RecordedTarget target)
    {
        var label = target.Name ?? target.Text ?? target.Placeholder ?? target.TestId ?? "";
        var role = target.Role ?? "node";
        return label.Length == 0 ? role : role + " " + LocatorExpect.Quote(TextRules.Bound(label, 40));
    }
}

public sealed class CacheLookup
{
    public CacheEntry? Entry { get; init; }

    /// <summary><c>no-entry</c> or <c>invalid-entry</c> when <see cref="Entry"/> is null.</summary>
    public string? Reason { get; init; }
}

public interface IStepCache
{
    CacheLookup Read(string key);

    void Write(string key, CacheEntry entry);

    /// <summary>Returns when the entry is absent; throws when deletion fails, so the best-effort caller can avoid reporting a successful eviction.</summary>
    void Delete(string key);
}

/// <summary>JSON files in a directory, one per cache key. Entries over 1 MiB are ignored.</summary>
public sealed class FileStepCache : IStepCache
{
    public const int SchemaVersion = 1;

    private readonly string _directory;

    // Key hashes by the step each entry was recorded for, read once on the first strict miss that asks.
    private Dictionary<string, List<string>>? _recordings;

    public FileStepCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>The directory the entry files are in, where a trace page names a step's entry.</summary>
    internal string CacheDirectory => _directory;

    public CacheLookup Read(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return new CacheLookup { Reason = "no-entry" };
        }

        var info = new FileInfo(path);
        if (info.Length > 1024 * 1024)
        {
            return new CacheLookup { Reason = "invalid-entry" };
        }

        try
        {
            var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonDefaults.Options);
            if (entry is null || entry.Schema != SchemaVersion)
            {
                return new CacheLookup { Reason = "invalid-entry" };
            }

            return new CacheLookup { Entry = entry };
        }
        catch (JsonException)
        {
            return new CacheLookup { Reason = "invalid-entry" };
        }
        catch (IOException)
        {
            return new CacheLookup { Reason = "invalid-entry" };
        }
    }

    public void Write(string key, CacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Directory.CreateDirectory(_directory);
        AtomicWrite.WriteFileAtomic(PathFor(key), JsonSerializer.Serialize(entry, JsonDefaults.Options));
    }

    public void Delete(string key)
    {
        try
        {
            // A missing file is no error; a missing directory is the same absence. Anything else throws, so the caller
            // does not report an eviction that did not happen.
            File.Delete(PathFor(key));
        }
        catch (DirectoryNotFoundException)
        {
            // The entry is absent.
        }
    }

    /// <summary>
    /// The key of an entry recorded for the same step as <paramref name="step"/> under a key other
    /// than <paramref name="key"/>, with the key context it recorded, or null. Only an entry that records the whole step counts: one
    /// written before the engine, params, repeat, and agent were stored could be another call of
    /// the same instruction. Strict mode uses it to tell a step whose key changed under its
    /// recording from a step that was never recorded.
    /// </summary>
    internal (string Key, TraceKeyContext? KeyedBy)? UnderAnotherKey(string key, CacheEntry step)
    {
        var stepKey = StepKey(step);
        if (stepKey is null)
        {
            return null;
        }

        try
        {
            _recordings ??= ListRecordings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var candidate in _recordings.GetValueOrDefault(stepKey) ?? [])
        {
            // Listed once: an entry evicted since is no evidence.
            if (!string.Equals(candidate, key, StringComparison.Ordinal) && TryRead(candidate) is { } recorded)
            {
                return (candidate, recorded.KeyedBy);
            }
        }

        return null;
    }

    private Dictionary<string, List<string>> ListRecordings()
    {
        var recordings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (!Directory.Exists(_directory))
        {
            return recordings;
        }

        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            if (!IsKey(key))
            {
                continue;
            }

            // An entry with no actions never replays under any key, so it is never the reason a step is stale.
            var entry = TryRead(key);
            var stepKey = entry is { Actions.Count: > 0 } ? StepKey(entry) : null;
            if (stepKey is not null)
            {
                if (!recordings.TryGetValue(stepKey, out var keys))
                {
                    recordings[stepKey] = keys = [];
                }

                keys.Add(key);
            }
        }

        return recordings;
    }

    // An entry that vanished or cannot be read since the listing is no evidence either way.
    private CacheEntry? TryRead(string key)
    {
        try
        {
            return Read(key).Entry;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? StepKey(CacheEntry step)
    {
        if (step.Test is null || step.Instruction is null || step.Engine is null || step.ParamsDigest is null || step.CallIndex is null || step.Agent is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(new object[] { step.Test, step.Engine, step.Instruction, step.ParamsDigest, step.CallIndex.Value, step.Agent });
    }

    private static bool IsKey(string key) => key.Length > 0 && key.All(char.IsAsciiLetterOrDigit);

    private string PathFor(string key)
    {
        if (!IsKey(key))
        {
            throw new TestException("INVALID_ARGUMENT", "Cache key must be ASCII letters and digits.");
        }

        return Path.Combine(_directory, key + ".json");
    }
}

/// <summary>Upstream's <c>ParamTemplate</c>: one <c>unique()</c> param, its JSON Pointer in the params and this call's value.</summary>
internal readonly record struct ParamTemplate(string Pointer, string Value);

internal static partial class CacheKeys
{
    /// <summary>
    /// Version of the rules that decide whether a recording replays. Bump it when those
    /// rules change, so old entries become misses instead of wrong replays.
    /// </summary>
    public const string ReplayPolicyVersion = "5";

    /// <summary>
    /// Upstream's <c>MAIN_LIST_SHARE</c> (<c>cache/relocate.ts</c>): the share of the viewport a scrolled node must have covered when
    /// it was addressed to scroll as the viewport does once it cannot be re-found. Scrolling the main list and scrolling the screen
    /// are the same gesture, while a smaller region that vanished is gone.
    /// </summary>
    public const double MainListShare = 0.5;

    private static readonly JsonSerializerOptions KeyJson = Json(new LeafConverter<Secret>(Canonical), new LeafConverter<UniqueValue>(Canonical));

    private static readonly JsonSerializerOptions DisplayJson = Json(new LeafConverter<Secret>(Display), new LeafConverter<UniqueValue>(Display));

    /// <summary>
    /// SHA-256/JCS of the params as the key digests them (<see cref="Identity.ParamsDigest"/>): a placeholder for each unique value and
    /// the name of each secret. An entry records it as its provenance.
    /// </summary>
    public static string ParamsDigest(IReadOnlyDictionary<string, object?>? parameters)
    {
        return Identity.ParamsDigest(Project(parameters));
    }

    /// <summary>Upstream's projected (secret-free) call parameters: the params as JSON, a unique value as its placeholder (<see cref="TemplateParams"/>) and a secret as its name.</summary>
    public static JsonObject? Project(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        try
        {
            return TemplateParams(JsonSerializer.SerializeToNode(parameters, KeyJson)!.AsObject(), TemplatesOf(parameters));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new TestException("INVALID_ARGUMENT", "Act params must serialize as JSON: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Upstream's <c>templateParams</c>: the params as the key digests them, each template's leaf replaced by its placeholder, at any depth.
    /// Two calls whose params differ only in <c>unique()</c> values share a key, and therefore a recording. Upstream walks the
    /// params; the port replaces the leaves of their serialized form, which has the same pointers.
    /// </summary>
    public static JsonObject TemplateParams(JsonObject serialized, IReadOnlyList<ParamTemplate> templates)
    {
        foreach (var template in templates)
        {
            var segments = template.Pointer[1..].Split('/').Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)).ToArray();
            JsonNode? parent = serialized;
            foreach (var segment in segments[..^1])
            {
                parent = parent switch
                {
                    JsonObject map => map[segment],
                    JsonArray list when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < list.Count => list[index],
                    _ => null,
                };
            }

            var last = segments[^1];
            if (parent is JsonObject leafMap && leafMap.ContainsKey(last))
            {
                leafMap[last] = Placeholder(template.Pointer, null);
            }
            else if (parent is JsonArray leafList && int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var at) && at < leafList.Count)
            {
                leafList[at] = Placeholder(template.Pointer, null);
            }
        }

        return serialized;
    }

    public static string Canonical(object? value)
    {
        return value switch
        {
            null => "",
            UniqueValue => "<unique>",
            Secret secret => "<secret:" + secret.Name + ">",
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => Json(value),
        };
    }

    public static string Display(object? value)
    {
        return value switch
        {
            null => "",
            Secret secret when secret.Purpose is null => secret.ToString(),
            Secret secret => secret + " (" + secret.Purpose + ")",
            UniqueValue unique => unique.Value,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => JsonSerializer.Serialize(value, DisplayJson),
        };
    }

    /// <summary>
    /// <see cref="TemplateText"/> with the <c>unique()</c> values of <paramref name="parameters"/> at any depth. The port templates a
    /// text as it records it, so this is the call the recording sites make.
    /// </summary>
    public static string Template(string value, IReadOnlyDictionary<string, object?>? parameters) => TemplateText(value, TemplatesOf(parameters));

    /// <summary>The <c>unique()</c> values of the params as templates (upstream's <c>validateParams</c> list); none for no params.</summary>
    public static List<ParamTemplate> TemplatesOf(IReadOnlyDictionary<string, object?>? parameters) => ActParams.ValidateParams(parameters, "agent.act").Templates;

    /// <summary>
    /// Upstream's <c>templateText</c>: replaces every occurrence of each template's value in <paramref name="value"/>, in any of its
    /// spellings, with the matching placeholder.
    /// </summary>
    public static string TemplateText(string value, IReadOnlyList<ParamTemplate> templates)
    {
        if (templates.Count == 0)
        {
            return value;
        }

        // Each value is looked for as typed, form-encoded as a URL spells it, and on one line as the screen shows a
        // multi-line value. The longer text goes first, so a value that contains another is replaced whole.
        var spellings = templates
            .Where(template => template.Value.Length > 0)
            .SelectMany(template =>
            {
                var text = template.Value;
                var seen = new HashSet<string>(StringComparer.Ordinal) { text };
                var pointer = template.Pointer;
                var found = new List<(string Text, string Slot)> { (text, Placeholder(pointer, null)) };
                foreach (var (name, encode) in Encodings)
                {
                    var spelled = encode(text);
                    if (!string.IsNullOrEmpty(spelled) && seen.Add(spelled))
                    {
                        found.Add((spelled, Placeholder(pointer, name)));
                    }
                }

                return found;
            })
            .OrderByDescending(spelling => spelling.Text.Length);
        // Upstream's templateText tracks literal segments: a slot written for one spelling is not literal text, so a shorter
        // spelling (a value that is a part of a parameter's name, or of "|uri") never matches inside it.
        List<(bool Literal, string Text)> segments = [(true, value)];
        foreach (var (text, slot) in spellings)
        {
            List<(bool Literal, string Text)> next = [];
            foreach (var segment in segments)
            {
                if (!segment.Literal || !segment.Text.Contains(text, StringComparison.Ordinal))
                {
                    next.Add(segment);
                    continue;
                }

                var parts = segment.Text.Split(text);
                for (var index = 0; index < parts.Length; index++)
                {
                    if (parts[index].Length > 0)
                    {
                        next.Add((true, parts[index]));
                    }

                    if (index < parts.Length - 1)
                    {
                        next.Add((false, slot));
                    }
                }
            }

            segments = next;
        }

        return string.Concat(segments.Select(segment => segment.Text));
    }

    /// <summary>
    /// Upstream's <c>paramPointer</c>: extends a JSON Pointer (RFC 6901) by one key; <c>~</c> and <c>/</c> in a key become <c>~0</c> and
    /// <c>~1</c>.
    /// </summary>
    public static string ParamPointer(string parent, string key) =>
        parent + "/" + key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>Upstream's <c>paramPointer</c> for an array index: the index follows its parent after one slash.</summary>
    public static string ParamPointer(string parent, int index) => parent + "/" + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>Upstream's <c>PLACEHOLDER_PREFIX</c>: what every placeholder opens with, so a text that already spells one is told apart.</summary>
    public const string PlaceholderPrefix = "{{param:";

    private static string Placeholder(string pointer, string? encoding) => PlaceholderPrefix + pointer + (encoding is null ? "" : "|" + encoding) + "}}";

    /// <summary>
    /// Fills a recorded text from this call's params (pointer to value), or returns <see langword="null"/> as
    /// <see cref="ExpandText"/> does: a placeholder that names a param the call did not mark makes the entry a miss.
    /// </summary>
    public static string? Detemplate(string value, IReadOnlyDictionary<string, object?>? parameters)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var template in TemplatesOf(parameters))
        {
            values[template.Pointer] = template.Value;
        }

        return ExpandText(value, values);
    }

    /// <summary>
    /// Upstream's <c>expandText</c>: fills every placeholder in one regex pass from <paramref name="values"/> (pointer to value), in the
    /// encoding the placeholder names, so an expanded value is never read again. Returns <see langword="null"/> (a miss) when one names
    /// a parameter the call did not mark or an encoding this codec does not know, or when the encoding cannot spell the value (an
    /// unpaired surrogate has no <c>uri</c> spelling).
    /// </summary>
    public static string? ExpandText(string text, IReadOnlyDictionary<string, string> values)
    {
        var missing = false;
        var expanded = PlaceholderPattern().Replace(text, match =>
        {
            // Upstream's parsePlaceholder: the encoding follows the last bar; an unknown one makes the placeholder unusable.
            var inner = match.Groups[1].Value;
            var bar = inner.LastIndexOf('|');
            var pointer = bar == -1 ? inner : inner[..bar];
            Func<string, string?>? encode = null;
            if (bar != -1)
            {
                var name = inner[(bar + 1)..];
                encode = Encodings.FirstOrDefault(encoding => string.Equals(encoding.Name, name, StringComparison.Ordinal)).Encode;
                if (encode is null)
                {
                    missing = true;
                    return "";
                }
            }

            if (!values.TryGetValue(pointer, out var value) || (encode is null ? value : encode(value)) is not { } filled)
            {
                missing = true;
                return "";
            }

            return filled;
        });
        return missing ? null : expanded;
    }

    /// <summary>
    /// Upstream's <c>expandTrace</c>, as a check: whether every templated text of the entry (the typed value, the navigated URL, the
    /// scroll text, the upload paths, the target descriptors, the anchors, the start and end routes) fills from this call's params. The entry stays as recorded, with its placeholders, because
    /// a replay that passes keeps it for the next run; the port expands each text as the replay reaches it.
    /// </summary>
    public static bool TryExpandTrace(CacheEntry entry, IReadOnlyDictionary<string, object?>? parameters)
    {
        IEnumerable<string?> texts =
        [
            entry.Route,
            entry.EndRoute,
            .. entry.Actions.SelectMany(action => new[] { action.Name, action.TestId, action.Value, action.Url, action.Text }.Concat(action.Paths ?? [])),
            .. entry.Appeared.Concat(entry.Gone).SelectMany(anchor => new[] { anchor.Name, anchor.Text, anchor.TestId, anchor.Placeholder, anchor.Value }),
        ];
        return texts.All(text => text is null || Detemplate(text, parameters) is not null);
    }

    [GeneratedRegex(@"\{\{param:([^\n\r\u2028\u2029]*?)\}\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>
    /// Upstream's <c>ENCODERS</c>: the other spellings a value takes in recorded text, in upstream's order. <c>uri</c> is
    /// <c>encodeURIComponent</c> (a space is <c>%20</c>), <c>form</c> is a URL's query, <c>slug</c> is the slug an app derives for a
    /// record's path, and <c>flat</c> is <c>normalizeText</c>, the way the screen and the end anchors show a multi-line value. An
    /// encoder returns <see langword="null"/> when the value has no such spelling.
    /// </summary>
    private static readonly (string Name, Func<string, string?> Encode)[] Encodings =
    [
        ("uri", UriEncode),
        ("form", FormEncode),
        ("slug", SlugEncode),
        ("flat", TextRules.Normalize),
    ];

    /// <summary>
    /// Upstream's <c>encodeURIComponent</c>: everything but letters, digits and <c>-_.!~*'()</c> percent-encoded. An unpaired
    /// surrogate cannot be encoded, so the value has no uri spelling.
    /// </summary>
    private static string? UriEncode(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(value[index]))
            {
                return null;
            }
        }

        var builder = new StringBuilder();
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            if (octet is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9')
                or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'!' or (byte)'~' or (byte)'*' or (byte)'\'' or (byte)'(' or (byte)')')
            {
                builder.Append((char)octet);
            }
            else
            {
                builder.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>The slug an app derives for a record's path: lowercase, letters and digits kept, every other run as one dash, no dash at either end.</summary>
    private static string SlugEncode(string value)
    {
        var builder = new StringBuilder();
        var dash = false;
        foreach (var rune in value.ToLowerInvariant().EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber)
            {
                if (dash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                dash = false;
                builder.Append(rune.ToString());
            }
            else
            {
                dash = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>The value as a form submission writes it: <c>+</c> for a space, and everything but letters, digits and <c>*-._</c> percent-encoded.</summary>
    private static string FormEncode(string value)
    {
        var builder = new StringBuilder();
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            if (octet is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9') or (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_')
            {
                builder.Append((char)octet);
            }
            else if (octet == (byte)' ')
            {
                builder.Append('+');
            }
            else
            {
                builder.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    public static bool Collides(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return false;
        }

        // Upstream's templatesCollide asks it of every spelling a recording can give a marked value, not only the raw one.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in parameters.Values)
        {
            string[] spellings = value switch
            {
                UniqueValue unique => [unique.Value, .. Encodings.Select(encoding => encoding.Encode(unique.Value) ?? "")],
                string textValue => [textValue],
                _ => [],
            };

            // One value's own spellings may repeat each other; they collide only with another param's.
            foreach (var text in spellings.Where(text => !string.IsNullOrEmpty(text)).Distinct(StringComparer.Ordinal))
            {
                if (!seen.Add(text))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Json(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, KeyJson);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new TestException("INVALID_ARGUMENT", "Act params must serialize as JSON: " + ex.Message, ex);
        }
    }

    private static JsonSerializerOptions Json(params JsonConverter[] converters)
    {
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }
}

/// <summary>Writes a nested <see cref="Secret"/> or <see cref="UniqueValue"/> param as one string.</summary>
internal sealed class LeafConverter<T>(Func<T, string> write) : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(write(value));
    }
}
