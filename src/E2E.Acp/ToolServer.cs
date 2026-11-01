// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>What a tool answers: text, with an image once the step shows pixels. Upstream's <c>ToolResult</c> (<c>mcp.ts</c>).</summary>
internal sealed record ToolResult(string Text, ToolImage? Image = null, bool IsError = false);

/// <summary>A base64 image.</summary>
internal sealed record ToolImage(string Data, string MimeType);

/// <summary>One tool the agent can call: a closed input schema and its handler. Upstream's <c>ServedTool</c>.</summary>
internal sealed class ServedTool
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonElement InputSchema { get; init; }

    public required bool ReadOnly { get; init; }

    public required Func<JsonElement, CancellationToken, Task<ToolResult>> RunAsync { get; init; }
}

/// <summary>
/// Serves the step tools to the agent as one MCP server over streamable HTTP on 127.0.0.1 (upstream's <c>serveTools</c>,
/// <c>mcp.ts</c>). An ACP agent takes tools only as MCP servers, and a stdio server would run in another process, away from the
/// step's context; over HTTP every call runs here, in the test process. The port is ephemeral and the path random per session, so
/// nothing else on the machine guesses it. Every reply is one JSON body: the server keeps no session and opens no event stream.
/// </summary>
internal sealed class ToolServer : IAsyncDisposable
{
    /// <summary>The MCP server name the agent sees. Distinct from <c>e2e mcp</c>'s <c>e2e</c>, so a permission request naming it is about these tools.</summary>
    public const string McpServerName = "e2e_step";

    private const string ProtocolVersion = "2025-06-18";

    private readonly IReadOnlyList<ServedTool> _tools;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopped = new();
    private readonly string _route = "/" + Guid.NewGuid().ToString("D") + "/mcp";
    private Task _loop = Task.CompletedTask;

    private ToolServer(IReadOnlyList<ServedTool> tools, int port)
    {
        _tools = tools;
        Url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + _route;
        _listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
    }

    public string Url { get; }

    /// <summary>Starts the tool server on an ephemeral 127.0.0.1 port.</summary>
    public static Task<ToolServer> StartAsync(IReadOnlyList<ServedTool> tools)
    {
        // A port another process takes between the probe and the bind fails the listener; ask again a few times.
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var server = new ToolServer(tools, port);
            try
            {
                server._listener.Start();
            }
            catch (HttpListenerException) when (attempt < 4)
            {
                server._listener.Close();
                server._stopped.Dispose();
                continue;
            }

            server._loop = Task.Run(server.AcceptAsync);
            return Task.FromResult(server);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopped.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener was closed under the accept.
        }

        _stopped.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stopped.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            if (!string.Equals(context.Request.Url?.AbsolutePath, _route, StringComparison.Ordinal))
            {
                response.StatusCode = 404;
            }
            else if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.Ordinal))
            {
                // No event stream and no session to end.
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST");
            }
            else
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync(_stopped.Token).ConfigureAwait(false);
                var reply = await DispatchAsync(body).ConfigureAwait(false);
                if (reply is null)
                {
                    response.StatusCode = 202;
                }
                else
                {
                    var bytes = Encoding.UTF8.GetBytes(reply);
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.ContentLength64 = bytes.Length;
                    await response.OutputStream.WriteAsync(bytes, _stopped.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            try
            {
                response.StatusCode = 500;
            }
            catch (Exception inner) when (inner is InvalidOperationException or ObjectDisposedException or HttpListenerException)
            {
                // The reply had begun.
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException or InvalidOperationException)
            {
                // The client went away.
            }
        }
    }

    /// <summary>Answers one JSON-RPC message or batch; null for notifications and responses, which take a bare 202.</summary>
    private async Task<string?> DispatchAsync(string body)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return Error(null, -32700, "Parse error");
        }

        if (message is JsonArray batch)
        {
            var replies = new JsonArray();
            foreach (var item in batch)
            {
                if (await AnswerAsync(item).ConfigureAwait(false) is { } reply)
                {
                    replies.Add(reply);
                }
            }

            return replies.Count == 0 ? null : replies.ToJsonString();
        }

        return (await AnswerAsync(message).ConfigureAwait(false))?.ToJsonString();
    }

    private async Task<JsonNode?> AnswerAsync(JsonNode? message)
    {
        if (message is not JsonObject request || request["method"] is not JsonValue methodValue || !methodValue.TryGetValue<string>(out var method))
        {
            return null;
        }

        var id = request["id"]?.DeepClone();
        if (id is null)
        {
            return null;
        }

        var parameters = request["params"] as JsonObject;
        try
        {
            JsonNode result = method switch
            {
                "initialize" => new JsonObject
                {
                    ["protocolVersion"] = parameters?["protocolVersion"]?.GetValue<string>() ?? ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = McpServerName, ["version"] = "1" },
                },
                "ping" => new JsonObject(),
                "tools/list" => new JsonObject { ["tools"] = new JsonArray([.. _tools.Select(Describe)]) },
                "tools/call" => await CallAsync(parameters).ConfigureAwait(false),
                _ => throw new McpError(-32601, "Method not found: " + method),
            };
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        }
        catch (McpError error)
        {
            return JsonNode.Parse(Error(id, error.Code, error.Message));
        }
    }

    private static JsonObject Describe(ServedTool tool) => new()
    {
        ["name"] = tool.Name,
        ["description"] = tool.Description,
        ["inputSchema"] = JsonNode.Parse(tool.InputSchema.GetRawText()),
        ["annotations"] = new JsonObject { ["readOnlyHint"] = tool.ReadOnly, ["openWorldHint"] = false },
    };

    private async Task<JsonNode> CallAsync(JsonObject? parameters)
    {
        var name = parameters?["name"]?.GetValue<string>();
        var tool = _tools.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal))
            ?? throw new McpError(-32602, "Unknown tool: " + name);
        var arguments = parameters?["arguments"] ?? new JsonObject();
        if (arguments is not JsonObject)
        {
            throw new McpError(-32602, "Invalid arguments for tool " + name);
        }

        using var document = JsonDocument.Parse(arguments.ToJsonString());
        var result = await tool.RunAsync(document.RootElement.Clone(), _stopped.Token).ConfigureAwait(false);
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result.Text } };
        if (result.Image is { } image)
        {
            content.Add(new JsonObject { ["type"] = "image", ["data"] = image.Data, ["mimeType"] = image.MimeType });
        }

        var reply = new JsonObject { ["content"] = content };
        if (result.IsError)
        {
            reply["isError"] = true;
        }

        return reply;
    }

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString();

    private sealed class McpError(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
