// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E2E.Internal;

// AI trace recording for E2E_AI_TRACE, the port's counterpart of `e2e run --ai-trace` (upstream internal/ai-trace.ts): every model
// round trip a test makes, in the AI SDK devtools database shape ({ runs[], steps[] }) that trace viewers such as unbox-ai read.
//
// Upstream's recorder is an AI SDK telemetry integration. The port has no AI SDK, so AiTraceModel feeds the same events to the recorder
// from the model seam, and the recorder keeps upstream's event methods. Attribution comes from an async-local scope that the step
// recorder enters per step, so no model call site needs to know it is traced.

/// <summary>A run record: one <c>generateText</c> call, which for <c>agent.act</c> is one whole step.</summary>
internal sealed record AiTraceRun
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("started_at")]
    public required string StartedAt { get; init; }

    [JsonPropertyName("parent_run_id")]
    public string? ParentRunId { get; init; }

    [JsonPropertyName("parent_step_id")]
    public string? ParentStepId { get; init; }

    /// <summary>Human-readable run name: test title, agent API, and step label.</summary>
    [JsonPropertyName("function_id")]
    public string? FunctionId { get; init; }

    /// <summary>e2e attribution beyond what the devtools shape carries.</summary>
    [JsonPropertyName("e2e")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiTraceScope? E2e { get; init; }
}

