// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Acp;

/// <summary>How to start one agent and what its session gets. Upstream's <c>AgentLaunch</c> (<c>session.ts</c>).</summary>
/// <param name="Command">The command that starts the agent in ACP mode on stdio.</param>
/// <param name="Args">Its arguments, passed one by one: nothing goes through a shell.</param>
/// <param name="Env">Variables added to the agent's environment, on top of this process's.</param>
/// <param name="Model">A value of the session's <c>model</c> config option.</param>
/// <param name="Mode">A session mode id to select.</param>
/// <param name="SessionMeta"><c>_meta</c> for <c>session/new</c>.</param>
/// <param name="SignIn">How the user signs the agent in, for a session it refuses until they do.</param>
internal sealed record AgentLaunch(
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string>? Env = null,
    string? Model = null,
    string? Mode = null,
    IReadOnlyDictionary<string, object?>? SessionMeta = null,
    string? SignIn = null);

/// <summary>What an agent reports of a prompt turn's token use. ACP's <c>Usage</c>.</summary>
internal sealed record TurnUsage(long TotalTokens, long InputTokens, long OutputTokens, long? CachedReadTokens, long? CachedWriteTokens);

/// <summary>What one prompt turn reported. Upstream's <c>TurnReport</c> (<c>session.ts</c>).</summary>
/// <param name="StartedAt">When the turn began.</param>
/// <param name="DurationMs">How long it took.</param>
/// <param name="StopReason">Why the agent ended it.</param>
/// <param name="Usage">The agent's token use for the turn.</param>
/// <param name="CostUsd">The turn's cost in USD, when the agent reports a session cost.</param>
/// <param name="Text">The agent's text replies during the turn.</param>
/// <param name="Rejected">Tool calls of the agent's own that asked permission and were rejected.</param>
/// <param name="Ran">Tool calls of the agent's own that ran without asking.</param>
internal sealed record TurnReport(
    DateTimeOffset StartedAt,
    int DurationMs,
    string StopReason,
    TurnUsage? Usage,
    double? CostUsd,
    string Text,
    IReadOnlyList<string> Rejected,
    IReadOnlyList<string> Ran);

/// <summary>
/// One ACP agent process and one session in it. The agent process starts in the project directory, so a command installed there
/// resolves, and gets <c>initialize</c> and one <c>session/new</c> in an empty temporary directory with the step tools as its only MCP
/// server. The client advertises no file system or terminal capability, and answers permission requests itself: calls of the step
/// tools are allowed, anything else is rejected. Each <see cref="PromptAsync"/> is one <c>session/prompt</c> and resolves when the
/// agent's turn ends. Upstream's <c>AcpSession</c> and <c>startSession</c> (<c>session.ts</c>).
/// </summary>
internal sealed partial class AcpSession
{
    /// <summary>The ACP protocol version the client speaks. Pinned: upstream takes the SDK's <c>PROTOCOL_VERSION</c>.</summary>
    internal const int ProtocolVersion = 1;

    /// <summary>ACP's error code for a request the agent refuses until the user signs in.</summary>
    private const int AuthRequired = -32000;

    /// <summary>The most stderr kept for a failure message.</summary>
    private const int StderrLimit = 2_000;

    /// <summary>Tool kinds an agent uses for its own tools and subagents; never a call of ours without an adapter's mark.</summary>
    private static readonly HashSet<string> BuiltInKinds = new(["read", "edit", "delete", "move", "search", "execute", "fetch", "think", "switch_mode"], StringComparer.Ordinal);

    /// <summary>The fields of an MCP call's raw input as adapters report it.</summary>
    private static readonly HashSet<string> McpCallFields = new(["server", "serverName", "server_name", "tool", "toolName", "tool_name", "arguments", "args", "input"], StringComparer.Ordinal);

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly AgentLaunch _launch;
    private readonly HashSet<string> _ours;
    private readonly IReadOnlyList<ServedTool> _tools;
    private readonly Action<string> _onOwnTool;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly Lock _turnGate = new();
    private readonly Dictionary<string, CallFields> _calls = new(StringComparer.Ordinal);
    private readonly StringBuilder _stderr = new();
    private readonly string _directory;
    private TurnState _turn = new();
    private Process? _process;
    private McpToolServer? _server;
    private JsonRpcConnection? _connection;
    private Task _stderrReader = Task.CompletedTask;
    private string? _sessionId;
    private bool _canClose;
    private int _closed;
    private double? _cost;

