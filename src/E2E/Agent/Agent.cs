// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>One goal (<see cref="ActAsync"/>) or a check against the current screen.</summary>
public sealed partial class Agent
{
    private const string JudgeSystem =
        "You judge one statement against the current screen. Call done. " +
        "status passed when the screen shows the statement is true. " +
        "status failed with code ASSERTION_FAILED when the statement is false. " +
        "status failed with code ASSERTION_INCONCLUSIVE when the screen does not show enough to decide. " +
        "When the same value or state is shown in more than one place, such as an order total in a summary and again as the amount on a pay button, the statement holds only when every place agrees with it; one place that contradicts it makes the statement false, even if another agrees. " +
        "This applies only when the statement does not say where to look: a statement that names the place, such as \"the order summary total\", is judged on that place alone. " +
        "It is about one value shown twice, not about different items: a claim about some item, such as \"a todo is marked done\", holds when one item matches. " +
        "You do not see earlier steps. Do not call any tool except done.";

    // Upstream's EXTRACT_REQUEST, for the extract and done tools. Extract cannot share the judge's
    // text: that says to call only done, and a model that follows its system prompt then never extracts.
    private const string ExtractSystem =
        "You read the requested data from the current screen. Call extract with the data when the screen shows it. " +
        "Call done only when it does not: the data is not on screen, is still loading, or cannot be read; say what is missing in summary. " +
        "Never fill a gap with a placeholder such as an empty string or 0. " +
        "An instruction that says what to answer when the data is absent, such as \"or null when none is shown\", is answered by calling extract with that value. " +
        "Take every value from the screen as it reads; never invent data. You do not see earlier steps.";

    // assert and extract make one model call and one repair round.
    private const int JudgmentModelCalls = 2;

    // The most bytes an act instruction may hold.
    private const int MaxInstructionBytes = 8_192;

    // How often waitFor looks for a changed screen once its interval has passed.
    private static readonly TimeSpan WaitForTick = TimeSpan.FromMilliseconds(250);

    // Quotes a label in a detail as JSON.stringify does: text stays readable, not escaped to \u codes.
    private static readonly JsonSerializerOptions JsonQuote = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly JsonSerializerOptions ExtractJson = CreateExtractJson(JsonDefaults.Options.NumberHandling);

    // The schema the model sees names a number as a number. Reading stays lenient: a model may still answer "3" for 3.
    private static readonly JsonSerializerOptions ExtractSchemaJson = CreateExtractJson(JsonNumberHandling.Strict);

    private static readonly JsonSchemaExporterOptions ExtractSchemaOptions = new() { TreatNullObliviousAsNonNullable = true };

    private readonly AttemptScope _scope;

    // The looks of the act in progress, with the change wait its newest action armed. Each act starts its own.
    private ObservationFeed _feed;

    /// <summary>The looks of the newest act.</summary>
    internal ObservationFeed Feed => _feed;

    internal Agent(AttemptScope scope)
    {
        _scope = scope;
        _feed = new ObservationFeed(scope.Session, TimeProvider.System);
    }

    public Task<ActResult> ActAsync(string instruction, ActOptions? options = null, CancellationToken cancellationToken = default) =>
        _scope.Track(RunActStepAsync(instruction, options, cancellationToken));

    public Task AssertAsync(string statement, AssertOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        var agent = _scope.Select(options?.Agent);
        return _scope.Track(JudgeAsync(statement, ResolveTimeout(options?.Timeout, agent.JudgmentTimeout), agent, cancellationToken));
    }

    public Task WaitForAsync(string statement, WaitForOptions? options = null, CancellationToken cancellationToken = default) =>
        _scope.Track(WaitForCoreAsync(statement, options, cancellationToken));

