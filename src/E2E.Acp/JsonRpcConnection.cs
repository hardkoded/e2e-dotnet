// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Acp;

/// <summary>
/// A JSON-RPC 2.0 peer over newline-delimited messages, the transport of the Agent Client Protocol on stdio. .NET-only: upstream
/// uses <c>@agentclientprotocol/sdk</c>'s <c>client()</c> and <c>ndJsonStream</c>; the port has no such package, so this carries the
/// few calls the executor needs. It answers a request from the agent with the request handler and hands a notification to
/// the notification handler, both on the reading loop, in the order the agent sent them.
/// </summary>
internal sealed class JsonRpcConnection : IDisposable
{
    /// <summary>JSON-RPC's "method not found".</summary>
    internal const int MethodNotFound = -32601;

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly Stream _output;
    private readonly TextReader _input;
    private readonly Func<string, JsonElement, JsonNode?> _onRequest;
    private readonly Action<string, JsonElement> _onNotification;
    private readonly SemaphoreSlim _writing = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _stopped = new();
    private readonly CancellationToken _stopToken;
    private long _nextId;
    private Exception? _failure;

    public JsonRpcConnection(Stream output, TextReader input, Func<string, JsonElement, JsonNode?> onRequest, Action<string, JsonElement> onNotification)
    {
        _output = output;
        _input = input;
        _onRequest = onRequest;
        _onNotification = onNotification;
        _stopToken = _stopped.Token;
    }

    /// <summary>Starts reading. The loop ends when the agent closes its output, and fails every call still waiting.</summary>
    public void Start() => _ = Task.Run(ReadAsync);

    /// <summary>Fails every call waiting for the agent, and every later one, with <paramref name="error"/>. The first failure stands.</summary>
    public void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref _failure, error, null);
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var waiting))
            {
                waiting.TrySetException(_failure!);
            }
        }
    }

    /// <summary>Sends a request and waits for its answer. <paramref name="cancellationToken"/> stops the wait only; the agent may still answer.</summary>
    public async Task<JsonElement> RequestAsync(string method, JsonNode? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;
        try
        {
            if (_failure is { } failure)
            {
                throw failure;
            }

            await SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters }, cancellationToken).ConfigureAwait(false);
            return await waiting.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>Sends a notification: no answer comes.</summary>
    public Task NotifyAsync(string method, JsonNode? parameters, CancellationToken cancellationToken) =>
        SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters }, cancellationToken);

    public void Dispose()
    {
        if (_stopped.IsCancellationRequested)
        {
            return;
        }

        _stopped.Cancel();
        Fail(new ObjectDisposedException(nameof(JsonRpcConnection), "the connection to the agent is closed"));
    }

    private async Task SendAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var line = Utf8.GetBytes(message.ToJsonString() + "\n");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopToken);
        await _writing.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(line, linked.Token).ConfigureAwait(false);
            await _output.FlushAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _writing.Release();
        }
    }

    private async Task ReadAsync()
    {
        try
        {
            while (await _input.ReadLineAsync(_stopToken).ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0)
                {
                    await HandleAsync(line).ConfigureAwait(false);
                }
            }

            Fail(new IOException("the agent closed its output"));
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private async Task HandleAsync(string line)
    {
        JsonElement message;
        try
        {
            using var document = JsonDocument.Parse(line);
            message = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not a message: some agents print a banner before they speak the protocol.
            return;
        }

        if (message.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var hasId = message.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Null;
        if (message.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String)
        {
            var parameters = message.TryGetProperty("params", out var found) ? found : default;
            if (!hasId)
            {
                _onNotification(method.GetString()!, parameters);
                return;
            }

            await AnswerAsync(id, method.GetString()!, parameters).ConfigureAwait(false);
            return;
        }

        if (hasId && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number) && _pending.TryRemove(number, out var waiting))
        {
            if (message.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                waiting.TrySetException(AcpRequestException.From(error));
            }
            else
            {
                waiting.TrySetResult(message.TryGetProperty("result", out var result) ? result : default);
            }
        }
    }

    private async Task AnswerAsync(JsonElement id, string method, JsonElement parameters)
    {
        JsonObject answer = new() { ["jsonrpc"] = "2.0", ["id"] = JsonNode.Parse(id.GetRawText()) };
        try
        {
            answer["result"] = _onRequest(method, parameters);
        }
        catch (AcpRequestException error)
        {
            answer["error"] = new JsonObject { ["code"] = error.Code, ["message"] = error.Message };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            answer["error"] = new JsonObject { ["code"] = -32603, ["message"] = error.Message };
        }

        try
        {
            await SendAsync(answer, _stopToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Closed while answering.
        }
    }
}

/// <summary>An error the agent answered a request with: JSON-RPC's <c>code</c>, <c>message</c>, and <c>data</c>.</summary>
internal sealed class AcpRequestException(int code, string message, JsonElement data) : Exception(message)
{
    public int Code { get; } = code;

    /// <summary>What the agent said beyond its message; undefined when it sent nothing.</summary>
    public JsonElement ErrorData { get; } = data;

    public static AcpRequestException From(JsonElement error)
    {
        var code = error.TryGetProperty("code", out var number) && number.TryGetInt32(out var value) ? value : 0;
        var message = error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString()! : "request failed";
        return new AcpRequestException(code, message, error.TryGetProperty("data", out var data) ? data : default);
    }
}
