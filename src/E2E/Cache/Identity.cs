// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E;

/// <summary>Upstream's <c>TraceCacheKey</c>: the exact context a trace was recorded in. Secret values never enter it.</summary>
internal sealed record TraceCacheKey(
    string SpecVersion,
    string CacheSchema,
    string Project,
    string TestId,
    string TargetId,
    string Platform,
    string EngineName,
    int EngineSpiVersion,
    string Kind,
    int CallIndex,
    string InstructionDigest,
    string ParamsDigest,
    string AppIdentity,
    string Agent,
    string AgentContextDigest,
    string PolicyVersion)
{
    /// <summary>The key as the JSON object that is hashed.</summary>
    internal JsonObject ToJson() => new()
    {
        ["specVersion"] = SpecVersion,
        ["cacheSchema"] = CacheSchema,
        ["project"] = Project,
        ["testId"] = TestId,
        ["targetId"] = TargetId,
        ["platform"] = Platform,
        ["engineName"] = EngineName,
        ["engineSpiVersion"] = EngineSpiVersion,
        ["kind"] = Kind,
        ["callIndex"] = CallIndex,
        ["instructionDigest"] = InstructionDigest,
        ["paramsDigest"] = ParamsDigest,
        ["appIdentity"] = AppIdentity,
        ["agent"] = Agent,
        ["agentContextDigest"] = AgentContextDigest,
        ["policyVersion"] = PolicyVersion,
    };
}

/// <summary>Upstream's <c>CacheAgentIdentity</c>: the agent a step runs with, as the key sees it. The context is already redacted.</summary>
internal sealed record CacheAgentIdentity(string Name, string? Context);

/// <summary>Upstream's <c>CacheTargetIdentity</c>: identity of the target, engine, and app a trace was recorded against.</summary>
internal sealed record CacheTargetIdentity(string TargetId, string Platform, string EngineName, int SpiVersion, string AppIdentity);

/// <summary>Upstream's <c>TraceCallSignature</c>: what a step asks for, independent of where it sits in the test.</summary>
internal sealed record TraceCallSignature(string Kind, string InstructionDigest, string ParamsDigest);

/// <summary>The argument object of upstream's <c>buildTraceCacheKey</c>.</summary>
internal sealed record TraceCacheKeyParts(
    string Project,
    string TestId,
    CacheTargetIdentity Target,
    TraceCallSignature Signature,
    int CallIndex,
    CacheAgentIdentity Agent,
    string PolicyVersion);

/// <summary>
/// Upstream's <c>cache/identity.ts</c>: trace cache key identity. A key names the exact context a trace was recorded in, so a
/// stale entry can only be a miss rather than a wrong answer. An entry's file name is the hash of its key.
/// </summary>
internal static class Identity
{
    /// <summary>Upstream's <c>TraceCacheKind</c>: only <c>act</c> is cached.</summary>
    public const string ActKind = "act";