    private async Task WaitForCoreAsync(string statement, WaitForOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        var agent = _scope.Select(options?.Agent);
        var interval = Interval(options?.Interval);
        var timeout = ResolveTimeout(options?.Timeout, agent.JudgmentTimeout);
        var maxCalls = Budget(options?.MaxModelCalls, agent.MaxModelCalls, "MaxModelCalls");
        using var linked = Link(cancellationToken, timeout);
        var token = linked.Token;
        var deadline = DateTime.UtcNow + timeout;
        var callsBefore = _scope.ModelCalls;
        var last = "the statement did not hold";

        // An observation can outlive the deadline that bounded it, and the engine failure it then reports describes
        // a truncated budget, not a broken app. The wait owns the timeout message, with the last judgment.
        async Task<string> ObserveAsync()
        {
            try
            {
                return SnapshotText.Render(await _scope.Session.ObserveAsync(token).ConfigureAwait(false), _scope.Redactor, _scope.AppOrigin);
            }
            catch (Exception ex) when (ex is not AgentException
                && (linked.IsCancellationRequested || DateTime.UtcNow >= deadline)
                && !cancellationToken.IsCancellationRequested
                && !_scope.Token().IsCancellationRequested)
            {
                throw new AgentException("STEP_TIMEOUT", "waitFor timed out; last judgment: " + last, ex);
            }
        }

        try
        {
            var snapshot = await ObserveAsync().ConfigureAwait(false);
            while (true)
            {
                var verdict = await JudgeOnceAsync(statement, snapshot, agent, maxCalls - (_scope.ModelCalls - callsBefore), token, cancellationToken).ConfigureAwait(false);
                if (string.Equals(verdict.Status, "passed", StringComparison.Ordinal))
                {
                    _scope.MarkVerified();
                    return;
                }

                last = string.IsNullOrWhiteSpace(verdict.Summary) ? last : verdict.Summary;

                // The next judgment waits for the interval and for a screen that changed since the last one.
                var judged = snapshot;
                var judgedAt = DateTime.UtcNow;
                while (true)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new AgentException("STEP_TIMEOUT", "waitFor timed out; last judgment: " + last);
                    }

                    if (_scope.ModelCalls - callsBefore >= maxCalls)
                    {
                        throw new AgentException("STEP_BUDGET_EXHAUSTED", "waitFor exhausted its model-call budget; last judgment: " + last);
                    }

                    var untilInterval = interval - (DateTime.UtcNow - judgedAt);
                    var wait = untilInterval > TimeSpan.Zero ? untilInterval : (interval < WaitForTick ? interval : WaitForTick);
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(wait < remaining ? wait : remaining, token).ConfigureAwait(false);
                    }

                    snapshot = await ObserveAsync().ConfigureAwait(false);
                    if (DateTime.UtcNow - judgedAt >= interval && !string.Equals(snapshot, judged, StringComparison.Ordinal))
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException ex) when (linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !_scope.Token().IsCancellationRequested)
        {
            throw new AgentException("STEP_TIMEOUT", "waitFor timed out; last judgment: " + last, ex);
        }
    }

    /// <summary>
    /// Reads data of type <typeparamref name="T"/> from the current screen. <typeparamref name="T"/> is the
    /// schema: its JSON schema is sent to the judge, and the answer must deserialize into it with required
    /// members and nullable annotations respected. An answer that does not gets one repair round, then
    /// <c>MODEL_OUTPUT_INVALID</c>.
    /// </summary>
    public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options = null, CancellationToken cancellationToken = default) =>
        _scope.Track(ExtractCoreAsync<T>(instruction, options, cancellationToken));

    private async Task<T> ExtractCoreAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        var agent = _scope.Select(options?.Agent);
        using var linked = Link(cancellationToken, ResolveTimeout(options?.Timeout, agent.JudgmentTimeout));
        var token = linked.Token;
        var observation = await _scope.Session.ObserveAsync(token).ConfigureAwait(false);
        var snapshot = SnapshotText.Render(observation, _scope.Redactor, _scope.AppOrigin);
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(ExtractSchemaJson, typeof(T), ExtractSchemaOptions);
        var tools = AgentTools.ExtractFor(schema);
        var messages = new List<ModelMessage>
        {
            new()
            {
                Role = "user",
                Content = _scope.Redactor.Redact("Instruction: " + instruction + "\n\nReturn data matching this JSON schema: " + schema.ToJsonString() + "\n\n") + snapshot,
            },
        };
        for (var call = 1; ; call++)
        {
            var response = await CallModelAsync(agent.Judge, ExtractSystemFor(agent), messages, tools, agent, token, cancellationToken).ConfigureAwait(false);
            var extract = response.ToolCalls.FirstOrDefault(item => string.Equals(item.Name, "extract", StringComparison.Ordinal));
            string issue;
            string? previous = null;
            IReadOnlyList<string> fields = [];
            if (extract is null)
            {
                var done = response.ToolCalls.FirstOrDefault(item => string.Equals(item.Name, "done", StringComparison.Ordinal));
                if (done is not null)
                {
                    throw new AgentException("ASSERTION_INCONCLUSIVE", "nothing to extract: " + (Args.String(done.Arguments, "summary") ?? "the screen does not show the requested data"));
                }

                issue = "call extract with the data, or done when the screen does not show it";
                previous = response.Content;
            }
            else if (!TryProperty(extract.Arguments, "data", out var data))
            {
                issue = "extract takes the data in a data field";
                previous = JsonSerializer.Serialize(extract.Arguments);
            }
            else
            {
                previous = JsonSerializer.Serialize(data);
                try
                {
                    var value = data.Deserialize<T>(ExtractJson);
                    if (value is not null)
                    {
                        return value;
                    }

                    issue = "the data is null";
                }
                catch (JsonException ex)
                {
                    issue = ex.Message;
                    fields = TopLevelField(ex.Path);
                }

                if (call >= JudgmentModelCalls)
                {
                    throw new AgentException("MODEL_OUTPUT_INVALID", "extracted data failed schema validation: " + issue);
                }
            }

            if (call >= JudgmentModelCalls)
            {
                throw new AgentException("MODEL_OUTPUT_INVALID", "extract did not return data: " + issue);
            }

            AddRejected(messages, response);
            messages.Add(new ModelMessage
            {
                Role = "user",
                Content = _scope.Redactor.Redact(
                    Repair(
                        issue,
                        previous,
                        fields,
                        "Correct a misread or a wrong shape. When the screen cannot satisfy the errors, call done with status failed and code ASSERTION_INCONCLUSIVE and say in summary what is missing, rather than change values.")),
            });
        }
    }

    private async Task JudgeAsync(string statement, TimeSpan timeout, ResolvedAgent agent, CancellationToken cancellationToken)
    {
        using var linked = Link(cancellationToken, timeout);
        var token = linked.Token;
        var observation = await _scope.Session.ObserveAsync(token).ConfigureAwait(false);
        var verdict = await JudgeOnceAsync(statement, SnapshotText.Render(observation, _scope.Redactor, _scope.AppOrigin), agent, JudgmentModelCalls, token, cancellationToken).ConfigureAwait(false);
        if (string.Equals(verdict.Status, "passed", StringComparison.Ordinal))
        {
            _scope.MarkVerified();
            return;
        }

        throw new AgentException(verdict.Code!, verdict.Summary ?? "The statement did not hold.");
    }

    // One judgment, with a repair round when the judge did not call done and the budget has a call left.
    private async Task<Verdict> JudgeOnceAsync(string statement, string snapshot, ResolvedAgent agent, int callsLeft, CancellationToken token, CancellationToken cancellationToken)
    {
        var messages = new List<ModelMessage>
        {
            new() { Role = "user", Content = _scope.Redactor.Redact("Statement: " + statement + "\n\n") + snapshot },
        };
        var system = JudgeSystemFor(agent);
        var response = await CallModelAsync(agent.Judge, system, messages, AgentTools.Judge, agent, token, cancellationToken).ConfigureAwait(false);
        var verdict = ReadVerdict(response);
        if (verdict is not null)
        {
            return verdict.Value;
        }

        if (callsLeft < 2)
        {
            throw new AgentException("MODEL_OUTPUT_INVALID", "The judge did not call done.");
        }

        AddRejected(messages, response);
        messages.Add(new ModelMessage { Role = "user", Content = "Call done with status passed or failed." });
        response = await CallModelAsync(agent.Judge, system, messages, AgentTools.Judge, agent, token, cancellationToken).ConfigureAwait(false);
        verdict = ReadVerdict(response);
        if (verdict is null)
        {
            throw new AgentException("MODEL_OUTPUT_INVALID", "The judge did not call done.");
        }

        return verdict.Value;
    }

    // A rejected answer stays in the transcript, with a reply to each of its tool calls.
    private static void AddRejected(List<ModelMessage> messages, ModelResponse response)
    {
        messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ToolCalls = response.ToolCalls, ReasoningItems = response.ReasoningItems });
        foreach (var call in response.ToolCalls)
        {
            messages.Add(new ModelMessage { Role = "tool", ToolCallId = call.Id, Name = call.Name, Content = "rejected" });
        }
    }

    private static string Repair(string issue, string? previous, IReadOnlyList<string> fields, string outlet)
    {
        var builder = new StringBuilder();
        builder.Append("<previous-attempt-rejected>\n");
        builder.Append("Your previous response was rejected. Do not repeat it.\n");
        if (previous is not null)
        {
            builder.Append("previous response: ").Append(previous.Length > 2000 ? previous[..2000] : previous).Append('\n');
        }

        builder.Append("validation errors: ").Append(issue).Append('\n');
        builder.Append(fields.Count == 0
            ? "Field paths in the errors describe the exact output shape required."
            : "The value must be a JSON object whose top-level fields include: " + string.Join(", ", fields) + ".");
        builder.Append('\n');
        builder.Append("Return a corrected response that satisfies every error above.\n");
        builder.Append(outlet).Append('\n');
        builder.Append("</previous-attempt-rejected>");
        return builder.ToString();
    }

    // The first member of a JSON path such as $.price or $['unit price'][0].
    private static IReadOnlyList<string> TopLevelField(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('$') || path.Length < 2)
        {
            return [];
        }

        if (path[1] == '.')
        {
            var end = path.IndexOfAny(['.', '['], 2);
            var name = end < 0 ? path[2..] : path[2..end];
            return name.Length == 0 ? [] : [name];
        }

        if (path.Length > 3 && path[1] == '[' && path[2] == '\'')
        {
            var end = path.IndexOf("']", 3, StringComparison.Ordinal);
            return end < 0 ? [] : [path[3..end]];
        }

        return [];
    }

    private static Verdict? ReadVerdict(ModelResponse response)
    {
        var done = response.ToolCalls.FirstOrDefault(call => string.Equals(call.Name, "done", StringComparison.Ordinal));
        if (done is null)
        {
            return null;
        }

        var status = Args.String(done.Arguments, "status");
        if (status is not ("passed" or "failed" or "blocked"))
        {
            return null;
        }

        // A judge answers holds, fails, or inconclusive. A judge that cannot decide is inconclusive.
        string? code = null;
        if (!string.Equals(status, "passed", StringComparison.Ordinal))
        {
            code = status == "blocked" || Args.String(done.Arguments, "code") == "ASSERTION_INCONCLUSIVE" ? "ASSERTION_INCONCLUSIVE" : "ASSERTION_FAILED";
        }

        return new Verdict(status, Args.String(done.Arguments, "summary"), code);
    }

    /// <summary>
    /// Runs one act tool for a coding agent that drives a live session over MCP: the tool as the testing agent runs
    /// it, with no model, no replay cache, and no action budget. A refusal comes back as a failed outcome.
    /// </summary>
    internal Task<ToolOutcome> RunToolAsync(string name, JsonElement arguments, CancellationToken token)
    {
        var budget = new StepAccounting(int.MaxValue, int.MaxValue);
        return _scope.Track(new ActTools(_scope, _feed).ExecuteAsync(new ModelToolCall { Id = name, Name = name, Arguments = arguments }, new ActionDispatcher(_scope, _feed, budget, null), budget, null, token));
    }

    internal static bool HasTarget(JsonElement arguments)
    {
        return Args.String(arguments, "role") is not null
            || Args.String(arguments, "name") is not null
            || Args.String(arguments, "testId") is not null
            || Args.String(arguments, "ref") is not null;
    }

    internal static bool HasTarget(RecordedAction action)
    {
        return action.Role is not null || action.Name is not null || action.TestId is not null;
    }

    internal static bool TryDirection(string? value, out ScrollDirection direction)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "up":
                direction = ScrollDirection.Up;
                return true;
            case "down":
                direction = ScrollDirection.Down;
                return true;
            case "left":
                direction = ScrollDirection.Left;
                return true;
            case "right":
                direction = ScrollDirection.Right;
                return true;
            default:
                direction = ScrollDirection.Down;
                return false;
        }
    }

    internal static ScrollDirection Direction(string? value)
    {
        TryDirection(value, out var direction);
        return direction;
    }

    internal static string DirectionName(ScrollDirection direction)
    {
        return direction switch
        {
            ScrollDirection.Up => "up",
            ScrollDirection.Left => "left",
            ScrollDirection.Right => "right",
            _ => "down",
        };
    }

    // A name or test id is matched in its redacted form, the form the model reads and the cache records.
    internal static List<SemanticNode> Find(Observation observation, string? role, string? name, string? testId, string? reference, Redactor redactor)
    {
        var matches = new List<SemanticNode>();
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            if (node.States.Hidden || node.States.AriaHidden)
            {
                continue;
            }

            if (reference is not null && role is null && name is null && testId is null)
            {
                if (string.Equals(node.Ref, reference, StringComparison.Ordinal))
                {
                    matches.Add(node);
                }

                continue;
            }

            if (role is not null && !string.Equals(node.Role, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name is not null && !TextRules.Matches(RedactLabel(node, redactor), name, exact: true))
            {
                continue;
            }

            if (testId is not null && !string.Equals(Redact(node.TestId, redactor), testId, StringComparison.Ordinal))
            {
                continue;
            }

            if (role is null && name is null && testId is null)
            {
                continue;
            }

            matches.Add(node);
        }

        return matches;
    }

    internal async Task<ModelResponse> CallModelAsync(
        IAgentModel? model,
        string system,
        List<ModelMessage> messages,
        IReadOnlyList<ModelTool> tools,
        ResolvedAgent agent,
        CancellationToken token,
        CancellationToken cancellationToken,
        bool markLatest = false)
    {
        if (model is null)
        {
            throw new AgentException("MODEL_UNAVAILABLE", "No model is configured. Agent steps need an IAgentModel, or a replay that finishes on its own.");
        }

        ModelResponse response;
        try
        {
            var request = new ModelRequest
            {
                System = _scope.Redactor.Redact(system),
                Messages = messages,
                Tools = tools,
                ProviderOptions = agent.ProviderOptions,
                MarkLatest = markLatest,
                Redactor = _scope.Redactor,
            };
            response = await model.CompleteAsync(request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_scope.Token().IsCancellationRequested)
        {
            throw new AgentException("STEP_TIMEOUT", "Agent step timed out.");
        }

        _scope.ModelCalls++;
        if (response.Usage is { } usage)
        {
            _scope.InputTokens += usage.InputTokens;
            _scope.OutputTokens += usage.OutputTokens;
        }

        return response;
    }

    internal static string? Redact(string? text, Redactor redactor)
    {
        return text is null ? null : redactor.Redact(text);
    }

    // No upstream equivalent: upstream redacts each node field once, when it builds the observation (redactNode).
    // The port keeps the raw nodes, since the engine reads a node's name, role, and test id as the page wrote them
    // (WebEngine.RequireNoNewTwinAsync), so each place that shows or records a field redacts it.
    internal static string? RedactField(string? text, int limit, Redactor redactor)
    {
        return text is null ? null : AgentObservation.RedactText(text, redactor, limit);
    }

    // The label as LabelOf reads it, redacted for the limit of the field it came from.
    internal static string? RedactLabel(SemanticNode node, Redactor redactor)
    {
        return node.Name is not null ? RedactField(node.Name, ObservationLimits.Name, redactor) : RedactField(LabelOf(node), ObservationLimits.Text, redactor);
    }

    // The name the snapshot shows: the accessible name, else the text of a node that has none.
    // The model names a target by it, and the cache records it.
    internal static string? LabelOf(SemanticNode node)
    {
        return node.Name ?? (string.IsNullOrEmpty(node.Text) ? null : node.Text);
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    // A judge never sees the act system text, so nothing in it can talk a judge into a verdict.
    private static string JudgeSystemFor(ResolvedAgent agent)
    {
        return string.IsNullOrWhiteSpace(agent.Context)
            ? JudgeSystem
            : JudgeSystem + "\n\n<project-context>\n" + agent.Context + "\n</project-context>";
    }

    private static string ExtractSystemFor(ResolvedAgent agent)
    {
        return string.IsNullOrWhiteSpace(agent.Context)
            ? ExtractSystem
            : ExtractSystem + "\n\n<project-context>\n" + agent.Context + "\n</project-context>";
    }

    private static void CheckInstruction(string instruction)
    {
        var bytes = Encoding.UTF8.GetByteCount(instruction.Normalize(NormalizationForm.FormC));
        if (bytes > MaxInstructionBytes)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "agent.act instruction is " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes; the maximum is " + MaxInstructionBytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static TimeSpan ResolveTimeout(TimeSpan? requested, TimeSpan fallback)
    {
        if (requested is null)
        {
            return fallback;
        }

        if (requested <= TimeSpan.Zero)
        {
            throw new TestException("INVALID_ARGUMENT", "timeout must be a positive duration");
        }

        return requested.Value;
    }

    private static TimeSpan Interval(TimeSpan? requested)
    {
        var value = requested ?? E2EDefaults.WaitForInterval;
        if (value < TimeSpan.FromMilliseconds(100) || value > TimeSpan.FromMilliseconds(60_000))
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                "interval must be from 100 through 60000 milliseconds, got " + value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
        }

        return value;
    }

    private static JsonSerializerOptions CreateExtractJson(JsonNumberHandling numberHandling)
    {
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            NumberHandling = numberHandling,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>A per-call budget can only lower the configured limit, never raise it.</summary>
    private static int Budget(int? requested, int limit, string label)
    {
        if (requested is null)
        {
            return limit;
        }

        if (requested <= 0)
        {
            throw new TestException("INVALID_ARGUMENT", label + " must be a positive integer.");
        }

        if (requested > limit)
        {
            throw new TestException(
                "INVALID_ARGUMENT",
                label + " " + requested.Value.ToString(CultureInfo.InvariantCulture) + " exceeds the configured limit " + limit.ToString(CultureInfo.InvariantCulture) + ".");
        }

        return requested.Value;
    }

    private CancellationTokenSource Link(CancellationToken cancellationToken, TimeSpan timeout)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _scope.Token());
        linked.CancelAfter(timeout);
        return linked;
    }
}

