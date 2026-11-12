// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>What a tool answers: text, with a screenshot once the step shows pixels. Upstream's <c>ToolResult</c> (<c>mcp.ts</c>).</summary>
/// <param name="Text">The text the agent reads.</param>
/// <param name="Image">A base64 image, with its media type.</param>
/// <param name="IsError">Whether the call failed.</param>
internal sealed record ToolResult(string Text, (string Data, string MimeType)? Image = null, bool IsError = false);

/// <summary>One tool the agent can call: a closed input schema and its handler. Upstream's <c>ServedTool</c> (<c>mcp.ts</c>).</summary>
internal sealed record ServedTool(string Name, string Description, JsonElement InputSchema, bool ReadOnly, Func<JsonElement, CancellationToken, Task<ToolResult>> RunAsync);

/// <summary>
/// Serves the step tools to the agent as one MCP server over streamable HTTP on 127.0.0.1. An ACP agent takes tools only as MCP
/// servers, and a stdio server would run in another process, away from the step's context; over HTTP every call runs here, in the
/// test process. The port is ephemeral and the path random per session, so nothing else on the machine guesses it. Upstream's
/// <c>serveTools</c> (<c>mcp.ts</c>) delegates the protocol to <c>@modelcontextprotocol/server</c>; the port answers the stateless
/// subset by hand (<c>initialize</c>, <c>ping</c>, <c>tools/list</c>, <c>tools/call</c>), which is all an agent asks of tools.
/// </summary>
internal sealed class McpToolServer : IDisposable
{
    /// <summary>The MCP server name the agent sees. Distinct from <c>e2e mcp</c>'s <c>e2e</c>, so a permission request naming it is about these tools. Upstream's <c>MCP_SERVER_NAME</c>.</summary>
    internal const string ServerName = "e2e_step";

    /// <summary>Largest request body the server reads: a tool call carries a few arguments.</summary>
    private const int MaxBodyBytes = 1 << 20;

    private static readonly JsonElement EmptyArguments = JsonSerializer.SerializeToElement(new { });

    private static readonly string[] ProtocolVersions = ["2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"];

    private readonly HttpListener _listener;
    private readonly string _route;
    private readonly IReadOnlyList<ServedTool> _tools;
    private readonly CancellationTokenSource _stopped = new();
    private readonly CancellationToken _stopToken;

    private McpToolServer(HttpListener listener, string route, string url, IReadOnlyList<ServedTool> tools)
    {
        _listener = listener;
        _route = route;
        Url = url;
        _tools = tools;
        _stopToken = _stopped.Token;
    }

    /// <summary>The address the agent connects to.</summary>
    public string Url { get; }

    /// <summary>Starts the tool server on an ephemeral 127.0.0.1 port. Upstream's <c>serveTools</c>.</summary>
    public static McpToolServer Start(IReadOnlyList<ServedTool> tools)
    {
        var path = "/" + Guid.NewGuid().ToString("N");
        // HttpListener cannot bind port 0: take a free one, and try again when another process takes it first.
        for (var attempt = 0; ; attempt++)
        {
            var port = FreePort();
            var prefix = $"http://127.0.0.1:{port}{path}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
                continue;
            }

            var server = new McpToolServer(listener, path + "/mcp", prefix + "mcp", tools);
            _ = Task.Run(server.AcceptAsync);
            return server;
        }
    }

    public void Dispose()
    {
        if (_stopped.IsCancellationRequested)
        {
            return;
        }

        _stopped.Cancel();
        _listener.Close();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task AcceptAsync()
    {
        while (!_stopToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
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
            var request = context.Request;
            // A page in a browser can reach 127.0.0.1 too; an agent sends no Origin.
            if (request.Url?.AbsolutePath != _route || request.Headers["Origin"] is not null)
            {
                response.StatusCode = 404;
            }
            else if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST");
            }
            else
            {
                await ServeAsync(request, response).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            try
            {
                response.StatusCode = 500;
            }
            catch (InvalidOperationException)
            {
                // The reply already started.
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                // The agent hung up.
            }
        }
    }

    private async Task ServeAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.ContentLength64 > MaxBodyBytes)
        {
            response.StatusCode = 413;
            return;
        }

        JsonElement message;
        try
        {
            using var body = new MemoryStream();
            await request.InputStream.CopyToAsync(body, _stopToken).ConfigureAwait(false);
            if (body.Length > MaxBodyBytes)
            {
                response.StatusCode = 413;
                return;
            }

            using var document = JsonDocument.Parse(body.ToArray());
            message = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            await WriteAsync(response, 400, Failure((JsonElement?)null, -32700, "Parse error")).ConfigureAwait(false);
            return;
        }

        if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String)
        {
            await WriteAsync(response, 400, Failure((JsonElement?)null, -32600, "Invalid Request")).ConfigureAwait(false);
            return;
        }

        // A notification or a response from the client (`notifications/initialized`) is acknowledged and needs no answer.
        if (!message.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            response.StatusCode = 202;
            return;
        }

        var parameters = message.TryGetProperty("params", out var found) ? found : default;
        JsonObject answer;
        try
        {
            answer = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonNode.Parse(id.GetRawText()),
                ["result"] = await ResultAsync(method.GetString()!, parameters, _stopToken).ConfigureAwait(false),
            };
        }
        catch (McpException error)
        {
            answer = Failure((JsonElement?)id, error.Code, error.Message);
        }

        await WriteAsync(response, 200, answer).ConfigureAwait(false);
    }

    private async Task<JsonNode> ResultAsync(string method, JsonElement parameters, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "initialize":
                var asked = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("protocolVersion", out var version) && version.ValueKind == JsonValueKind.String ? version.GetString() : null;
                return new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersions.Contains(asked) ? asked : ProtocolVersions[0],
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = ServerName, ["version"] = "1" },
                };
            case "ping":
                return new JsonObject();
            case "tools/list":
                return new JsonObject
                {
                    ["tools"] = new JsonArray(_tools.Select(tool => (JsonNode)new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["inputSchema"] = JsonNode.Parse(tool.InputSchema.GetRawText()),
                        ["annotations"] = new JsonObject { ["readOnlyHint"] = tool.ReadOnly, ["openWorldHint"] = false },
                    }).ToArray()),
                };
            case "tools/call":
                return await CallAsync(parameters, cancellationToken).ConfigureAwait(false);
            default:
                throw new McpException(-32601, "Method not found");
        }
    }

    private async Task<JsonNode> CallAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("name", out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;
        var tool = _tools.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))
            ?? throw new McpException(-32602, $"Tool {name} not found");
        var arguments = parameters.TryGetProperty("arguments", out var given) && given.ValueKind == JsonValueKind.Object ? given : EmptyArguments;
        ToolResult result;
        try
        {
            result = await tool.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            result = new ToolResult($"{tool.Name} failed: {error.Message.Split('\n')[0]}", IsError: true);
        }

        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result.Text } };
        if (result.Image is { } image)
        {
            content.Add(new JsonObject { ["type"] = "image", ["data"] = image.Data, ["mimeType"] = image.MimeType });
        }

        var answer = new JsonObject { ["content"] = content };
        if (result.IsError)
        {
            answer["isError"] = true;
        }

        return answer;
    }

    private static JsonObject Failure(JsonElement? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id is { } value ? JsonNode.Parse(value.GetRawText()) : null,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private static async Task WriteAsync(HttpListenerResponse response, int status, JsonObject body)
    {
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        response.StatusCode = status;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private sealed class McpException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