    private AcpSession(AgentLaunch launch, IReadOnlyList<ServedTool> tools, Action<string> onOwnTool)
    {
        _launch = launch;
        _tools = tools;
        _ours = new HashSet<string>(tools.Select(tool => tool.Name), StringComparer.Ordinal);
        _onOwnTool = onOwnTool;
        _lifetimeToken = _lifetime.Token;
        _directory = Directory.CreateTempSubdirectory("e2e-acp-").FullName;
    }

    /// <summary>The model the session runs, when the agent names it.</summary>
    public string? ModelId { get; private set; }

    public string? AgentName { get; private set; }

    /// <summary>Whether <see cref="Close"/> ran: the agent is stopped and the session takes no more prompts.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    /// <summary>
    /// Starts the agent, initializes it, and opens a session with the tools. Cancelling <paramref name="token"/> before the session is open
    /// stops the agent and the tool server and fails. <paramref name="onOwnTool"/> hears of every tool of the agent's own that runs
    /// without asking, as soon as it starts or finishes. Upstream's <c>startSession</c>.
    /// </summary>
    public static async Task<AcpSession> StartAsync(AgentLaunch launch, IReadOnlyList<ServedTool> tools, CancellationToken token, Action<string>? onOwnTool = null)
    {
        token.ThrowIfCancellationRequested();
        var session = new AcpSession(launch, tools, onOwnTool ?? (_ => { }));
        try
        {
            await session.OpenAsync(token).ConfigureAwait(false);
            return session;
        }
        catch (Exception error)
        {
            session.Close();
            if (error is OperationCanceledException && token.IsCancellationRequested)
            {
                throw new InvalidOperationException("the step ended while the agent was starting", error);
            }

            if (error is AcpRequestException { Code: AuthRequired })
            {
                throw new InvalidOperationException($"the agent is not signed in{(launch.SignIn is null ? "" : $": {launch.SignIn}")}", error);
            }

            throw WithDetails(error);
        }
    }

    /// <summary>Sends one prompt and resolves when the agent's turn ends. The wait lasts as long as the session: a step that gives up on it calls <see cref="Cancel"/>.</summary>
    public async Task<TurnReport> PromptAsync(string text)
    {
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        double? costBefore;
        lock (_turnGate)
        {
            _turn = new TurnState();
            costBefore = _cost;
        }

        JsonElement response;
        try
        {
            response = await _connection!.RequestAsync(
                "session/prompt",
                new JsonObject { ["sessionId"] = _sessionId, ["prompt"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }) },
                _lifetimeToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw WithDetails(error);
        }

        lock (_turnGate)
        {
            return new TurnReport(
                started,
                (int)clock.ElapsedMilliseconds,
                Text(response, "stopReason") ?? "unknown",
                UsageOf(response),
                _cost is { } cost ? cost - (costBefore ?? 0) : null,
                _turn.Text.ToString(),
                [.. _turn.Rejected.Values],
                [.. _turn.Ran.Values]);
        }
    }