/// <summary>Values passed into one <see cref="Agent.ActAsync"/> goal. Put fresh data in <see cref="Values.Unique"/>.</summary>
public sealed class ActOptions
{
    public IReadOnlyDictionary<string, object?>? Params { get; init; }

    public TimeSpan? Timeout { get; init; }

    /// <summary>Action budget. Defaults to the configured <c>MaxSteps</c> (25) and can only lower it.</summary>
    public int? MaxSteps { get; init; }

    /// <summary>Model-call budget. Defaults to the configured <c>MaxModelCalls</c> and can only lower it.</summary>
    public int? MaxModelCalls { get; init; }

    /// <summary>The named agent that runs the step. Defaults to <c>default</c>.</summary>
    public string? Agent { get; init; }
}

/// <summary><c>agent.assert</c>: one judgment and one repair round, within <see cref="Timeout"/>.</summary>
public sealed class AssertOptions
{
    /// <summary>Defaults to the agent's <c>JudgmentTimeout</c> (30 s).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The named agent whose judge decides. Defaults to <c>default</c>.</summary>
    public string? Agent { get; init; }
}

/// <summary><c>agent.waitFor</c>: a judgment at most once per <see cref="Interval"/>, and only on a changed screen, until <see cref="Timeout"/>.</summary>
public sealed class WaitForOptions
{
    /// <summary>Defaults to the agent's <c>JudgmentTimeout</c> (30 s).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Least time between two judgments, 100 ms through 60 s. Defaults to 3 s.</summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>Model-call budget. Defaults to the configured <c>MaxModelCalls</c> and can only lower it.</summary>
    public int? MaxModelCalls { get; init; }

