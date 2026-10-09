// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace E2E.Cli.Tests.Helpers;

/// <summary>The built <c>e2e mcp</c> process on raw stdio pipes, for tests about what the server does when its client is slow or gone.</summary>
internal sealed class RawServer : IDisposable
{
    private readonly StringBuilder _stderr = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _responses = new();
    private readonly Process _process;

    private RawServer(Process process)
    {
        _process = process;
    }

    /// <summary>Everything the server wrote to stderr so far.</summary>
    public string Stderr
    {
        get
        {
            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    /// <summary>Starts the server in <paramref name="workingDirectory"/>. With <paramref name="readStdout"/> false, nothing reads its stdout until <see cref="ReadAllAsync"/>.</summary>
    public static RawServer Start(string workingDirectory, bool readStdout = true)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "E2E.Cli.dll"));
        info.ArgumentList.Add("mcp");
        info.Environment["CI"] = "";
        var process = Process.Start(info)!;
        var server = new RawServer(process);
        process.ErrorDataReceived += (_, data) =>
        {
            if (data.Data is not null)
            {
                lock (server._stderr)
                {
                    server._stderr.AppendLine(data.Data);
                }
            }
        };
        process.BeginErrorReadLine();
        if (readStdout)
        {
            _ = Task.Run(() => server.ReadResponsesAsync());
        }

        return server;
    }

    /// <summary>Sends one JSON-RPC message.</summary>
    public async Task SendAsync(object message)
    {
        await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>Performs the handshake: initialize, then the initialized notification.</summary>
    public async Task InitializeAsync()
    {
        await SendAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "raw", version = "0.0.0" } },
        });
        await ResponseAsync(1);
        await SendAsync(new { jsonrpc = "2.0", method = "notifications/initialized" });
    }

    /// <summary>The response to request <paramref name="id"/>, or a failure when the server exits first.</summary>
    public Task<JsonElement> ResponseAsync(int id) => Pending(id).Task.WaitAsync(TimeSpan.FromSeconds(60));

    /// <summary>The text of a tool's result in a response.</summary>
    public static string ResultText(JsonElement response) =>
        string.Join('\n', response.GetProperty("result").GetProperty("content").EnumerateArray().Select(part => part.GetProperty("text").GetString()));

    /// <summary>Closes the client's end of stdin, as a client that goes away does.</summary>
    public void CloseStdin() => _process.StandardInput.Close();

    /// <summary>Closes stdin and abandons stdout and stderr, as a killed client takes every pipe with it.</summary>
    public void Abandon()
    {
        _process.StandardInput.Close();
        _process.StandardOutput.Close();
    }

    /// <summary>The exit code, or null when the server outlives <paramref name="limit"/>.</summary>
    public async Task<int?> ExitWithinAsync(TimeSpan limit)
    {
        using var cancel = new CancellationTokenSource(limit);
        try
        {
            await _process.WaitForExitAsync(cancel.Token);
            return _process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Reads stdout lines until <paramref name="count"/> responses with a result arrived, or the limit passes; returns how many did.</summary>
    public async Task<int> ReadAllAsync(int count, TimeSpan limit)
    {
        var answered = 0;
        using var cancel = new CancellationTokenSource(limit);
        try
        {
            while (answered < count && await _process.StandardOutput.ReadLineAsync(cancel.Token) is { } line)
            {
                if (line.Contains("\"result\"", StringComparison.Ordinal) && line.Contains("\"contents\"", StringComparison.Ordinal))
                {
                    answered++;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        return answered;
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }

        _process.Dispose();
    }

    private TaskCompletionSource<JsonElement> Pending(int id) =>
        _responses.GetOrAdd(id, _ => new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously));

    private async Task ReadResponsesAsync()
    {
        try
        {
            while (await _process.StandardOutput.ReadLineAsync() is { } line)
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                {
                    Pending(id.GetInt32()).TrySetResult(document.RootElement.Clone());
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or InvalidOperationException)
        {
            // The client end closed under the read: the test abandoned the pipe on purpose.
        }

        foreach (var pending in _responses.Values)
        {
            pending.TrySetException(new InvalidOperationException("the server exited; stderr:\n" + Stderr));
        }
    }
}
