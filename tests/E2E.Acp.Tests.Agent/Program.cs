// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// A scripted ACP agent on stdio, for tests: the C# shape of upstream's tests/fixtures/scripted-agent.ts (tester-army/e2e, packages/acp).
// ACP_SCRIPT is a JSON array with one entry per prompt turn, or an object keyed by text the prompt contains, each a list of moves:
// call one of the client's MCP tools, or several at once (batch), ask permission for a tool of the agent's own (own), run one without
// asking (ran), say something, fail the turn with a message in the error's data, or hang until cancelled or for good. Every prompt and
// tool result is appended to ACP_LOG as JSON lines, with the tool list the session saw, and the E2E_ variables and CODEX_CONFIG it
// started with. ACP_USAGE is the usage each turn reports. With ACP_HANG_INIT set, the agent never answers initialize. The agent speaks
// raw newline-delimited JSON-RPC and, as a client of the MCP server it is given, raw streamable HTTP, as the real agents do.
var script = JsonNode.Parse(Environment.GetEnvironmentVariable("ACP_SCRIPT") ?? "[]")!;
var logFile = Environment.GetEnvironmentVariable("ACP_LOG");
var logGate = new object();
void Log(JsonNode entry)
{
    if (logFile is null)
    {
        return;
    }

    lock (logGate)
    {
        File.AppendAllText(logFile, entry.ToJsonString() + "\n");
    }
}

Log(new JsonObject { ["pid"] = Environment.ProcessId });
Log(new JsonObject
{
    ["env"] = new JsonArray(Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(name => name.StartsWith("E2E_", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).Select(name => (JsonNode)name).ToArray()),
    ["codexConfig"] = Environment.GetEnvironmentVariable("CODEX_CONFIG"),
});

var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
var writeGate = new object();
void Send(JsonObject message)
{
    message["jsonrpc"] = "2.0";
    lock (writeGate)
    {
        output.Write(message.ToJsonString() + "\n");
    }
}

var pending = new ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>>();
var nextRequest = 1000L;
async Task<JsonNode?> Ask(string method, JsonNode parameters)
{
    var id = Interlocked.Increment(ref nextRequest);
    var waiting = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
    pending[id] = waiting;
    Send(new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters });
    return await waiting.Task;
}

void Notify(JsonNode update) => Send(new JsonObject { ["method"] = "session/update", ["params"] = new JsonObject { ["sessionId"] = "s1", ["update"] = update } });

using var http = new HttpClient();
string? mcpUrl = null;
var mcpCalls = 0;
async Task<JsonNode?> Mcp(string method, JsonNode? parameters, bool notification = false)
{
    var body = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
    if (parameters is not null)
    {
        body["params"] = parameters;
    }

    if (!notification)
    {
        body["id"] = Interlocked.Increment(ref mcpCalls);
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, mcpUrl) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
    using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    return notification || text.Length == 0 ? null : JsonNode.Parse(text)!["result"];
}

var turn = 0;
var cancelRequested = false;
var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var usage = Environment.GetEnvironmentVariable("ACP_USAGE") ?? """{"totalTokens":15,"inputTokens":10,"outputTokens":5,"cachedReadTokens":4}""";

JsonArray MovesFor(string prompt, int index)
{
    if (script is JsonArray turns)
    {
        return index < turns.Count ? turns[index]!.AsArray() : [];
    }

    foreach (var (key, moves) in script.AsObject())
    {
        if (prompt.Contains(key, StringComparison.Ordinal))
        {
            return moves!.AsArray();
        }
    }

    return [];
}