    /// <summary>The named agent whose judge decides. Defaults to <c>default</c>.</summary>
    public string? Agent { get; init; }
}

/// <summary>
/// <c>agent.extract</c>: one extraction and one repair round. The type argument of
/// <see cref="Agent.ExtractAsync{T}"/> is the schema.
/// </summary>
public sealed class ExtractOptions
{
    /// <summary>Defaults to the agent's <c>JudgmentTimeout</c> (30 s).</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The named agent whose judge reads the screen. Defaults to <c>default</c>.</summary>
    public string? Agent { get; init; }
}

public sealed class ActResult
{
    public required string Summary { get; init; }

    /// <summary>
    /// Null when caching is off. <c>self-finalized</c> finished with no model call,
    /// <c>agent-concluded</c> replayed then handed off, <c>missed</c> ran live from the start.
    /// </summary>
    public CacheInfo? Cache { get; init; }

    /// <summary>Model calls the step spent. 0 when a replay finished it.</summary>
    public int ModelCalls { get; init; }

    /// <summary>Actions the step performed, replayed or live, counting ones that failed.</summary>
    public int Actions { get; init; }
}

public sealed class CacheInfo
{
    public required string Mode { get; init; }

    public string? Reason { get; init; }

    /// <summary>The recorded actions the replay ran. 0 for a miss.</summary>
    public int ReplayedActions { get; init; }