    /// <summary>Cancels the turn in flight, if any.</summary>
    public void Cancel()
    {
        if (_sessionId is null || _connection is null || Volatile.Read(ref _closed) == 1)
        {
            return;
        }

        _ = _connection.NotifyAsync("session/cancel", new JsonObject { ["sessionId"] = _sessionId }, CancellationToken.None).ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Ends the session: the agent process and everything under it, the tool server, and the temporary directory. Synchronous to the end,
    /// so a process exiting right after the attempt still stops the agent. Safe to call twice.
    /// </summary>
    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        try
        {
            if (_canClose && _sessionId is not null && _connection is not null)
            {
                _ = _connection.RequestAsync("session/close", new JsonObject { ["sessionId"] = _sessionId }, CancellationToken.None).ContinueWith(
                    static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }
        }
        finally
        {
            Teardown();
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;

    private static TurnUsage? UsageOf(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new TurnUsage(Number(usage, "totalTokens") ?? 0, Number(usage, "inputTokens") ?? 0, Number(usage, "outputTokens") ?? 0, Number(usage, "cachedReadTokens"), Number(usage, "cachedWriteTokens"));
    }

    /// <summary><paramref name="error"/> with what the agent said about it beyond its message appended, if anything.</summary>
    private static Exception WithDetails(Exception error) =>
        error is AcpRequestException request && DetailsOf(request) is { } details ? new InvalidOperationException($"{error.Message}: {details}", error) : error;

    /// <summary>What an agent said about a refused request beyond its message: the Claude adapter's <c>data.details</c>, the Codex adapter's <c>data.message</c>.</summary>
    private static string? DetailsOf(AcpRequestException error)
    {
        var data = error.ErrorData;
        var details = data.ValueKind == JsonValueKind.Object ? Text(data, "details") ?? Text(data, "message") : data.ValueKind == JsonValueKind.String ? data.GetString() : null;
        return string.IsNullOrEmpty(details) ? null : details;
    }

    /// <summary>The values of a select config option, groups flattened.</summary>
    private static List<string> SelectValues(JsonElement option)
    {
        var values = new List<string>();
        if (Text(option, "type") != "select" || !option.TryGetProperty("options", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("group", out _) && entry.TryGetProperty("options", out var inner) && inner.ValueKind == JsonValueKind.Array)
            {
                values.AddRange(inner.EnumerateArray().Select(item => Text(item, "value")).OfType<string>());
            }
            else if (Text(entry, "value") is { } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string DisplayOf(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static string LastLine(string text) => text.Trim().Split('\n')[^1].TrimEnd('\r');

    private async Task OpenAsync(CancellationToken token)
    {
        _server = McpToolServer.Start(_tools);
        StartProcess();

        var initialize = await RequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["clientCapabilities"] = new JsonObject { ["fs"] = new JsonObject { ["readTextFile"] = false, ["writeTextFile"] = false }, ["terminal"] = false },
            ["clientInfo"] = new JsonObject { ["name"] = "e2e", ["version"] = "1" },
        }, token).ConfigureAwait(false);
        if (Number(initialize, "protocolVersion") != ProtocolVersion)
        {
            throw new ConfigurationException("INVALID_CONFIG", $"{_launch.Command} speaks another version of the Agent Client Protocol than {ProtocolVersion}, which this client needs");
        }

        JsonElement capabilities = initialize.TryGetProperty("agentCapabilities", out var found) ? found : default;
        var http = capabilities.ValueKind == JsonValueKind.Object && capabilities.TryGetProperty("mcpCapabilities", out var mcp) && mcp.ValueKind == JsonValueKind.Object
            && mcp.TryGetProperty("http", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (!http)
        {
            throw new ConfigurationException("INVALID_CONFIG", $"{_launch.Command} does not accept MCP servers over HTTP, which the step tools need");
        }

        _canClose = capabilities.TryGetProperty("sessionCapabilities", out var sessionCapabilities) && sessionCapabilities.ValueKind == JsonValueKind.Object
            && sessionCapabilities.TryGetProperty("close", out var close) && close.ValueKind != JsonValueKind.Null;
        AgentName = initialize.TryGetProperty("agentInfo", out var info) ? Text(info, "name") : null;

        var parameters = new JsonObject
        {
            ["cwd"] = _directory,
            ["mcpServers"] = new JsonArray(new JsonObject { ["type"] = "http", ["name"] = McpToolServer.ServerName, ["url"] = _server.Url, ["headers"] = new JsonArray() }),
        };
        if (_launch.SessionMeta is not null)
        {
            parameters["_meta"] = JsonSerializer.SerializeToNode(_launch.SessionMeta);
        }

        var session = await RequestAsync("session/new", parameters, token).ConfigureAwait(false);
        _sessionId = Text(session, "sessionId") ?? throw new InvalidOperationException("the agent answered session/new without a session id");

        var options = session.TryGetProperty("configOptions", out var configOptions) && configOptions.ValueKind == JsonValueKind.Array ? configOptions.EnumerateArray().ToList() : [];
        JsonElement? modelOption = options.Where(option => Text(option, "category") == "model" || Text(option, "id") == "model").Select(option => (JsonElement?)option).FirstOrDefault();
        if (_launch.Model is { } model)
        {
            var offered = modelOption is { } choice ? SelectValues(choice) : [];
            if (modelOption is null || !offered.Contains(model))
            {
                throw new ConfigurationException("INVALID_CONFIG", $"the agent does not offer model {model}{(offered.Count == 0 ? "" : $"; it offers {string.Join(", ", offered)}")}");
            }

            await SetConfigOptionAsync(Text(modelOption.Value, "id")!, model, token).ConfigureAwait(false);
            ModelId = model;
        }
        else if (modelOption is { } current && Text(current, "type") == "select" && current.TryGetProperty("currentValue", out var value))
        {
            ModelId = DisplayOf(value);
        }

        if (_launch.Mode is { } mode)
        {
            await SelectModeAsync(session, options, mode, token).ConfigureAwait(false);
        }
    }

    private async Task SelectModeAsync(JsonElement session, List<JsonElement> options, string mode, CancellationToken token)
    {
        JsonElement? option = options.Where(entry => Text(entry, "category") == "mode").Select(entry => (JsonElement?)entry).FirstOrDefault();
        if (option is { } configured && SelectValues(configured).Contains(mode))
        {
            await SetConfigOptionAsync(Text(configured, "id")!, mode, token).ConfigureAwait(false);
        }
        else if (session.TryGetProperty("modes", out var modes) && modes.ValueKind == JsonValueKind.Object && modes.TryGetProperty("availableModes", out var available)
            && available.ValueKind == JsonValueKind.Array && available.EnumerateArray().Any(entry => Text(entry, "id") == mode))
        {
            await RequestAsync("session/set_mode", new JsonObject { ["sessionId"] = _sessionId, ["modeId"] = mode }, token).ConfigureAwait(false);
        }
        else
        {
            throw new ConfigurationException("INVALID_CONFIG", $"the agent has no mode {mode}");
        }
    }

    private Task SetConfigOptionAsync(string id, string value, CancellationToken token) =>
        RequestAsync("session/set_config_option", new JsonObject { ["sessionId"] = _sessionId, ["configId"] = id, ["value"] = value }, token);

    private async Task<JsonElement> RequestAsync(string method, JsonNode? parameters, CancellationToken token)
    {
        // Linked to the session's own end, so a Close() from another thread frees a startup that waits.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetimeToken);
        return await _connection!.RequestAsync(method, parameters, linked.Token).ConfigureAwait(false);
    }

    private void StartProcess()
    {
        var start = new ProcessStartInfo(_launch.Command)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in _launch.Args)
        {
            start.ArgumentList.Add(argument);
        }

        WithoutSecrets(start, _launch.Env);
        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.Exited += (_, _) => _ = Task.Run(OnExitedAsync);
        _process = process;
        try
        {
            process.Start();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            throw new InvalidOperationException($"could not start {_launch.Command}: {error.Message}", error);
        }

        _stderrReader = Task.Run(() => ReadStderrAsync(process.StandardError));
        _connection = new JsonRpcConnection(process.StandardInput.BaseStream, process.StandardOutput, OnRequest, OnNotification);
        _connection.Start();
    }

    /// <summary>The variables e2e reads secrets and credentials from, in any casing.</summary>
    [GeneratedRegex(@"^E2E_(?:SECRET|USER)_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretVariable();

    /// <summary>
    /// Sets <paramref name="env"/> on top of this process's environment for <paramref name="start"/>, without the variables e2e reads secrets from,
    /// wherever they came from. Upstream's <c>withoutSecrets</c> (<c>session.ts</c>).
    /// </summary>
    internal static void WithoutSecrets(ProcessStartInfo start, IReadOnlyDictionary<string, string>? env)
    {
        if (env is not null)
        {
            foreach (var (name, value) in env)
            {
                start.Environment[name] = value;
            }
        }

        foreach (var name in start.Environment.Keys.Where(name => SecretVariable().IsMatch(name)).ToList())
        {
            start.Environment.Remove(name);
        }
    }

    /// <summary>Reads stderr as the agent writes it, so a full pipe never stops the agent, and keeps its last lines for a failure message.</summary>
    private async Task ReadStderrAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), _lifetimeToken).ConfigureAwait(false)) > 0)
            {
                lock (_stderr)
                {
                    _stderr.Append(buffer, 0, read);
                    if (_stderr.Length > StderrLimit)
                    {
                        _stderr.Remove(0, _stderr.Length - StderrLimit);
                    }
                }
            }
        }
        catch (Exception error) when (error is OperationCanceledException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The agent is gone.
        }
    }

    private async Task OnExitedAsync()
    {
        try
        {
            await _stderrReader.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        {
            // Describe the exit with what the agent said so far.
        }

        string tail;
        lock (_stderr)
        {
            tail = _stderr.Length == 0 ? "" : $": {LastLine(_stderr.ToString())}";
        }

        string how;
        try
        {
            how = $"code {_process!.ExitCode}";
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException)
        {
            how = "killed";
        }

        _connection?.Fail(new IOException($"the agent exited ({how}){tail}"));
    }

    private void Teardown()
    {
        try
        {
            _lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already ended.
        }

        _connection?.Dispose();
        var process = _process;
        if (process is not null)
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
            {
                // The pipe is gone.
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException)
            {
                // Not started, already exited, or part of the tree could not be killed.
            }
        }

        _server?.Dispose();
        // Try at once, then once the agent is gone: removing at once avoids leaking the directory when the worker exits before the
        // agent does, and on Windows a process the agent started in the directory still holds it, so an early removal throws.
        if (!TryRemoveDirectory() && process is not null)
        {
            _ = RetryRemoveDirectoryAfterExitAsync(process);
        }
        else
        {
            process?.Dispose();
        }

        _lifetime.Dispose();
    }

    private bool TryRemoveDirectory()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Waits for the agent to exit, then retries the removal 10 times at 200 ms, and disposes the process. Left to the temporary directory's own cleanup if it still fails.</summary>
    private async Task RetryRemoveDirectoryAfterExitAsync(Process process)
    {
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or InvalidOperationException)
            {
                // The agent did not exit in time: try anyway.
            }

            for (var attempt = 0; attempt < 10 && !TryRemoveDirectory(); attempt++)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private JsonNode? OnRequest(string method, JsonElement parameters) =>
        method == "session/request_permission" ? AnswerPermission(parameters) : throw new AcpRequestException(JsonRpcConnection.MethodNotFound, "Method not found", default);

    private void OnNotification(string method, JsonElement parameters)
    {
        if (method == "session/update" && parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("update", out var update))
        {
            Observe(update);
        }
    }

    /// <summary>Allows calls of the step tools; rejects everything else, and when a request does not read, cancels it: it fails closed.</summary>
    private JsonNode AnswerPermission(JsonElement parameters)
    {
        try
        {
            if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("toolCall", out var requested) && requested.ValueKind == JsonValueKind.Object
                && Text(requested, "toolCallId") is { } callId && parameters.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
            {
                CallFields call;
                lock (_turnGate)
                {
                    call = _calls.GetValueOrDefault(callId)?.Merged(requested) ?? CallFields.From(requested);
                }

                var allowed = IsOurs(call, parameters.TryGetProperty("_meta", out var requestMeta) ? requestMeta : default);
                lock (_turnGate)
                {
                    // Remembered, since the call's later updates may not carry what the request named it by.
                    if (allowed)
                    {
                        _turn.Allowed.Add(callId);
                    }
                    else
                    {
                        _turn.Rejected[callId] = call.Title;
                    }
                }

                string[] kinds = allowed ? ["allow_once", "allow_always"] : ["reject_once", "reject_always"];
                foreach (var kind in kinds)
                {
                    var option = options.EnumerateArray().FirstOrDefault(entry => Text(entry, "kind") == kind);
                    if (option.ValueKind == JsonValueKind.Object && Text(option, "optionId") is { } optionId)
                    {
                        return new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "selected", ["optionId"] = optionId } };
                    }
                }
            }
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or JsonException)
        {
            // Unreadable: refuse.
        }

