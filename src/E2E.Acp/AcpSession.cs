// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>How to start one agent and what its session gets. Upstream's <c>AgentLaunch</c> (<c>session.ts</c>).</summary>
internal sealed record AgentLaunch
{
    public required string Command { get; init; }

    public IReadOnlyList<string> Args { get; init; } = [];

    public IReadOnlyDictionary<string, string>? Env { get; init; }

    public string? Model { get; init; }

    public string? Mode { get; init; }

    public IReadOnlyDictionary<string, object?>? SessionMeta { get; init; }

    /// <summary>How the user signs the agent in, for a session it refuses until they do.</summary>
    public string? SignIn { get; init; }
}

/// <summary>The tokens a turn reported. Upstream reads the SDK's <c>Usage</c>.</summary>
internal sealed record AcpUsage(long TotalTokens, long InputTokens, long OutputTokens, long? CachedReadTokens, long? CachedWriteTokens);

/// <summary>What one prompt turn reported. Upstream's <c>TurnReport</c>.</summary>
internal sealed record TurnReport
{
    public required DateTimeOffset StartedAt { get; init; }

    public required int DurationMs { get; init; }

    public required string StopReason { get; init; }

    public AcpUsage? Usage { get; init; }

    /// <summary>The turn's cost in USD, when the agent reports a session cost.</summary>
    public double? CostUsd { get; init; }

    /// <summary>The agent's text replies during the turn.</summary>
    public required string Text { get; init; }

    /// <summary>Tool calls of the agent's own that asked permission and were rejected.</summary>
    public required IReadOnlyList<string> Rejected { get; init; }

    /// <summary>Tool calls of the agent's own that ran without asking.</summary>
    public required IReadOnlyList<string> Ran { get; init; }
}

/// <summary>A running agent session. Upstream's <c>AcpSession</c>.</summary>
internal interface IAcpSession
{
    Task<TurnReport> PromptAsync(string text);

    /// <summary>Cancels the turn in flight, if any.</summary>
    void Cancel();

    void Close();

    /// <summary>The model the session runs, when the agent names it.</summary>
    string? ModelId { get; }

    string? AgentName { get; }
}

/// <summary>
/// One ACP agent process and one session in it (upstream's <c>session.ts</c>).
/// <para>
/// The agent process starts in the project directory, so a command installed there resolves, and gets <c>initialize</c> and one
/// <c>session/new</c> in an empty temporary directory with the step tools as its only MCP server. The client advertises no file
/// system or terminal capability, and answers permission requests itself: calls of the step tools are allowed, anything else is
/// rejected. Each <c>PromptAsync</c> is one <c>session/prompt</c> and resolves when the agent's turn ends.
/// </para>
/// </summary>
internal sealed class AcpSession : IAcpSession
{
    /// <summary>Tool kinds an agent uses for its own tools and subagents; never a call of ours without an adapter's mark.</summary>
    private static readonly HashSet<string> BuiltInKinds = new(["read", "edit", "delete", "move", "search", "execute", "fetch", "think", "switch_mode"], StringComparer.Ordinal);

    /// <summary>The fields of an MCP call's raw input as adapters report it.</summary>
    private static readonly HashSet<string> McpCallFields = new(["server", "serverName", "server_name", "tool", "toolName", "tool_name", "arguments", "args", "input"], StringComparer.Ordinal);

    /// <summary>ACP's error code for a request the agent refuses until the user signs in.</summary>
    private const int AuthRequired = -32000;

    private readonly AcpConnection _connection;
    private readonly string _sessionId;
    private readonly bool _closeable;
    private readonly Action _teardown;
    private readonly TurnState _turn;
    private bool _closed;

    private AcpSession(AcpConnection connection, string sessionId, string? modelId, string? agentName, bool closeable, TurnState turn, Action teardown)
    {
        _connection = connection;
        _sessionId = sessionId;
        ModelId = modelId;
        AgentName = agentName;
        _closeable = closeable;
        _turn = turn;
        _teardown = teardown;
    }

    public string? ModelId { get; }

    public string? AgentName { get; }