    /// <summary>The actions the recording holds. 0 when there was no recording to read.</summary>
    public int TotalActions { get; init; }

    /// <summary>Where the replay stopped and why, after a hand-off. After an <c>end-mismatch</c> it names the recorded nodes that did not show, the ones that did not go away, and any alert the recording never saw, or says the outcome already showed before the replay acted. Null when the replay gave no detail.</summary>
    public string? Detail { get; init; }

    /// <summary><c>param-collision</c> when the step passed but its recording was not written, because a <c>unique()</c> value is spelled by another param.</summary>
    public string? NotRecorded { get; init; }
}

internal sealed class AttemptScope
{
    private readonly Dictionary<string, int> _callIndexes = new(StringComparer.Ordinal);
    private readonly List<Secret> _secrets = [];
    private readonly List<(string Name, string Value)> _registered = [];
    private Built? _redactor;

    /// <summary>
    /// An agent call in this attempt hit a model outage or a stale strict replay. The cache keeps its
    /// entries even when the host reports the failure without the original exception.
    /// </summary>
    public bool KeepCache { get; private set; }

    /// <summary>
    /// A failure on the app landed in this attempt, such as a failed assertion. A model outage in the same attempt no
    /// longer spares the entries nothing confirmed.
    /// </summary>
    public bool ImplicatesUnconfirmed { get; private set; }