/// <summary>A step record: one model round trip inside a run, JSON columns as strings.</summary>
internal sealed record AiTraceStep
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("step_number")]
    public required int StepNumber { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "generate";

    [JsonPropertyName("model_id")]
    public required string ModelId { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("started_at")]
    public required string StartedAt { get; init; }

    [JsonPropertyName("duration_ms")]
    public double? DurationMs { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("usage")]
    public string? Usage { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>The on-disk document: exactly what <c>@ai-sdk/devtools</c> writes. Also what a recorder drains.</summary>
internal sealed record AiTraceDocument
{
    [JsonPropertyName("runs")]
    public IReadOnlyList<AiTraceRun> Runs { get; init; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyList<AiTraceStep> Steps { get; init; } = [];
}

/// <summary>What the run layer knows about the code that is calling the model.</summary>
internal sealed record AiTraceScope
{
    [JsonPropertyName("test")]
    public required string Test { get; init; }

    [JsonPropertyName("testId")]
    public required string TestId { get; init; }

    [JsonPropertyName("target")]
    public required string Target { get; init; }

    /// <summary>The configured agent the attempt runs as.</summary>
    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    /// <summary>Zero-based attempt index.</summary>
    [JsonPropertyName("attempt")]
    public required int Attempt { get; init; }

    [JsonPropertyName("api")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Api { get; init; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}

internal sealed record AiTraceStartEvent(string CallId, string OperationId, string? FunctionId = null);

internal sealed record AiTraceStepStartEvent(string CallId, int StepNumber, string? Provider, string ModelId, object? Instructions, IReadOnlyList<object?> Messages, object? ToolChoice = null);

internal sealed record AiTraceModelCallStartEvent(
    string CallId,
    object? Instructions = null,
    IReadOnlyList<object?>? Messages = null,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Tools = null,
    object? ToolChoice = null);

internal sealed record AiTraceModelCallEndEvent(string CallId, double? ResponseTimeMs = null);

internal sealed record AiTraceResponse(string? Id = null, string? ModelId = null, object? Timestamp = null, IReadOnlyList<object?>? Messages = null);

internal sealed record AiTraceStepEndEvent(
    string CallId,
    int StepNumber,
    object? Content,
    object? FinishReason,
    object? Usage,
    AiTraceResponse Response,
    object? ProviderMetadata = null);

internal sealed record AiTraceEndEvent(string CallId);

internal sealed record AiTraceErrorEvent(string? CallId, object? Error);

/// <summary>Upstream's <c>withAiTraceScope</c> and <c>withAiTraceStep</c>: the attribution of the model calls made inside.</summary>
internal static class AiTraceScopes
{
    private static readonly AsyncLocal<AiTraceScope?> Storage = new();

    /// <summary>The scope of the calling async context, when inside an attempt.</summary>
    public static AiTraceScope? Current => Storage.Value;

    /// <summary>
    /// Runs <paramref name="work"/> with <paramref name="scope"/> as the attribution for every model call inside. The change stays with this
    /// call and what it starts, because both helpers are <c>async</c>: the caller's own context is restored when the call yields.
    /// </summary>
    public static async Task<T> WithAiTraceScope<T>(AiTraceScope scope, Func<Task<T>> work)
    {
        Storage.Value = scope;
        return await work().ConfigureAwait(false);
    }

    /// <summary>
    /// Narrows the current attempt scope to one public API step. Outside an attempt scope there is nothing to narrow, and the work
    /// runs as is.
    /// </summary>
    public static async Task<T> WithAiTraceStep<T>(string api, string label, Func<Task<T>> work)
    {
        if (Storage.Value is not { } parent)
        {
            return await work().ConfigureAwait(false);
        }

        Storage.Value = parent with { Api = api, Label = label };
        return await work().ConfigureAwait(false);
    }
}

/// <summary>
/// Collects one session's model calls. <see cref="AiTraceModel"/> feeds it; <see cref="Drain"/> hands the completed records to the
/// collector. Upstream's <c>AiTraceRecorder</c>, with the AI SDK telemetry callbacks as methods.
/// </summary>
internal sealed partial class AiTraceRecorder
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, CallState> _calls = new(StringComparer.Ordinal);

    // One run per agent step: every generation made under the same step scope (a waitFor poll, a judgment repair round) continues the
    // step's run rather than starting its own, so a viewer's run list reads as the test's step list. Keyed by scope identity, which is
    // fresh per step.
    private readonly ConditionalWeakTable<AiTraceScope, RunCounter> _stepRuns = [];
    private readonly List<AiTraceRun> _runs = [];
    private readonly List<AiTraceStep> _steps = [];

    // The tool a generation is being made from, carried by async context so tools that execute in parallel each see only their own ancestry.
    private readonly AsyncLocal<ToolAncestry?> _toolAncestry = new();
    private bool _disposed;

    /// <summary>Stops recording: a disposed recorder ignores every later event.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Returns every run created and every step closed since the previous drain. Open steps stay until they close, so a step that
    /// finishes after a unit boundary is never reported half-written; <c>all</c> at shutdown takes them as they are.
    /// </summary>
    public AiTraceDocument Drain(bool all = false)
    {
        lock (_gate)
        {
            var runs = _runs.ToList();
            var steps = _steps.ToList();
            _runs.Clear();
            _steps.Clear();
            if (all)
            {
                foreach (var state in _calls.Values)
                {
                    foreach (var open in state.OpenSteps.Values)
                    {
                        steps.Add(CloseStep(open, error: "run ended before the model answered"));
                    }
                }

                _calls.Clear();
            }

            return new AiTraceDocument { Runs = runs, Steps = steps };
        }
    }

    /// <summary>True while any step has started and not yet ended; useful in tests.</summary>
    public bool Pending
    {
        get
        {
            lock (_gate)
            {
                return _calls.Values.Any(state => state.OpenSteps.Count > 0);
            }
        }
    }

    /// <summary>Runs a tool, so a generation it makes is attributed to the step it executes under.</summary>
    public async Task ExecuteTool(string callId, Func<Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ToolAncestry? ancestry = null;
        lock (_gate)
        {
            if (_calls.TryGetValue(callId, out var state))
            {
                ancestry = new ToolAncestry(state.RunId, state.OpenSteps.Count == 0 ? null : state.OpenSteps.Values.Last().Id);
            }
        }

        if (ancestry is null)
        {
            await execute().ConfigureAwait(false);
            return;
        }

        _toolAncestry.Value = ancestry;
        await execute().ConfigureAwait(false);
    }

    public void OnStart(AiTraceStartEvent start)
    {
        if (!string.Equals(start.OperationId, "ai.generateText", StringComparison.Ordinal) && !string.Equals(start.OperationId, "ai.streamText", StringComparison.Ordinal))
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var parent = _toolAncestry.Value;
            var scope = AiTraceScopes.Current;

            // A generation made inside a tool is its own nested run, never a continuation of the step's run, so the parent link stays meaningful.
            var stepScope = scope is { Api: not null } && parent is null ? scope : null;
            if (stepScope is not null && _stepRuns.TryGetValue(stepScope, out var existing))
            {
                _calls[start.CallId] = new CallState(existing.RunId, existing);
                return;
            }

            var run = new RunCounter(Guid.NewGuid().ToString());
            if (stepScope is not null)
            {
                _stepRuns.Add(stepScope, run);
            }

            _calls[start.CallId] = new CallState(run.RunId, run);
            _runs.Add(new AiTraceRun
            {
                Id = run.RunId,
                StartedAt = Now(),
                ParentRunId = parent?.RunId,
                ParentStepId = parent?.StepId,
                FunctionId = RunName(scope, start.FunctionId),
                E2e = scope,
            });
        }
    }

    public void OnStepStart(AiTraceStepStartEvent start)
    {
        lock (_gate)
        {
            if (_disposed || !_calls.TryGetValue(start.CallId, out var state))
            {
                return;
            }

            var stepNumber = state.Run.Steps;
            state.Run.Steps++;
            state.OpenSteps[start.StepNumber] = new OpenStep
            {
                Id = Guid.NewGuid().ToString(),
                RunId = state.RunId,
                StepNumber = stepNumber,
                ModelId = start.ModelId,
                Provider = start.Provider,
                StartedAt = Now(),
                Started = DateTimeOffset.UtcNow,
                Prompt = PromptMessages(start.Instructions, start.Messages),
                ToolChoice = start.ToolChoice,
            };
        }
    }

    /// <summary>
    /// The request as actually sent: after the history is prepared, with the tool definitions carrying their JSON schemas. This
    /// supersedes the step-start view, which predates per-step message compaction and tool filtering.
    /// </summary>
    public void OnLanguageModelCallStart(AiTraceModelCallStartEvent start)
    {
        lock (_gate)
        {
            if (_disposed || NewestOpen(start.CallId) is not { } open)
            {
                return;
            }

            if (start.Messages is not null)
            {
                open.Prompt = PromptMessages(start.Instructions, start.Messages);
            }

            if (start.Tools is not null)
            {
                open.Tools = [.. start.Tools.Select(ToolDefinition)];
            }

            if (start.ToolChoice is not null)
            {
                open.ToolChoice = start.ToolChoice;
            }
        }
    }

    public void OnLanguageModelCallEnd(AiTraceModelCallEndEvent end)
    {
        lock (_gate)
        {
            if (!_disposed && NewestOpen(end.CallId) is { } open && end.ResponseTimeMs is { } responseTimeMs)
            {
                open.ResponseTimeMs = responseTimeMs;
            }
        }
    }

    public void OnStepEnd(AiTraceStepEndEvent end)
    {
        lock (_gate)
        {
            if (_disposed || !_calls.TryGetValue(end.CallId, out var state) || !state.OpenSteps.Remove(end.StepNumber, out var open))
            {
                return;
            }

            var response = new Dictionary<string, object?>(StringComparer.Ordinal);
            AddIfSet(response, "id", end.Response.Id);
            AddIfSet(response, "modelId", end.Response.ModelId);
            AddIfSet(response, "timestamp", end.Response.Timestamp is DateTimeOffset at ? Iso(at) : end.Response.Timestamp);
            AddIfSet(response, "messages", end.Response.Messages is null ? null : TraceMessages(end.Response.Messages));
            var output = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["content"] = end.Content,
                ["finishReason"] = end.FinishReason,
                ["response"] = response,
            };
            AddIfSet(output, "providerMetadata", end.ProviderMetadata);
            _steps.Add(CloseStep(open, output: output, usage: end.Usage));
        }
    }

    public void OnEnd(AiTraceEndEvent end)
    {
        lock (_gate)
        {
            if (!_calls.TryGetValue(end.CallId, out var state))
            {
                return;
            }

            // Steps that never reported an end (aborted mid-flight) close here so nothing dangles once the generation itself is over.
            foreach (var open in state.OpenSteps.Values)
            {
                _steps.Add(CloseStep(open, error: "the generation ended before this step did"));
            }

            _calls.Remove(end.CallId);
        }
    }

    public void OnError(AiTraceErrorEvent error)
    {
        if (error.CallId is not { } callId)
        {
            return;
        }

        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out var state))
            {
                return;
            }

            var message = error.Error switch
            {
                null => "unknown error",
                Exception cause => cause.Message,
                var other => Convert.ToString(other, CultureInfo.InvariantCulture) ?? "unknown error",
            };
            foreach (var open in state.OpenSteps.Values)
            {
                _steps.Add(CloseStep(open, error: message));
            }

            _calls.Remove(callId);
        }
    }

    private static string Now() => Iso(DateTimeOffset.UtcNow);

    /// <summary>The instant as JavaScript's <c>toISOString</c> writes it, which sorts as text in time order.</summary>
    internal static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static void AddIfSet(Dictionary<string, object?> target, string key, object? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }

    private static Dictionary<string, object?> ToolDefinition(IReadOnlyDictionary<string, object?> tool)
    {
        var definition = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = tool.GetValueOrDefault("name") };
        if (tool.TryGetValue("description", out var description) && description is not null)
        {
            definition["description"] = description;
        }

        if (tool.TryGetValue("inputSchema", out var schema) && schema is not null)
        {
            definition["parameters"] = schema;
        }

        return definition;
    }

    private OpenStep? NewestOpen(string callId) =>
        _calls.TryGetValue(callId, out var state) && state.OpenSteps.Count > 0 ? state.OpenSteps.Values.Last() : null;

    private AiTraceStep CloseStep(OpenStep open, object? output = null, object? usage = null, string? error = null)
    {
        var input = new Dictionary<string, object?>(StringComparer.Ordinal) { ["prompt"] = TraceMessages(open.Prompt) };
        AddIfSet(input, "tools", open.Tools);
        AddIfSet(input, "toolChoice", open.ToolChoice);
        return new AiTraceStep
        {
            Id = open.Id,
            RunId = open.RunId,
            StepNumber = open.StepNumber + 1,
            ModelId = open.ModelId,
            Provider = open.Provider,
            StartedAt = open.StartedAt,
            DurationMs = open.ResponseTimeMs ?? (DateTimeOffset.UtcNow - open.Started).TotalMilliseconds,
            Input = AiTrace.Stringify(input),
            Output = output is null ? null : AiTrace.Stringify(output),
            Usage = usage is null ? null : AiTrace.Stringify(usage),
            Error = error,
        };
    }

    // The prompt as a message list. The system prompt is kept apart from the conversation (instructions); viewers attribute tokens per
    // message role, so it is put back in front as system messages.
    private static List<object?> PromptMessages(object? instructions, IReadOnlyList<object?> messages)
    {
        var prompt = new List<object?>();
        switch (instructions)
        {
            case string text:
                prompt.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["role"] = "system", ["content"] = text });
                break;
            case IReadOnlyList<object?> parts:
                prompt.AddRange(parts);
                break;
            case not null:
                prompt.Add(instructions);
                break;
        }

        prompt.AddRange(messages);
        return prompt;
    }

    private static string? RunName(AiTraceScope? scope, string? functionId)
    {
        if (scope is null)
        {
            return functionId;
        }

        var parts = new List<string> { scope.Test };

        // A test run as several agents is several runs; the name tells them apart.
        if (!string.Equals(scope.Agent, "default", StringComparison.Ordinal))
        {
            parts.Add("as " + scope.Agent);
        }

        if (scope.Attempt > 0)
        {
            parts.Add("attempt " + (scope.Attempt + 1).ToString(CultureInfo.InvariantCulture));
        }

        if (scope.Api is not null)
        {
            var label = string.IsNullOrEmpty(scope.Label) ? "" : " " + JsonSerializer.Serialize(Truncate(scope.Label), AiTrace.Options);
            parts.Add(scope.Api + label);
        }

        return string.Join(" · ", parts);
    }

    private static string Truncate(string text) => text.Length <= AiTrace.MaxLabelChars ? text : text[..AiTrace.MaxLabelChars] + "…";

    /// <summary>Copies messages with inline media omitted, without inspecting user JSON.</summary>
    internal static List<object?>? TraceMessages(IReadOnlyList<object?>? messages)
    {
        return messages?.Select(message =>
        {
            if (AsObject(message) is not { } record || !record.TryGetValue("role", out var role) || role is not ("user" or "assistant" or "tool"))
            {
                return message;
            }

            return With(record, "content", TraceContent(record.GetValueOrDefault("content")));
        }).ToList();
    }

    // Omits encoded media only in content parts and structured tool outputs.
    private static object? TraceContent(object? content)
    {
        if (content is not IReadOnlyList<object?> parts)
        {
            return content;
        }

        return parts.Select(part =>
        {
            if (AsObject(part) is not { } record)
            {
                return part;
            }

            switch (record.GetValueOrDefault("type") as string)
            {
                case "image":
                    return With(record, "image", OmitInlineData(record.GetValueOrDefault("image")));
                case "file" or "reasoning-file":
                    var data = AsObject(record.GetValueOrDefault("data"));
                    return (data?.GetValueOrDefault("type") as string) switch
                    {
                        "data" => With(record, "data", With(data!, "data", OmitInlineData(data!.GetValueOrDefault("data")))),
                        "url" => With(record, "data", With(data!, "url", OmitDataUrl(data!.GetValueOrDefault("url")))),
                        _ => With(record, "data", OmitInlineData(record.GetValueOrDefault("data"))),
                    };
                case "image-data" or "file-data":
                    return With(record, "data", OmitInlineData(record.GetValueOrDefault("data")));
                case "image-url" or "file-url":
                    return With(record, "url", OmitDataUrl(record.GetValueOrDefault("url")));
                case "tool-result":
                    var output = AsObject(record.GetValueOrDefault("output"));
                    return string.Equals(output?.GetValueOrDefault("type") as string, "content", StringComparison.Ordinal)
                        ? With(record, "output", With(output!, "value", TraceContent(output!.GetValueOrDefault("value"))))
                        : part;
                default:
                    return part;
            }
        }).ToList();
    }

    // The object fields needed to read a content part.
    private static IReadOnlyDictionary<string, object?>? AsObject(object? value) => value as IReadOnlyDictionary<string, object?>;

    private static Dictionary<string, object?> With(IReadOnlyDictionary<string, object?> record, string key, object? value)
    {
        var copy = new Dictionary<string, object?>(record, StringComparer.Ordinal) { [key] = value };
        return copy;
    }

    // Keeps remote URLs and references while omitting inline binary data without decoding it.
    private static object? OmitInlineData(object? value)
    {
        switch (value)
        {
            case byte[] bytes:
                return BinaryNote(bytes.Length);
            case ReadOnlyMemory<byte> memory:
                return BinaryNote(memory.Length);
            case Uri uri:
                return OmitDataUrl(uri);
            case not string:
                return value;
        }

        var text = (string)value;
        var prefix = Base64DataPrefix().Match(text);
        if (!prefix.Success && UrlScheme().IsMatch(text))
        {
            return value;
        }

        var encoded = WhiteSpace().Replace(prefix.Success ? text[prefix.Length..] : text, "");
        return BinaryNote(DecodedLength(encoded));
    }

    // A base64 data URL contains inline bytes; other URL forms remain useful diagnostics.
    private static object? OmitDataUrl(object? value)
    {
        var url = value is Uri uri ? uri.OriginalString : value;
        return url is string text && Base64DataPrefix().IsMatch(text) ? OmitInlineData(text) : value;
    }

    // What the base64 text decodes to, as Node's Buffer.byteLength counts it: unpadded text counts too.
    private static int DecodedLength(string encoded)
    {
        var length = encoded.Length;
        if (length > 0 && encoded[^1] == '=')
        {
            length--;
        }

        if (encoded.Length > 1 && encoded[^2] == '=')
        {
            length--;
        }

        return (int)((long)length * 3 >> 2);
    }

    // The existing devtools-compatible marker for omitted media.
    private static string BinaryNote(int bytes) => "[binary " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes omitted]";

    [GeneratedRegex(@"^data:[^,]*;base64,", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Base64DataPrefix();

    [GeneratedRegex(@"^[a-z][a-z\d+.-]*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlScheme();

    [GeneratedRegex(@"\s")]
    private static partial Regex WhiteSpace();

    /// <summary>One open step, kept as objects until it closes and gets serialized.</summary>
    private sealed class OpenStep
    {
        public required string Id { get; init; }

        public required string RunId { get; init; }

        /// <summary>Zero-based position within the run, across every generation in it.</summary>
        public required int StepNumber { get; init; }

        public required string ModelId { get; init; }

        public string? Provider { get; init; }

        public required string StartedAt { get; init; }

        public required DateTimeOffset Started { get; init; }

        public required List<object?> Prompt { get; set; }

        public List<Dictionary<string, object?>>? Tools { get; set; }

        public object? ToolChoice { get; set; }

        /// <summary>Model-side latency, when the model reported one; wall time otherwise.</summary>
        public double? ResponseTimeMs { get; set; }
    }

    private sealed class CallState(string runId, RunCounter run)
    {
        public string RunId { get; } = runId;

        /// <summary>Open steps by the per-generation step index, oldest first.</summary>
        public OrderedDictionary<int, OpenStep> OpenSteps { get; } = [];

        /// <summary>The run's step allocator, shared by every generation in the run.</summary>
        public RunCounter Run { get; } = run;
    }

    // Per-run step allocation. Numbers are taken when a step starts, not reserved when a generation starts, so generations a custom
    // executor overlaps inside one step interleave without ever sharing a number.
    private sealed class RunCounter(string runId)
    {
        public string RunId { get; } = runId;

        public int Steps { get; set; }
    }

    // The generation and step a tool is executing under, for nested runs.
    private sealed record ToolAncestry(string RunId, string? StepId);
}

/// <summary>Merges drained snapshots into one document; call order is arrival order. Upstream's <c>AiTraceCollector</c>.</summary>
internal sealed class AiTraceCollector
{
    private readonly Lock _gate = new();
    private readonly List<AiTraceRun> _runs = [];
    private readonly List<AiTraceStep> _steps = [];

    public void Merge(AiTraceDocument snapshot)
    {
        lock (_gate)
        {
            _runs.AddRange(snapshot.Runs);
            _steps.AddRange(snapshot.Steps);
        }
    }

    /// <summary>The document to write. Sessions drain in batches, so records are put back in start order: viewers read file order as time order.</summary>
    public AiTraceDocument Document()
    {
        lock (_gate)
        {
            return new AiTraceDocument
            {
                Runs = [.. _runs.OrderBy(run => run.StartedAt, StringComparer.Ordinal)],
                Steps = [.. _steps.OrderBy(step => step.StartedAt, StringComparer.Ordinal)],
            };
        }
    }

    public int Size
    {
        get
        {
            lock (_gate)
            {
                return _steps.Count;
            }
        }
    }
}

internal static class AiTrace
{
    /// <summary>Longest step label quoted in a run name before truncation.</summary>
    internal const int MaxLabelChars = 80;

    internal static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new BinaryNoteConverter(), new MemoryNoteConverter(), new BigIntegerConverter() },
    };

    // The sessions of a test process write one document per file, which is rewritten whole. A rewrite costs the size of the whole
    // document, so a busy process rewrites it at most every FlushInterval and once more when it exits.
    private static readonly ConcurrentDictionary<string, TraceFile> Files = new(StringComparer.Ordinal);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);
    private static int _exitHooked;

    /// <summary>JSON with binary views omitted; arbitrary strings and raw tool JSON are preserved. Upstream's <c>stringify</c>.</summary>
    public static string Stringify(object? value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// The trace file of this process under <paramref name="outputDirectory"/>. A test run may use several processes (one per test
    /// project), so each writes its own file instead of racing for <c>ai-trace.json</c>.
    /// </summary>
    public static string PathFor(string outputDirectory) =>
        Path.Combine(outputDirectory, "ai-trace-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".json");

    /// <summary>Takes everything the recorder holds and adds it to the process's document for <paramref name="path"/>, which is written soon after.</summary>
    public static void Collect(AiTraceRecorder recorder, string path)
    {
        var snapshot = recorder.Drain(all: true);
        recorder.Dispose();
        if (snapshot.Steps.Count == 0 && snapshot.Runs.Count == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _exitHooked, 1) == 0)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushAll();
        }

        var file = Files.GetOrAdd(path, static key => new TraceFile(key));
        file.Collector.Merge(snapshot);
        file.Flush(force: false);
    }

    // A process that exits cleanly writes what the interval held back. A file that cannot be written is the trace's loss, not the run's.
    private static void FlushAll()
    {
        foreach (var file in Files.Values)
        {
            try
            {
                file.Flush(force: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("[e2e] could not write the AI trace " + file.Path + ": " + ex.Message);
            }
        }
    }

    private sealed class TraceFile(string path)
    {
        private readonly Lock _gate = new();
        private long _written;
        private bool _dirty;
        private Timer? _pending;

        public string Path { get; } = path;

        public void FlushQuietly()
        {
            try
            {
                Flush(force: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("[e2e] could not write the AI trace " + Path + ": " + ex.Message);
            }
        }

        public AiTraceCollector Collector { get; } = new();

        public void Flush(bool force)
        {
            lock (_gate)
            {
                if (!force)
                {
                    _dirty = true;
                }

                if (!_dirty)
                {
                    return;
                }

                if (!force && _written != 0 && Stopwatch.GetElapsedTime(_written) is var since && since < FlushInterval)
                {
                    // One pending write at a time: a process that is killed before it exits still leaves the last sessions' records.
                    _pending ??= new Timer(_ => FlushQuietly(), null, FlushInterval - since, Timeout.InfiniteTimeSpan);
                    return;
                }

                _pending?.Dispose();
                _pending = null;

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                try
                {
                    AtomicWrite.WriteFileAtomic(Path, JsonSerializer.Serialize(Collector.Document(), Options));
                    _dirty = false;
                }
                finally
                {
                    _written = Stopwatch.GetTimestamp();
                }
            }
        }
    }

    private sealed class BinaryNoteConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
            writer.WriteStringValue("[binary " + value.Length.ToString(CultureInfo.InvariantCulture) + " bytes omitted]");
    }

    private sealed class MemoryNoteConverter : JsonConverter<ReadOnlyMemory<byte>>
    {
        public override ReadOnlyMemory<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializerOptions options) =>
            writer.WriteStringValue("[binary " + value.Length.ToString(CultureInfo.InvariantCulture) + " bytes omitted]");
    }

    private sealed class BigIntegerConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