    /// <summary>
    /// Normalizes one instruction before digesting it: CRLF and CR become LF, the text is NFC-normalized, and leading and trailing
    /// whitespace is trimmed. Interior whitespace is preserved.
    /// </summary>
    public static string NormalizeInstruction(string instruction)
    {
        var text = instruction.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(NormalizationForm.FormC);
        var start = 0;
        var end = text.Length;
        while (start < end && IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    /// <summary>SHA-256 of the normalized instruction.</summary>
    public static string InstructionDigest(string instruction) => Ids.Sha256Hex(NormalizeInstruction(instruction));

    /// <summary>SHA-256/JCS of the projected (secret-free) call parameters. A missing params object digests as the empty object.</summary>
    public static string ParamsDigest(JsonObject? parameters) => Ids.CanonicalDigest(parameters ?? []);

    /// <summary>SHA-256 of the resolved project identity.</summary>
    public static string ProjectIdentity(string projectId) => Ids.Sha256Hex(projectId);

    /// <summary>Builds one signature by digesting the instruction and projected parameters.</summary>
    public static TraceCallSignature TraceCallSignature(string kind, string instruction, JsonObject? parameters) =>
        new(kind, InstructionDigest(instruction), ParamsDigest(parameters));

    /// <summary>
    /// Counts occurrences of each call signature within one attempt, returning the zero-based index of each step among its own
    /// repeats. Per signature rather than per attempt on purpose: only a genuine repeat of the same request advances the number.
    /// </summary>
    public static Func<TraceCallSignature, int> CreateCallIndexer()
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return signature =>
        {
            // Both digests are hex, so a colon cannot appear inside either and the join is unambiguous.
            var mapKey = $"{signature.Kind}:{signature.InstructionDigest}:{signature.ParamsDigest}";
            seen.TryGetValue(mapKey, out var count);
            seen[mapKey] = count + 1;
            return count;
        };
    }

    /// <summary>Assembles one key.</summary>
    public static TraceCacheKey BuildTraceCacheKey(TraceCacheKeyParts parts) =>
        new(
            "0.1",
            Trace.TraceSchemaVersion,
            parts.Project,
            parts.TestId,
            parts.Target.TargetId,
            parts.Target.Platform,
            parts.Target.EngineName,
            parts.Target.SpiVersion,
            parts.Signature.Kind,
            parts.CallIndex,
            parts.Signature.InstructionDigest,
            parts.Signature.ParamsDigest,
            parts.Target.AppIdentity,
            parts.Agent.Name,
            Ids.Sha256Hex(parts.Agent.Context ?? ""),
            parts.PolicyVersion);

    /// <summary>SHA-256/JCS of one key, used as both the entry digest and its file name.</summary>
    public static string TraceCacheKeyHash(TraceCacheKey key) => Ids.CanonicalDigest(key.ToJson());

    /// <summary>
    /// The parts of a key outside the step's own identity, as an entry records them (<c>ActionTrace.keyedBy</c>). Upstream takes
    /// the key and picks <c>KEY_CONTEXT_FIELDS</c> from it.
    /// </summary>
    public static TraceKeyContext KeyContext(TraceCacheKey key) =>
        new(key.CacheSchema, key.PolicyVersion, key.Project, key.Platform, key.EngineName, key.EngineSpiVersion, key.AppIdentity, key.AgentContextDigest);

    // How each part of a key's context reads in a sentence, and whether its value means anything to a reader.
    private static readonly Dictionary<string, (string Name, bool Shown)> KeyContextWords = new(StringComparer.Ordinal)
    {
        ["cacheSchema"] = ("the cache format", true),
        ["policyVersion"] = ("the replay policy", true),
        ["project"] = ("the projectId", false),
        ["platform"] = ("the platform", true),
        ["engineName"] = ("the engine", true),
        ["engineSpiVersion"] = ("the engine contract", true),
        ["appIdentity"] = ("the app's identity (app.identity, else its URL) or environment", false),
        ["agentContextDigest"] = ("the agent's context (its context, or the test's agentContext)", false),
    };

    /// <summary>
    /// What differs between the context an entry was recorded under and this run's: <c>the engine contract (1 -> 2)</c>, <c>the app's
    /// identity ...</c>. Empty when nothing differs or the entry recorded no context.
    /// </summary>
    public static IReadOnlyList<string> KeyContextChanges(TraceKeyContext? recorded, TraceKeyContext now)
    {
        if (recorded is null)
        {
            return [];
        }

        var changes = new List<string>();
        foreach (var (field, get) in Trace.KeyContextFields)
        {
            var before = get(recorded);
            var after = get(now);
            if (before.Equals(after))
            {
                continue;
            }

            var (name, shown) = KeyContextWords[field];
            changes.Add(shown ? string.Create(CultureInfo.InvariantCulture, $"{name} ({before} -> {after})") : name);
        }

        return changes;
    }

    // JavaScript's String.prototype.trim: the WhiteSpace and LineTerminator productions, which differ from char.IsWhiteSpace.
    private static bool IsWhiteSpace(char c) =>
        c is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\x00A0' or '\x1680' or (>= '\x2000' and <= '\x200A') or '\x2028' or '\x2029' or '\x202F' or '\x205F' or '\x3000' or '\xFEFF';
}