    public void Implicate() => ImplicatesUnconfirmed = true;

    public static bool KeepsCache(Exception? error) => error is AgentException { Code: "MODEL_UNAVAILABLE" or "MODEL_PROVIDER_FAILED" or "REPLAY_STALE" };

    public async Task Track(Task call)
    {
        try
        {
            await call.ConfigureAwait(false);
        }
        catch (AgentException ex) when (KeepsCache(ex))
        {
            KeepCache = true;
            throw;
        }
        catch (AgentException)
        {
            Implicate();
            throw;
        }
        catch (EngineException ex) when (AppNotOpen.Is(ex))
        {
            throw AppNotOpen.ForAgent(ex);
        }
    }

    public async Task<T> Track<T>(Task<T> call)
    {
        try
        {
            return await call.ConfigureAwait(false);
        }
        catch (AgentException ex) when (KeepsCache(ex))
        {
            KeepCache = true;
            throw;
        }
        catch (AgentException)
        {
            Implicate();
            throw;
        }
        catch (EngineException ex) when (AppNotOpen.Is(ex))
        {
            throw AppNotOpen.ForAgent(ex);
        }
    }

    public required IEngineSession Session { get; init; }

    /// <summary>How far a secret reached the attempt. A secret fill, by the agent or by a locator, raises it.</summary>
    public Exposure Exposure { get; init; } = new();