async Task<JsonNode> Prompt(JsonNode parameters)
{
    var text = string.Join("\n", parameters["prompt"]!.AsArray().Select(block => block!["type"]!.GetValue<string>() == "text" ? block["text"]!.GetValue<string>() : ""));
    Log(new JsonObject { ["prompt"] = text });
    cancelRequested = false;
    var number = ++turn;
    foreach (var move in MovesFor(text, number - 1))
    {
        var node = move!.AsObject();
        if (node.ContainsKey("say"))
        {
            Notify(new JsonObject { ["sessionUpdate"] = "agent_message_chunk", ["content"] = new JsonObject { ["type"] = "text", ["text"] = node["say"]!.DeepClone() } });
        }
        else if (node.ContainsKey("fail"))
        {
            throw new RpcFailure(-32603, "Internal error", new JsonObject { ["message"] = node["fail"]!.DeepClone() });
        }
        else if (node.ContainsKey("own"))
        {
            var title = node["own"]!.GetValue<string>();
            var toolCall = new JsonObject { ["toolCallId"] = $"own-{number}-{title}", ["title"] = title };
            foreach (var (field, name) in new[] { ("kind", "kind"), ("rawInput", "rawInput"), ("meta", "_meta") })
            {
                if (node[field] is { } value)
                {
                    toolCall[name] = value.DeepClone();
                }
            }

            var start = (JsonObject)toolCall.DeepClone();
            start["sessionUpdate"] = "tool_call";
            Notify(start);
            var request = new JsonObject
            {
                ["sessionId"] = "s1",
                ["toolCall"] = toolCall,
                ["options"] = new JsonArray(
                    new JsonObject { ["optionId"] = "yes", ["name"] = "Allow", ["kind"] = "allow_once" },
                    new JsonObject { ["optionId"] = "no", ["name"] = "Reject", ["kind"] = "reject_once" }),
            };
            if (node["requestMeta"] is { } requestMeta)
            {
                request["_meta"] = requestMeta.DeepClone();
            }

            var answer = await Ask("session/request_permission", request);
            Log(new JsonObject { ["permission"] = title, ["outcome"] = answer?["outcome"]?.DeepClone() });
            if (node["status"] is { } status)
            {
                Notify(new JsonObject { ["sessionUpdate"] = "tool_call_update", ["toolCallId"] = toolCall["toolCallId"]!.DeepClone(), ["status"] = status.DeepClone() });
            }
        }
        else if (node.ContainsKey("ran"))
        {
            // A tool of the agent's own that runs without asking.
            var title = node["ran"]!.GetValue<string>();
            var id = $"ran-{number}-{title}";
            var status = node["status"]?.GetValue<string>();
            Notify(new JsonObject { ["sessionUpdate"] = "tool_call", ["toolCallId"] = id, ["title"] = title, ["kind"] = node["kind"]?.GetValue<string>() ?? "read", ["status"] = "in_progress" });
            if (status != "in_progress")
            {
                Notify(new JsonObject { ["sessionUpdate"] = "tool_call_update", ["toolCallId"] = id, ["status"] = status ?? "completed" });
            }

            Log(new JsonObject { ["ran"] = title });
            // An agent's own tool takes a moment: the notification is heard before the next call arrives.
            await Task.Delay(50);
        }
        else if (node.ContainsKey("batch"))
        {
            // Sent at once, the way an agent batches parallel tool calls: each one starts a moment after the one before.
            var calls = new List<Task>();
            foreach (var entry in node["batch"]!.AsArray())
            {
                calls.Add(CallTool(entry!["call"]!.GetValue<string>(), entry["args"]));
                await Task.Delay(40);
            }

            await Task.WhenAll(calls);
        }
        else if (node.ContainsKey("ignoreCancel"))
        {
            await Task.Delay(Timeout.Infinite);
        }
        else if (node.ContainsKey("hang"))
        {
            if (!cancelRequested)
            {
                await cancelled.Task;
            }

            if (node["thenRan"] is { } ran)
            {
                // Logged first: the executor stops this agent as soon as it reports the tool.
                var title = ran.GetValue<string>();
                Log(new JsonObject { ["ran"] = title });
                Notify(new JsonObject { ["sessionUpdate"] = "tool_call", ["toolCallId"] = $"ran-{number}-{title}", ["title"] = title, ["kind"] = "read", ["status"] = "in_progress" });
            }

            return new JsonObject { ["stopReason"] = "cancelled" };
        }
        else
        {
            await CallTool(node["call"]!.GetValue<string>(), node["args"]);
        }
    }

    Notify(new JsonObject { ["sessionUpdate"] = "usage_update", ["used"] = 100, ["size"] = 1000, ["cost"] = new JsonObject { ["amount"] = 0.01 * number, ["currency"] = "USD" } });
    return new JsonObject { ["stopReason"] = "end_turn", ["usage"] = JsonNode.Parse(usage) };
}