    /// <summary>
    /// Starts the agent, initializes it, and opens a session with the tools. Cancelling <paramref name="cancellationToken"/> before the
    /// session is open stops the agent and the tool server and fails. <paramref name="onOwnTool"/> hears of every tool of the agent's
    /// own that runs without asking, as soon as it starts or finishes.
    /// </summary>
    public static async Task<IAcpSession> StartAsync(AgentLaunch launch, IReadOnlyList<ServedTool> tools, CancellationToken cancellationToken, Action<string> onOwnTool)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cwd = Directory.CreateTempSubdirectory("e2e-acp-").FullName;
        // Started before anything can fail, so teardown can close it even when the startup race is lost while it binds.
        var serving = ToolServer.StartAsync(tools);
        var ours = new HashSet<string>(tools.Select(tool => tool.Name), StringComparer.Ordinal);
        var calls = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var turn = new TurnState();
        AcpConnection connection;
        try
        {
            connection = AcpConnection.Start(
                launch.Command,
                launch.Args,
                launch.Env,
                (method, parameters) => method == "session/request_permission"
                    ? AnswerPermission(parameters!, ours, calls, turn.Own)
                    : throw new AcpRpcException(-32601, "Method not found: " + method),
                (method, parameters) =>
                {
                    if (method == "session/update" && parameters?["update"] is JsonObject update)
                    {
                        Observe(update, ours, calls, turn, onOwnTool);
                    }
                });
        }
        catch
        {
            Cleanup(serving, cwd);
            throw;
        }

        var teardown = () =>
        {
            connection.Dispose();
            Cleanup(serving, cwd);
        };