    /// <summary>The base URL a navigate action resolves against, under the same URL rule as <c>app.open</c>.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>The origin of <see cref="BaseUrl"/>, which a link target on it shows without.</summary>
    public string? AppOrigin => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url) ? url.GetLeftPart(UriPartial.Authority) : null;

    /// <summary>The agents by name. <c>default</c> is always present.</summary>
    public required IReadOnlyDictionary<string, ResolvedAgent> Agents { get; init; }

    public required IStepCache? Cache { get; init; }

    public required bool CacheEnabled { get; init; }

    /// <summary>False in read-only mode: the attempt replays but does not write or delete recordings.</summary>
    public bool CacheWrite { get; init; }

    public bool CacheStrict { get; init; }

    /// <summary>Whether this attempt may replay: a first attempt, or any attempt under strict mode.</summary>
    public bool ReplayEligible { get; init; }

    public TimeSpan CleanupTimeout { get; init; } = E2EDefaults.CleanupTimeout;

    public required string TestTitle { get; init; }

    public required string EnginePlatform { get; init; }

    /// <summary>What the engine declared. Scroll and back tools are offered only when it can honor them.</summary>
    public EngineCapabilities EngineCapabilities { get; init; }

    public int Attempt { get; init; } = 1;

    public TimeSpan ActionTimeout { get; init; } = E2EDefaults.ActionTimeout;

    public TimeSpan ReplayTimeout { get; init; } = E2EDefaults.ReplayTimeout;

    public TimeSpan StepTimeout { get; init; } = E2EDefaults.StepTimeout;

    public Func<CancellationToken> Token { get; init; } = static () => CancellationToken.None;

    /// <summary>True once the test has failed. Verification stops there, so a later teardown check proves nothing.</summary>
    public Func<bool> TestFailed { get; init; } = static () => false;

    public IReadOnlyList<Secret> Secrets => _secrets;

    /// <summary>
    /// The redactor for <see cref="Secrets"/> and the values <see cref="Register"/> took, rebuilt only when
    /// <see cref="Remember"/> or <see cref="Register"/> has added one since the last build: building one costs time linear in
    /// the values' length, so a long value must not pay it on every snapshot.
    /// </summary>
    public Redactor Redactor
    {
        get
        {
            var built = _redactor;
            var count = _secrets.Count + _registered.Count;
            if (built is not null && built.Count == count)
            {
                return built.Redactor;
            }

            built = new Built(Redactor.For(_secrets.Select(secret => (secret.Name, secret.Value)).Concat(_registered).ToArray()), count);
            _redactor = built;
            return built.Redactor;
        }
    }

    public List<string> Completed { get; } = [];

    public List<PendingAct> Acts { get; } = [];

    public int ModelCalls { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int Replayed { get; set; }

    public int HandedOff { get; set; }

    public int Missed { get; set; }

    /// <summary>
    /// Claims one act's cache key. Each claim advances the zero-based repeat of its signature in the
    /// attempt. Only an identical act counts, with the same agent and context, so an optional step or
    /// another agent's call does not renumber the acts after it. <paramref name="callIndex"/> is that repeat.
    /// </summary>
    public string ClaimKey(string instruction, IReadOnlyDictionary<string, object?>? parameters, ResolvedAgent agent, out int callIndex)
    {
        // The key reads the agent context as the model does, with the secrets known so far redacted.
        var context = agent.Context is null ? null : Redactor.Redact(agent.Context);
        var signature = CacheKeys.Create(EnginePlatform, TestTitle, instruction, parameters, agent.Name, context);
        var index = _callIndexes.GetValueOrDefault(signature);
        _callIndexes[signature] = index + 1;
        callIndex = index;
        return CacheKeys.ForCall(signature, index);
    }

    /// <summary>The agent a call names, or <c>default</c>. An unknown name is <c>INVALID_ARGUMENT</c>.</summary>
    public ResolvedAgent Select(string? name)
    {
        if (name is not null && name.Trim().Length == 0)
        {
            throw new TestException("INVALID_ARGUMENT", "agent must be the name of a configured agent");
        }

        name ??= "default";
        if (Agents.TryGetValue(name, out var agent))
        {
            return agent;
        }

        throw new TestException("INVALID_ARGUMENT", "unknown agent \"" + name + "\"; configured: " + string.Join(", ", Agents.Keys));
    }

    public void Remember(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var value in parameters.Values)
        {
            if (value is Secret secret && !Secrets.Any(item => string.Equals(item.Name, secret.Name, StringComparison.Ordinal) && string.Equals(item.Value, secret.Value, StringComparison.Ordinal)))
            {
                _secrets.Add(secret);
            }
        }
    }

    /// <summary>
    /// Registers one value under a name for redaction alone, as the redaction ledger does: it is not a secret
    /// the agent may fill, and it does not withhold pixels. An earlier value for the name keeps redacting.
    /// </summary>
    public void Register(string name, string value)
    {
        if (!_registered.Contains((name, value)))
        {
            _registered.Add((name, value));
        }
    }

    public void MarkVerified()
    {
        if (TestFailed())
        {
            return;
        }

        foreach (var act in Acts)
        {
            if (act.Completed)
            {
                act.Verified = true;
            }
        }
    }

    /// <summary>A redactor and how many secrets it was built from. One object, so a reader never sees one without the other.</summary>
    private sealed record Built(Redactor Redactor, int Count);
}

