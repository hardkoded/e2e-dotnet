// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Acp;

/// <summary>An error response of the agent: the code and message of a JSON-RPC error, and its <c>data</c>.</summary>
internal sealed class AcpRpcException(int code, string message, JsonNode? data = null) : Exception(message)
{
    public int Code { get; } = code;

    public JsonNode? Details { get; } = data;
}

/// <summary>
/// The JSON-RPC 2.0 connection to one ACP agent process on stdio, one JSON message to a line (the SDK's <c>ClientSideConnection</c>
/// over <c>ndJsonStream</c>, which the port does not have). .NET-only: upstream uses <c>@agentclientprotocol/sdk</c>.
/// </summary>
internal sealed partial class AcpConnection : IDisposable
{
    /// <summary>Variables e2e reads secret and credential values from: the agent never needs them. Any case, since Windows reads names that way.</summary>
    [GeneratedRegex("^E2E_(?:SECRET|USER)_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretVariable();

    private readonly Process _child;
    private readonly string _command;
    private readonly object _write = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly TaskCompletionSource<Exception> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<string, JsonObject?, JsonNode?> _onRequest;
    private readonly Action<string, JsonObject?> _onNotification;
    private readonly StringBuilder _stderr = new();
    private long _nextId;

    private AcpConnection(Process child, string command, Func<string, JsonObject?, JsonNode?> onRequest, Action<string, JsonObject?> onNotification)
    {
        _child = child;
        _command = command;
        _onRequest = onRequest;
        _onNotification = onNotification;
    }

    /// <summary>Completes with the error that describes the agent's exit, whenever it exits.</summary>
    public Task<Exception> Exited => _exited.Task;

    /// <summary>
    /// Starts <paramref name="command"/> in the current directory, so a command installed in the project resolves. The agent gets this
    /// process's environment plus <paramref name="env"/>, without the variables e2e reads secrets from.
    /// </summary>
    public static AcpConnection Start(
        string command,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string>? env,
        Func<string, JsonObject?, JsonNode?> onRequest,
        Action<string, JsonObject?> onNotification)
    {
        var info = new ProcessStartInfo(command)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var name in info.Environment.Keys.Where(name => SecretVariable().IsMatch(name)).ToList())
        {
            info.Environment.Remove(name);
        }

        if (env is not null)
        {
            foreach (var (name, value) in env)
            {
                if (!SecretVariable().IsMatch(name))
                {
                    info.Environment[name] = value;
                }
            }
        }

        var child = new Process { StartInfo = info, EnableRaisingEvents = true };
        var connection = new AcpConnection(child, command, onRequest, onNotification);
        try
        {
            child.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            child.Dispose();
            throw new InvalidOperationException("could not start " + command + ": " + ex.Message, ex);
        }

        _ = Task.Run(connection.ReadErrorsAsync);
        _ = Task.Run(connection.ReadAsync);
        return connection;
    }

    /// <summary>The agent's environment without the variables e2e reads secrets from, wherever they came from.</summary>
    public static IReadOnlyDictionary<string, string> WithoutSecrets(IReadOnlyDictionary<string, string> env) =>
        env.Where(pair => !SecretVariable().IsMatch(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>Sends a request and answers its result. An error response is an <see cref="AcpRpcException"/>.</summary>
    public async Task<JsonNode?> RequestAsync(string method, JsonObject parameters)
    {
        var id = Interlocked.Increment(ref _nextId);
        var reply = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;
        if (_exited.Task.IsCompleted)
        {
            _pending.TryRemove(id, out _);
            throw _exited.Task.Result;
        }

        Send(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters });
        // The exit may have raced the registration.
        if (_exited.Task.IsCompleted && _pending.TryRemove(id, out var lost))
        {
            lost.TrySetException(_exited.Task.Result);
        }

        return await reply.Task.ConfigureAwait(false);
    }

    public void Notify(string method, JsonObject parameters) =>
        Send(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters });

    /// <summary>Ends the agent: closes its input and kills it with its children.</summary>
    public void Terminate()
    {
        // Killed first, outside the write lock: a write blocked on a pipe the agent does not read must not keep the agent alive.
        try
        {
            if (!_child.HasExited)
            {
                _child.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // It exited between the check and the kill.
        }

        try
        {
            _child.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The agent is already gone.
        }
    }

    public void Dispose()
    {
        Terminate();
        _child.Dispose();
    }

    private void Send(JsonObject message)
    {
        var line = message.ToJsonString();
        try
        {
            lock (_write)
            {
                _child.StandardInput.WriteLine(line);
                _child.StandardInput.Flush();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // A dead agent must not take the test run down with a pipe error: the exit settles every request.
        }
    }

    private async Task ReadErrorsAsync()
    {
        try
        {
            var buffer = new char[1024];
            int read;
            while ((read = await _child.StandardError.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                lock (_stderr)
                {
                    _stderr.Append(buffer, 0, read);
                    if (_stderr.Length > 2000)
                    {
                        _stderr.Remove(0, _stderr.Length - 2000);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The process ended.
        }
    }

    private async Task ReadAsync()
    {
        try
        {
            string? line;
            while ((line = await _child.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.Length > 0)
                {
                    HandleSafely(line);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The process ended.
        }

        await Task.Run(() =>
        {
            try
            {
                _child.WaitForExit();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Disposed under the wait.
            }
        }).ConfigureAwait(false);
        var code = SafeExitCode();
        string last;
        lock (_stderr)
        {
            last = LastLine(_stderr.ToString());
        }

        var ended = new InvalidOperationException("the agent exited (code " + code + ")" + (last.Length == 0 ? "" : ": " + last));
        _exited.TrySetResult(ended);
        foreach (var (id, reply) in _pending)
        {
            if (_pending.TryRemove(id, out _))
            {
                reply.TrySetException(ended);
            }
        }
    }

    private string SafeExitCode()
    {
        try
        {
            return _child.ExitCode.ToString(CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException)
        {
            return "unknown";
        }
    }

    // One malformed message from the agent must not end the read loop: a request gets an error reply, anything else is dropped.
    private void HandleSafely(string line)
    {
        try
        {
            Handle(line);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            try
            {
                if (JsonNode.Parse(line) is JsonObject { } message && message["method"] is not null && message["id"] is { } id)
                {
                    Send(new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id.DeepClone(),
                        ["error"] = new JsonObject { ["code"] = -32602, ["message"] = "Invalid params: " + ex.Message },
                    });
                }
            }
            catch (JsonException)
            {
                // Not JSON: nothing to answer.
            }
        }
    }

    private void Handle(string line)
    {
        JsonObject? message;
        try
        {
            message = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        if (message is null)
        {
            return;
        }

        var method = message["method"]?.GetValue<string>();
        var id = message["id"];
        if (method is not null)
        {
            var parameters = message["params"] as JsonObject;
            if (id is null)
            {
                _onNotification(method, parameters);
                return;
            }

            JsonObject reply;
            try
            {
                reply = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = _onRequest(method, parameters) };
            }
            catch (AcpRpcException error)
            {
                reply = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id.DeepClone(),
                    ["error"] = new JsonObject { ["code"] = error.Code, ["message"] = error.Message },
                };
            }

            Send(reply);
            return;
        }

        if (id is not JsonValue idValue || !idValue.TryGetValue<long>(out var key) || !_pending.TryRemove(key, out var pending))
        {
            return;
        }

        if (message["error"] is JsonObject failure)
        {
            pending.TrySetException(new AcpRpcException(
                failure["code"]?.GetValue<int>() ?? -32603,
                failure["message"]?.GetValue<string>() ?? "request failed",
                failure["data"]?.DeepClone()));
        }
        else
        {
            pending.TrySetResult(message["result"]?.DeepClone());
        }
    }

    private static string LastLine(string text)
    {
        var lines = text.Trim().Split('\n');
        return lines[^1].Trim();
    }

    public override string ToString() => _command;
}