        return new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "cancelled" } };
    }

    private void Observe(JsonElement update)
    {
        switch (Text(update, "sessionUpdate"))
        {
            case "tool_call" or "tool_call_update" when Text(update, "toolCallId") is { } callId:
                string? title = null;
                lock (_turnGate)
                {
                    var call = _calls.GetValueOrDefault(callId)?.Merged(update) ?? CallFields.From(update);
                    _calls[callId] = call;
                    // A call of the agent's own that started or finished without a permission we rejected ran without asking:
                    // caught when it starts, so a long one cannot feed what it read to the step tools before it ends.
                    var started = call.Status is "in_progress" or "completed" or "failed";
                    var turn = _turn;
                    var ran = started && !turn.Allowed.Contains(callId) && !turn.Rejected.ContainsKey(callId) && !turn.Ran.ContainsKey(callId);
                    if (ran && !IsOurs(call, default))
                    {
                        title = call.Title;
                        turn.Ran[callId] = title;
                    }
                }

                if (title is not null)
                {
                    _onOwnTool(title);
                }

                break;
            case "agent_message_chunk":
                if (update.TryGetProperty("content", out var content) && Text(content, "type") == "text" && Text(content, "text") is { } chunk)
                {
                    lock (_turnGate)
                    {
                        _turn.Text.Append(chunk);
                    }
                }

                break;
            case "usage_update":
                if (update.TryGetProperty("cost", out var cost) && cost.ValueKind == JsonValueKind.Object && Text(cost, "currency") == "USD"
                    && cost.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number)
                {
                    lock (_turnGate)
                    {
                        _cost = amount.GetDouble();
                    }
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Whether a tool call is one of ours. ACP has no standard field for the MCP server behind a call, so this reads what the adapters
    /// fill in, never text the agent wrote: the Claude adapter's <c>_meta.claudeCode.toolName</c>; the server and tool of a call the Codex
    /// adapter marks as an MCP call; and otherwise, for a call of no built-in kind, a raw input that is just a server, a tool, and its
    /// arguments. Anything else is not ours. Upstream's <c>isOurs</c>.
    /// </summary>
    private bool IsOurs(CallFields call, JsonElement requestMeta)
    {
        var meta = call.Meta;
        if (meta is { ValueKind: JsonValueKind.Object } && meta.Value.TryGetProperty("claudeCode", out var claude))
        {
            var prefix = $"mcp__{McpToolServer.ServerName}__";
            return Text(claude, "toolName") is { } name && name.StartsWith(prefix, StringComparison.Ordinal) && _ours.Contains(name[prefix.Length..]);
        }

        var marked = meta is { ValueKind: JsonValueKind.Object } && meta.Value.TryGetProperty("is_mcp_tool_call", out var flag) && flag.ValueKind == JsonValueKind.True;
        var approval = requestMeta.ValueKind == JsonValueKind.Object && requestMeta.TryGetProperty("is_mcp_tool_approval", out var approved) && approved.ValueKind == JsonValueKind.True;
        if (marked || approval)
        {
            return IsMcpCall(call.RawInput);
        }

        if (call.Kind is { } kind && BuiltInKinds.Contains(kind))
        {
            return false;
        }

        return IsMcpCall(call.RawInput);
    }

    /// <summary>Whether a raw input is an MCP call of one of our tools: our server, the tool's name, and its arguments, nothing else.</summary>
    private bool IsMcpCall(JsonElement? rawInput)
    {
        if (rawInput is not { ValueKind: JsonValueKind.Object } fields)
        {
            return false;
        }

        if (fields.EnumerateObject().Any(field => !McpCallFields.Contains(field.Name)))
        {
            return false;
        }

        var server = First(fields, "server", "serverName", "server_name");
        var tool = First(fields, "tool", "toolName", "tool_name");
        return server is { ValueKind: JsonValueKind.String } && server.Value.GetString() == McpToolServer.ServerName
            && tool is { ValueKind: JsonValueKind.String } && _ours.Contains(tool.Value.GetString()!);
    }

    /// <summary>The first of these fields that is set: JavaScript's <c>??</c> over them.</summary>
    private static JsonElement? First(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>What the session gathers over one prompt turn.</summary>
    private sealed class TurnState
    {
        public StringBuilder Text { get; } = new();

        public HashSet<string> Allowed { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Rejected { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Ran { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>What a tool call is known by so far: a call's updates add to what its start said.</summary>
    private sealed record CallFields(string? Kind, string? Status, string? RawTitle, JsonElement? RawInput, JsonElement? Meta)
    {
        public string Title => RawTitle ?? Kind ?? "tool";

        public static CallFields From(JsonElement call) => new CallFields(null, null, null, null, null).Merged(call);

        public CallFields Merged(JsonElement call) => new(
            Text(call, "kind") ?? Kind,
            Text(call, "status") ?? Status,
            Text(call, "title") ?? RawTitle,
            Set(call, "rawInput") ?? RawInput,
            Set(call, "_meta") ?? Meta);

        private static JsonElement? Set(JsonElement call, string name) =>
            call.ValueKind == JsonValueKind.Object && call.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;
    }
}