internal sealed class PendingAct
{
    public required string Key { get; init; }

    /// <summary>The step this act is, as an entry records it. Null only in tests that build an act by hand.</summary>
    public CacheEntry? Step { get; init; }

    public bool Completed { get; set; }

    public bool Verified { get; set; }

    public bool ParamCollision { get; set; }

    /// <summary>A recording was read and replay ran at least up to its first action.</summary>
    public bool ConsumedReplay { get; set; }

    /// <summary>Replay finished the act with no model call, so the stored entry is already this flow.</summary>
    public bool ReplayedWhole { get; set; }

    /// <summary>The store returned an entry for this act, whether or not it replayed.</summary>
    public bool ReadEntry { get; set; }

    /// <summary>The store's read threw, so nothing is known about a recording.</summary>
    public bool ReadFailed { get; set; }

    /// <summary>The actions of the entry the store returned. 0 when it returned none.</summary>
    public int RecordedActions { get; set; }

    public CacheEntry? Entry { get; set; }
}

internal readonly record struct Verdict(string Status, string? Summary, string? Code);

internal readonly record struct ReplayAttempt(bool Completed, bool Handoff, CacheInfo? Info, CacheEntry? Entry = null)
{
    public static ReplayAttempt Done(CacheEntry entry) => new(true, false, new CacheInfo { Mode = "self-finalized" }, entry);

    public static ReplayAttempt Miss(string reason, string? detail = null) => new(false, false, new CacheInfo { Mode = "missed", Reason = reason, Detail = detail });

    public static ReplayAttempt Hand(string reason, string? detail = null) => new(false, true, new CacheInfo { Mode = "agent-concluded", Reason = reason, Detail = detail });
}

/// <summary>
/// The end check's verdict and, when it is known, why it failed: the recorded outcome already showed before the
/// replay acted, or the anchors that did not hold.
/// </summary>
internal readonly record struct EndStateCheck(bool Matches, bool AlreadyShowing = false, DeltaMismatch? Failure = null);

internal sealed class ToolOutcome
{
    public required string Content { get; init; }

    public bool Succeeded { get; init; }

    public bool Done { get; init; }

    public string? Status { get; init; }

    public string? Summary { get; init; }

    public string? Code { get; init; }

    public static ToolOutcome Ok(string content) => new() { Content = content, Succeeded = true };

    public static ToolOutcome Fail(string content) => new() { Content = "failed: " + content, Succeeded = false };

    /// <summary>A refusal that keeps its error code, which the model never reads and a host driving the tools does.</summary>
    public static ToolOutcome Fail(E2EException error) => new() { Content = "failed: " + error.Message, Succeeded = false, Code = error.Code };

    public static ToolOutcome Finish(string status, string? summary, string? code) => new()
    {
        Content = "done " + status,
        Succeeded = true,
        Done = true,
        Status = status,
        Summary = summary,
        Code = code,
    };
}
