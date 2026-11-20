// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Acp.Tests.Executor;

/// <summary>A scripted agent and the log it writes. The C# shape of upstream's <c>scripted()</c> helper (<c>executor.test.ts</c>).</summary>
internal sealed class ScriptedAgent(IStepExecutor executor, string logFile)
{
    public IStepExecutor Executor { get; } = executor;

    /// <summary>Every line the agent logged so far: its pid, its prompts, the tool results it saw, the permissions it asked.</summary>
    public IReadOnlyList<JsonObject> Log()
    {
        try
        {
            using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return
            [
                .. reader
                    .ReadToEnd()
                    // The agent appends while this reads: a last line without its newline is half written, so leave it for the next read.
                    .Split('\n')
                    .SkipLast(1)
                    .Where(line => line.Length > 0)
                    .Select(line => JsonNode.Parse(line)!.AsObject()),
            ];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public IReadOnlyList<string> Prompts() =>
        [.. Log().Where(entry => entry.ContainsKey("prompt")).Select(entry => entry["prompt"]!.GetValue<string>())];

    public IReadOnlyList<JsonObject> Sessions() => [.. Log().Where(entry => entry.ContainsKey("session"))];

    public IReadOnlyList<int> Pids() =>
        [.. Log().Where(entry => entry.ContainsKey("pid")).Select(entry => entry["pid"]!.GetValue<int>())];

    /// <summary>The first tool result the agent logged for a call of <paramref name="tool"/>, as the text the agent read.</summary>
    public string? ResultOf(string tool) =>
        Log().FirstOrDefault(entry => entry["call"]?.GetValue<string>() == tool)?["result"]?.ToJsonString();
}

/// <summary>A page for the steps to act on: a field, a button that counts, and a button that stalls.</summary>
internal sealed class TodoPage
{
    private int _adds;

    public DocumentElement? Title { get; private set; }

    public int Adds => Volatile.Read(ref _adds);

    public DocumentWorld World() => new DocumentWorld().Map("/", page =>
        {
            Title = page.Textbox("Title");
            page.Button("Add", () => Interlocked.Increment(ref _adds));
            page.Button("Slow", () => Thread.Sleep(300));
        });
}

/// <summary>A local page for the web engine to open. Upstream's step context mocks the observation; the port opens a real page.</summary>
internal sealed class PixelSite : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();

    private PixelSite(string url) => Url = url;

    public string Url { get; }

    public static PixelSite Start(string html)
    {
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var site = new PixelSite(
                "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"
            );
            site._listener.Prefixes.Add(site.Url);
            try
            {
                site._listener.Start();
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                site._listener.Close();
                continue;
            }

            _ = site.ServeAsync(Encoding.UTF8.GetBytes(html));
            return site;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        _stop.Dispose();
    }

    private async Task ServeAsync(byte[] page)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = page.Length;
                await context.Response.OutputStream.WriteAsync(page);
                context.Response.Close();
            }
            catch (Exception) when (_stop.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
                // A client that hung up does not stop the site.
            }
        }
    }
}

internal static class AcpHarness
{
    /// <summary>The program that stands in for an agent. Its apphost lands beside the tests.</summary>
    public static string AgentExecutable { get; } =
        Path.Combine(AppContext.BaseDirectory, "E2E.Acp.Tests.Agent" + (OperatingSystem.IsWindows() ? ".exe" : ""));

    /// <summary>The wait for something a child process does: it can start or answer seconds late on a busy machine.</summary>
    public static async Task EventuallyAsync(Action check)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            try
            {
                check();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }
        }
    }

    public static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static ScriptedAgent Scripted(
        string directory,
        object turns,
        AcpOptions? options = null,
        IReadOnlyDictionary<string, string>? env = null,
        string? command = null,
        IReadOnlyList<string>? args = null
    )
    {
        var logFile = Path.Combine(directory, "agent-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var variables = new Dictionary<string, string>
        {
            ["ACP_SCRIPT"] = JsonSerializer.Serialize(turns),
            ["ACP_LOG"] = logFile,
        };
        foreach (var pair in env ?? new Dictionary<string, string>())
        {
            variables[pair.Key] = pair.Value;
        }

        var executor = AcpExecutor.Create(
            command ?? AgentExecutable,
            args ?? [],
            (options ?? new AcpOptions()) with
            {
                Env = variables,
            }
        );
        return new ScriptedAgent(executor, logFile);
    }

    /// <summary>A session over the in-memory document engine, with the executor as the default agent's brain and the page open.</summary>
    public static async Task<E2ESession> StartAsync(
        IStepExecutor executor,
        DocumentWorld world,
        bool debug = false,
        string? projectRoot = null,
        IReadOnlyDictionary<string, AgentOptions>? agents = null,
        IEngine? engine = null,
        string baseUrl = "https://todos.test"
    )
    {
        var session = await E2ESession.StartAsync(
            new E2ESessionOptions
            {
                Engine = engine ?? new DocumentEngine(world),
                AgentExecutor = executor,
                Agents = agents,
                BaseUrl = baseUrl,
                TestTitle = "acp",
                CacheEnabled = false,
                StepTimeout = TimeSpan.FromSeconds(60),
                TestTimeout = TimeSpan.FromSeconds(120),
                ProjectRoot = projectRoot ?? Path.GetTempPath(),
                Debug = debug,
            }
        );
        await session.App.OpenAsync("/");
        return session;
    }
}