        // Ends a startup step early when the agent exits or the attempt ends.
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => aborted.TrySetResult());
        async Task<T> Startup<T>(Task<T> step)
        {
            var first = await Task.WhenAny(step, connection.Exited, aborted.Task).ConfigureAwait(false);
            if (first == step)
            {
                return await step.ConfigureAwait(false);
            }

            if (first == aborted.Task)
            {
                throw new InvalidOperationException("the step ended while the agent was starting");
            }

            throw await connection.Exited.ConfigureAwait(false);
        }

        string sessionId;
        string? modelId = null;
        string? agentName;
        bool closeable;
        try
        {
            var server = await Startup(serving).ConfigureAwait(false);
            var initialize = await Startup(connection.RequestAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = 1,
                ["clientCapabilities"] = new JsonObject
                {
                    ["fs"] = new JsonObject { ["readTextFile"] = false, ["writeTextFile"] = false },
                    ["terminal"] = false,
                },
                ["clientInfo"] = new JsonObject { ["name"] = "e2e", ["version"] = "1" },
            })).ConfigureAwait(false);
            if (initialize?["agentCapabilities"]?["mcpCapabilities"]?["http"]?.GetValue<bool>() != true)
            {
                throw new ConfigurationException("INVALID_CONFIG", launch.Command + " does not accept MCP servers over HTTP, which the step tools need");
            }

            agentName = initialize["agentInfo"]?["name"]?.GetValue<string>();
            closeable = initialize["agentCapabilities"]?["sessionCapabilities"]?["close"] is not null;
            var newSession = new JsonObject
            {
                ["cwd"] = cwd,
                ["mcpServers"] = new JsonArray(new JsonObject
                {
                    ["type"] = "http",
                    ["name"] = ToolServer.McpServerName,
                    ["url"] = server.Url,
                    ["headers"] = new JsonArray(),
                }),
            };
            if (launch.SessionMeta is not null)
            {
                newSession["_meta"] = ToNode(launch.SessionMeta);
            }

            var session = await Startup(connection.RequestAsync("session/new", newSession)).ConfigureAwait(false)
                ?? throw new InvalidOperationException("the agent answered session/new with nothing");
            sessionId = session["sessionId"]?.GetValue<string>() ?? throw new InvalidOperationException("the agent opened no session");
            var modelOption = (session["configOptions"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(option => option["category"]?.GetValue<string>() == "model" || option["id"]?.GetValue<string>() == "model");
            if (launch.Model is not null)
            {
                var offered = modelOption is null ? [] : SelectValues(modelOption);
                if (modelOption is null || !offered.Contains(launch.Model, StringComparer.Ordinal))
                {
                    throw new ConfigurationException(
                        "INVALID_CONFIG",
                        "the agent does not offer model " + launch.Model + (offered.Count == 0 ? "" : "; it offers " + string.Join(", ", offered)));
                }

                await Startup<JsonNode?>(SetConfigOptionAsync(connection, sessionId, modelOption["id"]!.GetValue<string>(), launch.Model)).ConfigureAwait(false);
                modelId = launch.Model;
            }
            else if (modelOption?["type"]?.GetValue<string>() == "select")
            {
                modelId = modelOption["currentValue"]?.ToString();
            }

            if (launch.Mode is not null)
            {
                await Startup<JsonNode?>(SelectModeAsync(connection, session, sessionId, launch.Mode)).ConfigureAwait(false);
            }
        }
        catch (Exception error)
        {
            teardown();
            if (error is AcpRpcException { Code: AuthRequired })
            {
                throw new InvalidOperationException("the agent is not signed in" + (launch.SignIn is null ? "" : ": " + launch.SignIn), error);
            }

            throw WithDetails(error);
        }

        return new AcpSession(connection, sessionId, modelId, agentName, closeable, turn, teardown);
    }

    public async Task<TurnReport> PromptAsync(string text)
    {
        _turn.Text.Clear();
        _turn.Own = new OwnCalls();
        var costBefore = _turn.Cost;
        var started = DateTimeOffset.UtcNow;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        JsonNode? response;
        try
        {
            response = await _connection.RequestAsync("session/prompt", new JsonObject
            {
                ["sessionId"] = _sessionId,
                ["prompt"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
            }).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            throw WithDetails(error);
        }

        var usage = response?["usage"] as JsonObject;
        return new TurnReport
        {
            StartedAt = started,
            DurationMs = (int)clock.ElapsedMilliseconds,
            StopReason = response?["stopReason"]?.GetValue<string>() ?? "end_turn",
            Usage = usage is null
                ? null
                : new AcpUsage(Long(usage, "totalTokens") ?? 0, Long(usage, "inputTokens") ?? 0, Long(usage, "outputTokens") ?? 0, Long(usage, "cachedReadTokens"), Long(usage, "cachedWriteTokens")),
            CostUsd = _turn.Cost is { } cost ? cost - (costBefore ?? 0) : null,
            Text = _turn.Text.ToString(),
            Rejected = [.. _turn.Own.Rejected.Values],
            Ran = [.. _turn.Own.Ran.Values],
        };
    }

    public void Cancel() => _connection.Notify("session/cancel", new JsonObject { ["sessionId"] = _sessionId });

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        if (_closeable)
        {
            _ = _connection.RequestAsync("session/close", new JsonObject { ["sessionId"] = _sessionId }).ContinueWith(static task => task.Exception, TaskScheduler.Default);
        }

        _teardown();
    }

    // Closes the tool server and removes the empty directory the session opened in.
    private static void Cleanup(Task<ToolServer> serving, string cwd)
    {
        _ = serving.ContinueWith(
            static async task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    await task.Result.DisposeAsync().ConfigureAwait(false);
                }
            },
            TaskScheduler.Default);
        try
        {
            Directory.Delete(cwd, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temp directory the system will clear.
        }
    }

    /// <summary><paramref name="error"/> with what the agent said about it beyond its message appended, if anything.</summary>
    private static Exception WithDetails(Exception error)
    {
        var details = DetailsOf(error);
        return details is null ? error : new InvalidOperationException(error.Message + ": " + details, error);
    }

    /// <summary>What an agent said about a refused request beyond its message: the Claude adapter's <c>data.details</c>, the Codex adapter's <c>data.message</c>.</summary>
    private static string? DetailsOf(Exception error)
    {
        if (error is not AcpRpcException { Details: { } data })
        {
            return null;
        }

        var details = data is JsonObject fields ? fields["details"] ?? fields["message"] : data;
        return details is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
    }

    private static Task<JsonNode?> SetConfigOptionAsync(AcpConnection connection, string sessionId, string configId, string value) =>
        connection.RequestAsync("session/set_config_option", new JsonObject { ["sessionId"] = sessionId, ["configId"] = configId, ["value"] = value });

    private static async Task<JsonNode?> SelectModeAsync(AcpConnection connection, JsonNode session, string sessionId, string mode)
    {
        var option = (session["configOptions"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(entry => entry["category"]?.GetValue<string>() == "mode");
        if (option is not null && SelectValues(option).Contains(mode, StringComparer.Ordinal))
        {
            await SetConfigOptionAsync(connection, sessionId, option["id"]!.GetValue<string>(), mode).ConfigureAwait(false);
        }
        else if ((session["modes"]?["availableModes"] as JsonArray)?.OfType<JsonObject>().Any(entry => entry["id"]?.GetValue<string>() == mode) == true)
        {
            await connection.RequestAsync("session/set_mode", new JsonObject { ["sessionId"] = sessionId, ["modeId"] = mode }).ConfigureAwait(false);
        }
        else
        {
            throw new ConfigurationException("INVALID_CONFIG", "the agent has no mode " + mode);
        }

        return null;
    }

    /// <summary>The values of a select config option, groups flattened.</summary>
    private static List<string> SelectValues(JsonObject option)
    {
        var values = new List<string>();
        if (option["type"]?.GetValue<string>() != "select" || option["options"] is not JsonArray entries)
        {
            return values;
        }

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (entry["options"] is JsonArray group)
            {
                values.AddRange(group.OfType<JsonObject>().Select(inner => inner["value"]?.GetValue<string>()).OfType<string>());
            }
            else if (entry["value"]?.GetValue<string>() is { } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static void Observe(JsonObject update, HashSet<string> ours, Dictionary<string, JsonObject> calls, TurnState turn, Action<string> onOwnTool)
    {
        switch (update["sessionUpdate"]?.GetValue<string>())
        {
            case "tool_call" or "tool_call_update":
            {
                var id = update["toolCallId"]?.GetValue<string>();
                if (id is null)
                {
                    break;
                }

                var call = Merge(calls.GetValueOrDefault(id), update);
                calls[id] = call;
                // A call of the agent's own that started or finished without a permission we rejected ran without asking:
                // caught when it starts, so a long one cannot feed what it read to the step tools before it ends.
                var status = call["status"]?.GetValue<string>();
                var started = status is "in_progress" or "completed" or "failed";
                var own = turn.Own;
                var ran = started && !own.Allowed.Contains(id) && !own.Rejected.ContainsKey(id) && !own.Ran.ContainsKey(id);
                if (ran && !IsOurs(call, null, ours))
                {
                    var title = TitleOf(call);
                    own.Ran[id] = title;
                    onOwnTool(title);
                }

                break;
            }

            case "agent_message_chunk":
                if (update["content"]?["type"]?.GetValue<string>() == "text")
                {
                    turn.Text.Append(update["content"]!["text"]?.GetValue<string>());
                }

                break;
            case "usage_update":
                if (update["cost"] is JsonObject cost && cost["currency"]?.GetValue<string>() == "USD")
                {
                    turn.Cost = cost["amount"]?.GetValue<double>();
                }

                break;
        }
    }

    /// <summary>Allows calls of the step tools; rejects everything else.</summary>
    private static JsonNode AnswerPermission(JsonObject parameters, HashSet<string> ours, Dictionary<string, JsonObject> calls, OwnCalls own)
    {
        var request = parameters["toolCall"] as JsonObject ?? new JsonObject();
        var id = request["toolCallId"]?.GetValue<string>() ?? "";
        var toolCall = Merge(calls.GetValueOrDefault(id), request);
        var allowed = IsOurs(toolCall, parameters["_meta"] as JsonObject, ours);
        // Remembered, since the call's later updates may not carry what the request named it by.
        if (allowed)
        {
            own.Allowed.Add(id);
        }
        else
        {
            own.Rejected[id] = TitleOf(toolCall);
        }

        string[] kinds = allowed ? ["allow_once", "allow_always"] : ["reject_once", "reject_always"];
        var options = (parameters["options"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var option = kinds.Select(kind => options.FirstOrDefault(entry => entry["kind"]?.GetValue<string>() == kind)).FirstOrDefault(entry => entry is not null);
        return option is null
            ? new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "cancelled" } }
            : new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "selected", ["optionId"] = option["optionId"]!.GetValue<string>() } };
    }

    /// <summary>
    /// Whether a tool call is one of ours. ACP has no standard field for the MCP server behind a call, so this reads what the adapters
    /// fill in, never text the agent wrote: the Claude adapter's <c>_meta.claudeCode.toolName</c>; the server and tool of a call the
    /// Codex adapter marks as an MCP call; and otherwise, for a call of no built-in kind, a raw input that is just a server, a tool,
    /// and its arguments. Anything else is not ours.
    /// </summary>
    private static bool IsOurs(JsonObject toolCall, JsonObject? requestMeta, HashSet<string> ours)
    {
        var meta = toolCall["_meta"] as JsonObject ?? new JsonObject();
        if (meta["claudeCode"] is JsonNode claude)
        {
            var name = (claude as JsonObject)?["toolName"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var prefix = "mcp__" + ToolServer.McpServerName + "__";
            return name is not null && name.StartsWith(prefix, StringComparison.Ordinal) && ours.Contains(name[prefix.Length..]);
        }

        if (meta["is_mcp_tool_call"] is JsonValue marked && marked.TryGetValue<bool>(out var isMcp) && isMcp ||
            requestMeta?["is_mcp_tool_approval"] is JsonValue approval && approval.TryGetValue<bool>(out var approved) && approved)
        {
            return McpCallOf(toolCall["rawInput"], ours);
        }

        if (toolCall["kind"]?.GetValue<string>() is { } kind && BuiltInKinds.Contains(kind))
        {
            return false;
        }

        return McpCallOf(toolCall["rawInput"], ours);
    }

    /// <summary>Whether a raw input is an MCP call of one of our tools: our server, the tool's name, and its arguments, nothing else.</summary>
    private static bool McpCallOf(JsonNode? rawInput, HashSet<string> ours)
    {
        if (rawInput is not JsonObject fields || !fields.All(pair => McpCallFields.Contains(pair.Key)))
        {
            return false;
        }

        var server = FirstOf(fields, "server", "serverName", "server_name");
        var tool = FirstOf(fields, "tool", "toolName", "tool_name");
        return server is JsonValue serverName && serverName.TryGetValue<string>(out var serverText) && serverText == ToolServer.McpServerName &&
            tool is JsonValue toolName && toolName.TryGetValue<string>(out var toolText) && ours.Contains(toolText);
    }

    private static JsonNode? FirstOf(JsonObject fields, params string[] names) => names.Select(name => fields[name]).FirstOrDefault(node => node is not null);

    private static string TitleOf(JsonObject call) => call["title"]?.GetValue<string>() ?? call["kind"]?.GetValue<string>() ?? "tool";

    /// <summary>The call so far with the fields of <paramref name="update"/> over it, leaving out the ones it does not carry.</summary>
    private static JsonObject Merge(JsonObject? call, JsonObject update)
    {
        var merged = new JsonObject();
        foreach (var (name, value) in call ?? new JsonObject())
        {
            merged[name] = value?.DeepClone();
        }

        foreach (var (name, value) in update)
        {
            if (value is not null && name != "sessionUpdate")
            {
                merged[name] = value.DeepClone();
            }
        }

        return merged;
    }

    private static long? Long(JsonObject node, string name) => node[name] is JsonValue value && value.TryGetValue<long>(out var number) ? number : null;

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        IReadOnlyDictionary<string, object?> map => new JsonObject([.. map.Select(pair => KeyValuePair.Create(pair.Key, ToNode(pair.Value)))]),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        System.Collections.IEnumerable items => new JsonArray([.. items.Cast<object?>().Select(ToNode)]),
        JsonNode node => node.DeepClone(),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    /// <summary>
    /// A turn's tool calls by call id: the step tools' calls allowed when they asked, and of the agent's own, the ones rejected when
    /// they asked and the ones that ran anyway.
    /// </summary>
    private sealed class OwnCalls
    {
        public HashSet<string> Allowed { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Rejected { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Ran { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>What the session gathers over one prompt turn.</summary>
    private sealed class TurnState
    {
        public double? Cost { get; set; }

        public StringBuilder Text { get; } = new();

        public OwnCalls Own { get; set; } = new();
    }
}
