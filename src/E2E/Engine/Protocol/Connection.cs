// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace E2E.Engine.Protocol;

/// <summary>The JSON-RPC error codes the protocol uses, LSP's numbering. Upstream's <c>RPC_ERRORS</c> (<c>engine/protocol/connection.ts</c>).</summary>
public static class RpcErrors
{
    public const int ParseError = -32700;

    public const int InvalidRequest = -32600;

    public const int MethodNotFound = -32601;

    public const int InvalidParams = -32602;

    public const int InternalError = -32603;

    /// <summary>A server error carrying an e2e error in <c>data</c>.</summary>
    public const int EngineError = -32000;

    /// <summary>The connection closed before the request was answered.</summary>
    public const int ConnectionClosed = -32099;
}

/// <summary>The error object of a failed JSON-RPC response. Upstream's <c>RpcErrorObject</c>.</summary>
public sealed record RpcErrorObject(int Code, string Message, JsonNode? Data = null)
{
    /// <summary>
    /// Whether <paramref name="value"/> has the shape of a JSON-RPC error object, and reads it. Upstream's <c>isRpcErrorObject</c>, which
    /// narrows the value where C# returns what it read.
    /// </summary>
    public static bool IsRpcErrorObject(JsonNode? value, out RpcErrorObject error)
    {
        error = null!;
        if (
            value is not JsonObject obj
            || obj["code"] is not JsonValue code
            || !code.TryGetValue<double>(out var number)
            || number != Math.Floor(number)
            || number is < int.MinValue or > int.MaxValue
            || obj["message"] is not JsonValue message
            || !message.TryGetValue<string>(out var text)
        )
        {
            return false;
        }

        error = new RpcErrorObject((int)number, text, obj["data"]?.DeepClone());
        return true;
    }
}

/// <summary>A failed response, thrown from <see cref="Connection.RequestAsync"/>. Upstream's <c>RpcError</c>.</summary>
public sealed class RpcException : Exception
{
    public RpcException(RpcErrorObject error)
        : base(error.Message)
    {
        Code = error.Code;
        Data = error.Data;
    }

    public int Code { get; }

    /// <summary>The error's <c>data</c>. .NET: the name <c>Data</c> would hide <see cref="Exception.Data"/>.</summary>
    public new JsonNode? Data { get; }
}

/// <summary>A byte stream pair one connection speaks over. Upstream's <c>MessageStreams</c>.</summary>
public sealed record MessageStreams(Stream Input, Stream Output);

/// <summary>How a connection handles what arrives on it. Upstream's <c>ConnectionOptions</c>.</summary>
public sealed record ConnectionOptions
{
    /// <summary>Answers one incoming request by method; what a handler throws becomes the error response.</summary>
    public IReadOnlyDictionary<string, Func<JsonNode?, Task<JsonNode?>>>? Requests { get; init; }

    /// <summary>Handles one incoming notification by method.</summary>
    public IReadOnlyDictionary<string, Action<JsonNode?>>? Notifications { get; init; }

    /// <summary>Encodes a handler failure; defaults to an internal error carrying the message.</summary>
    public Func<Exception, RpcErrorObject>? EncodeError { get; init; }

    /// <summary>Called with what a notification handler threw: a notification has no reply to carry it.</summary>
    public Action<string, Exception>? OnNotificationError { get; init; }

    /// <summary>Called once when the input ends or fails, after every pending request was rejected.</summary>
    public Action<string>? OnClose { get; init; }
}

/// <summary>
/// The engine protocol's base layer, the Language Server Protocol's base protocol unchanged: JSON-RPC 2.0 messages, each framed by a
/// <c>Content-Length</c> header, over any byte stream. Either side may send requests and notifications; the harness is the client, the
/// engine the server. Nothing here knows an engine method. Requests are answered concurrently; messages are written in the order they are
/// sent, so a notification sent before a response is handled before it. Upstream's <c>Connection</c>.
/// </summary>
public sealed class Connection : IDisposable
{
    /// <summary>Largest message a peer may send: a tree with pixels fits many times over.</summary>
    private const int MaxMessageBytes = 256 * 1024 * 1024;