async Task<JsonNode> CallTool(string name, JsonNode? arguments)
{
    var result = (await Mcp("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments?.DeepClone() ?? new JsonObject() }))!;
    Log(new JsonObject { ["call"] = name, ["result"] = result.DeepClone() });
    return result;
}

async Task<JsonNode?> NewSession(JsonNode parameters)
{
    var server = parameters["mcpServers"]![0]!;
    mcpUrl = server["url"]!.GetValue<string>();
    await Mcp("initialize", new JsonObject
    {
        ["protocolVersion"] = "2025-06-18",
        ["capabilities"] = new JsonObject(),
        ["clientInfo"] = new JsonObject { ["name"] = "scripted-agent", ["version"] = "1" },
    });
    await Mcp("notifications/initialized", null, notification: true);
    var tools = (await Mcp("tools/list", null))!["tools"]!.AsArray().Select(tool => (JsonNode)tool!["name"]!.GetValue<string>()).ToArray();
    Log(new JsonObject
    {
        ["session"] = new JsonObject
        {
            ["cwd"] = parameters["cwd"]!.DeepClone(),
            ["server"] = server["name"]!.DeepClone(),
            ["meta"] = parameters["_meta"]?.DeepClone(),
            ["tools"] = new JsonArray(tools),
        },
    });
    return new JsonObject
    {
        ["sessionId"] = "s1",
        ["configOptions"] = new JsonArray(new JsonObject
        {
            ["id"] = "model",
            ["name"] = "Model",
            ["category"] = "model",
            ["type"] = "select",
            ["currentValue"] = "fast",
            ["options"] = new JsonArray(
                new JsonObject { ["value"] = "fast", ["name"] = "Fast" },
                new JsonObject { ["value"] = "slow", ["name"] = "Slow" }),
        }),
        ["modes"] = new JsonObject
        {
            ["currentModeId"] = "default",
            ["availableModes"] = new JsonArray(
                new JsonObject { ["id"] = "default", ["name"] = "Default" },
                new JsonObject { ["id"] = "read-only", ["name"] = "Read only" }),
        },
    };
}

async Task Handle(JsonObject message)
{
    var id = message["id"]!.DeepClone();
    var parameters = message["params"] ?? new JsonObject();
    try
    {
        JsonNode? result = message["method"]!.GetValue<string>() switch
        {
            "initialize" => Environment.GetEnvironmentVariable("ACP_HANG_INIT") is null
                ? new JsonObject
                {
                    ["protocolVersion"] = 1,
                    ["agentCapabilities"] = new JsonObject { ["mcpCapabilities"] = new JsonObject { ["http"] = true }, ["sessionCapabilities"] = new JsonObject { ["close"] = new JsonObject() } },
                    ["agentInfo"] = new JsonObject { ["name"] = "scripted-agent", ["version"] = "1" },
                    ["authMethods"] = new JsonArray(),
                }
                : await new TaskCompletionSource<JsonNode?>().Task,
            "session/new" => await NewSession(parameters),
            "session/set_mode" => LogAndAnswer(new JsonObject { ["mode"] = parameters["modeId"]!.DeepClone() }),
            "session/set_config_option" => LogAndAnswer(new JsonObject { ["config"] = new JsonObject { [parameters["configId"]!.GetValue<string>()] = parameters["value"]!.DeepClone() } }, new JsonObject { ["configOptions"] = new JsonArray() }),
            "authenticate" => new JsonObject(),
            "session/close" => LogAndAnswer(new JsonObject { ["closed"] = true }),
            "session/prompt" => await Prompt(parameters),
            _ => throw new RpcFailure(-32601, "Method not found", null),
        };
        Send(new JsonObject { ["id"] = id, ["result"] = result });
    }
    catch (RpcFailure failure)
    {
        Send(new JsonObject { ["id"] = id, ["error"] = new JsonObject { ["code"] = failure.Code, ["message"] = failure.Message, ["data"] = failure.Details } });
    }
}

JsonNode LogAndAnswer(JsonNode entry, JsonNode? answer = null)
{
    Log(entry);
    return answer ?? new JsonObject();
}

string? line;
while ((line = await Console.In.ReadLineAsync()) is not null)
{
    if (line.Length == 0)
    {
        continue;
    }

    var message = JsonNode.Parse(line)!.AsObject();
    if (message.ContainsKey("method") && message.ContainsKey("id"))
    {
        _ = Task.Run(() => Handle(message));
    }
    else if (message["method"]?.GetValue<string>() == "session/cancel")
    {
        Log(new JsonObject { ["cancelled"] = true });
        cancelRequested = true;
        cancelled.TrySetResult();
    }
    else if (message.ContainsKey("id") && pending.TryRemove(message["id"]!.GetValue<long>(), out var waiting))
    {
        waiting.TrySetResult(message["result"]);
    }
}

return 0;

internal sealed class RpcFailure(int code, string message, JsonNode? data) : Exception(message)
{
    public int Code { get; } = code;

    public JsonNode? Details { get; } = data;
}