    private static readonly byte[] HeaderDelimiter = "\r\n\r\n"u8.ToArray();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly ConcurrentDictionary<string, Func<JsonNode?, Task<JsonNode?>>> _requests;
    private readonly ConcurrentDictionary<string, Action<JsonNode?>> _notifications;
    private readonly Func<Exception, RpcErrorObject> _encodeError;
    private readonly Action<string, Exception>? _onNotificationError;
    private readonly Action<string>? _onClose;
    private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true }
    );
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private long _nextId;
    private string? _closed;

    /// <summary>Reads and writes <paramref name="streams"/> until the input ends, fails, or the connection closes.</summary>
    public Connection(MessageStreams streams, ConnectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(streams);
        options ??= new ConnectionOptions();
        _input = streams.Input;
        _output = streams.Output;
        _requests = new(
            options.Requests ?? new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>(),
            StringComparer.Ordinal
        );
        _notifications = new(
            options.Notifications ?? new Dictionary<string, Action<JsonNode?>>(),
            StringComparer.Ordinal
        );
        _encodeError = options.EncodeError ?? DefaultEncodeError;
        _onNotificationError = options.OnNotificationError;
        _onClose = options.OnClose;
        _ = Task.Run(() => ReadLoopAsync(_stop.Token));
        _ = Task.Run(() => WriteLoopAsync(_stop.Token));
    }

    /// <summary>Why the connection closed, or null while it is open.</summary>
    public string? Closed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>Registers a request handler after construction, for a method only known later.</summary>
    public void OnRequest(string method, Func<JsonNode?, Task<JsonNode?>> handler) => _requests[method] = handler;

    /// <summary>Sends a request and resolves with its result; throws <see cref="RpcException"/>.</summary>
    public Task<JsonNode?> RequestAsync(string method, JsonNode? parameters = null)
    {
        var pending = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        long id;
        lock (_gate)
        {
            if (_closed is not null)
            {
                return Task.FromException<JsonNode?>(
                    new RpcException(new RpcErrorObject(RpcErrors.ConnectionClosed, _closed))
                );
            }

            id = ++_nextId;
            _pending[id] = pending;
        }

        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (parameters is not null)
        {
            message["params"] = parameters.DeepClone();
        }

        Send(message);
        return pending.Task;
    }

    /// <summary>Sends a notification; dropped once the connection is closed.</summary>
    public void Notify(string method, JsonNode? parameters = null)
    {
        if (Closed is not null)
        {
            return;
        }

        var message = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        if (parameters is not null)
        {
            message["params"] = parameters.DeepClone();
        }

        Send(message);
    }

    /// <summary>Closes the connection: every pending request rejects with <paramref name="reason"/>.</summary>
    public void Close(string reason)
    {
        lock (_gate)
        {
            if (_closed is not null)
            {
                return;
            }

            _closed = reason;
        }

        var failure = new RpcException(new RpcErrorObject(RpcErrors.ConnectionClosed, reason));
        foreach (var id in _pending.Keys.ToArray())
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.TrySetException(failure);
            }
        }

        _outbox.Writer.TryComplete();
        _onClose?.Invoke(reason);
    }

    /// <summary>Closes the connection and stops its readers. .NET-only: a stream is read by a task that must end.</summary>
    public void Dispose()
    {
        Close("the connection was disposed");
        _stop.Cancel();
        _stop.Dispose();
    }

    private static RpcErrorObject DefaultEncodeError(Exception cause) => new(RpcErrors.InternalError, cause.Message);

    /// <summary>Reads <c>Content-Length</c> off a header block; other headers are ignored, as LSP allows.</summary>
    private static long? ContentLength(string header)
    {
        foreach (var line in header.Split("\r\n"))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (
                separator == -1
                || !line[..separator].Trim().Equals("content-length", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            var text = line[(separator + 1)..].Trim();
            if (text.Length == 0)
            {
                return 0;
            }

            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        return null;
    }

    /// <summary>A request id as a key: numbers and strings stay apart, as in upstream's map.</summary>
    private static object? IdKey(JsonNode? id) =>
        id switch
        {
            JsonValue value when value.TryGetValue<long>(out var number) => number,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<double>(out var number) => number,
            _ => null,
        };

    private void Send(JsonObject message)
    {
        var body = Encoding.UTF8.GetBytes(message.ToJsonString(WriteOptions));
        var header = Encoding.ASCII.GetBytes(
            $"Content-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n"
        );
        var frame = new byte[header.Length + body.Length];
        header.CopyTo(frame, 0);
        body.CopyTo(frame, header.Length);
        _outbox.Writer.TryWrite(frame);
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _outbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await _output.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The connection was disposed.
        }
        catch (Exception cause)
        {
            Close($"the connection failed: {cause.Message}");
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var size = 0;
        long? bodyLength = null;
        try
        {
            while (Closed is null)
            {
                if (size == buffer.Length)
                {
                    Array.Resize(ref buffer, Math.Min(buffer.Length * 2, int.MaxValue));
                }

                var read = await _input.ReadAsync(buffer.AsMemory(size), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    Close("the peer closed the connection");
                    return;
                }

                size += read;
                // Frames are read as far as the buffered bytes go; the rest waits for the next chunk.
                while (Closed is null)
                {
                    var start = 0;
                    if (bodyLength is null)
                    {
                        var headerEnd = buffer.AsSpan(0, size).IndexOf(HeaderDelimiter);
                        if (headerEnd == -1)
                        {
                            break;
                        }

                        var length = ContentLength(Encoding.ASCII.GetString(buffer, 0, headerEnd));
                        if (length is null)
                        {
                            Close("the peer sent a message without a valid Content-Length header");
                            return;
                        }

                        if (length > MaxMessageBytes)
                        {
                            Close(
                                $"the peer sent a {length.Value.ToString(CultureInfo.InvariantCulture)}-byte message, over the {MaxMessageBytes.ToString(CultureInfo.InvariantCulture)}-byte limit"
                            );
                            return;
                        }

                        bodyLength = length;
                        start = headerEnd + HeaderDelimiter.Length;
                        Buffer.BlockCopy(buffer, start, buffer, 0, size - start);
                        size -= start;
                    }

                    var needed = (int)bodyLength.Value;
                    if (size < needed)
                    {
                        if (buffer.Length < needed)
                        {
                            Array.Resize(ref buffer, needed);
                        }

                        break;
                    }

                    var body = Encoding.UTF8.GetString(buffer, 0, needed);
                    Buffer.BlockCopy(buffer, needed, buffer, 0, size - needed);
                    size -= needed;
                    bodyLength = null;
                    JsonNode? message;
                    try
                    {
                        message = JsonNode.Parse(body);
                    }
                    catch (JsonException)
                    {
                        Send(
                            new JsonObject
                            {
                                ["jsonrpc"] = "2.0",
                                ["id"] = null,
                                ["error"] = new JsonObject
                                {
                                    ["code"] = RpcErrors.ParseError,
                                    ["message"] = "message is not JSON",
                                },
                            }
                        );
                        continue;
                    }

                    Dispatch(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection was disposed.
        }
        catch (Exception cause)
        {
            Close($"the connection failed: {cause.Message}");
        }
    }

    private void Dispatch(JsonNode? message)
    {
        if (message is not JsonObject obj)
        {
            return;
        }

        var idNode = obj["id"];
        var key = IdKey(idNode);
        var hasId = idNode is not null;
        var methodNode = obj["method"];
        var hasMethod = obj.ContainsKey("method");
        if (hasMethod && !(methodNode is JsonValue value && value.TryGetValue<string>(out _)))
        {
            if (hasId)
            {
                Respond(idNode!, error: new RpcErrorObject(RpcErrors.InvalidRequest, "method must be a string"));
            }

            return;
        }

        if (hasMethod && methodNode!.GetValue<string>() is { } method)
        {
            var parameters = obj["params"];
            if (!hasId)
            {
                // A malformed notification must not take the process down with it.
                try
                {
                    if (_notifications.TryGetValue(method, out var handler))
                    {
                        handler(parameters);
                    }
                }
                catch (Exception cause)
                {
                    _onNotificationError?.Invoke(method, cause);
                }

                return;
            }

            _ = AnswerAsync(idNode!, method, parameters);
            return;
        }

        // A response carries exactly one of `result` and `error`; anything else answers nothing and must not settle a request.
        var hasResult = obj.ContainsKey("result");
        var hasError = obj.ContainsKey("error");
        if (!hasId || key is null || hasResult == hasError)
        {
            return;
        }

        if (!_pending.TryRemove(key, out var pending))
        {
            return;
        }

        if (!hasError)
        {
            pending.TrySetResult(obj["result"]?.DeepClone());
        }
        else if (RpcErrorObject.IsRpcErrorObject(obj["error"], out var error))
        {
            pending.TrySetException(new RpcException(error));
        }
        else
        {
            var shown = obj["error"] is null ? "null" : obj["error"]!.GetValueKind().ToString().ToLowerInvariant();
            pending.TrySetException(
                new RpcException(
                    new RpcErrorObject(
                        RpcErrors.InternalError,
                        $"the peer answered with a malformed error ({shown}), not {{ code, message }}"
                    )
                )
            );
        }
    }

    private async Task AnswerAsync(JsonNode id, string method, JsonNode? parameters)
    {
        if (!_requests.TryGetValue(method, out var handler))
        {
            Respond(id, error: new RpcErrorObject(RpcErrors.MethodNotFound, $"unknown method \"{method}\""));
            return;
        }

        try
        {
            var result = await handler(parameters?.DeepClone()).ConfigureAwait(false);
            Respond(id, result: result);
        }
        catch (Exception cause)
        {
            Respond(id, error: _encodeError(cause));
        }
    }

    private void Respond(JsonNode id, JsonNode? result = null, RpcErrorObject? error = null)
    {
        if (Closed is not null)
        {
            return;
        }

        var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone() };
        if (error is null)
        {
            message["result"] = result?.DeepClone();
        }
        else
        {
            var wire = new JsonObject { ["code"] = error.Code, ["message"] = error.Message };
            if (error.Data is not null)
            {
                wire["data"] = error.Data.DeepClone();
            }

            message["error"] = wire;
        }

        Send(message);
    }
}
