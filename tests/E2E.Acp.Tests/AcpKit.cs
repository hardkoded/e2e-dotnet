// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Acp.Tests;

/// <summary>The scripted agent of <c>Fixtures/scripted-agent.mjs</c> and the log it writes. The C# shape of upstream's <c>scripted()</c>.</summary>
internal sealed class ScriptedAgent : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-acp-test-").FullName;

    public ScriptedAgent(object turns, AcpOptions? extra = null, IReadOnlyDictionary<string, string>? env = null)
    {
        LogFile = Path.Combine(_directory, "agent.jsonl");
        var variables = new Dictionary<string, string>
        {
            ["ACP_SCRIPT"] = JsonSerializer.Serialize(turns),
            ["ACP_LOG"] = LogFile,
        };
        foreach (var (name, value) in extra?.Env ?? new Dictionary<string, string>())
        {
            variables[name] = value;
        }

        foreach (var (name, value) in env ?? new Dictionary<string, string>())
        {
            variables[name] = value;
        }

        Executor = AcpExecutors.Acp(new AcpOptions
        {
            Command = "node",
            Args = extra?.Args ?? [AcpKit.Agent],
            Env = variables,
            Model = extra?.Model,
            Mode = extra?.Mode,
            System = extra?.System,
            Name = extra?.Name,
        });
    }

    public IStepExecutor Executor { get; }

    public string LogFile { get; }

    /// <summary>Every line the agent logged, parsed.</summary>
    public List<JsonObject> Log()
    {
        try
        {
            return [.. File.ReadAllLines(LogFile).Where(line => line.Length > 0).Select(line => (JsonObject)JsonNode.Parse(line)!)];
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            return [];
        }
    }

    public List<string> Prompts() => [.. Log().Where(entry => entry.ContainsKey("prompt")).Select(entry => entry["prompt"]!.GetValue<string>())];

    public List<JsonObject> Sessions() => [.. Log().Where(entry => entry.ContainsKey("session"))];

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // The agent may still hold its log open.
        }
    }
}

internal static class AcpKit
{
    /// <summary>The scripted agent, beside the tests.</summary>
    public static string Agent { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "scripted-agent.mjs");

    public static readonly object Pass = new { call = "done", args = new { status = "passed", summary = "added" } };

    /// <summary>The page the steps drive: a counter, a name box, and an Email box.</summary>
    public static DocumentWorld World(Action<string>? onFill = null, Action? onTap = null) =>
        new DocumentWorld().Map("/", page =>
        {
            var count = 0;
            var counter = page.Status("Counter");
            counter.Text = "0";
            page.Button("Increment", () =>
            {
                counter.Text = (++count).ToString(CultureInfo.InvariantCulture);
                onTap?.Invoke();
            });
            page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Title", OnFill = value => onFill?.Invoke(value) });
        });

    public static async Task<E2ESession> StartAsync(DocumentWorld world, IStepExecutor executor, TimeSpan? stepTimeout = null) =>
        await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world),
            AgentExecutor = executor,
            BaseUrl = "https://todos.test",
            TestTitle = "acp > case",
            CacheEnabled = false,
            ActionTimeout = TimeSpan.FromSeconds(2),
            AssertionTimeout = TimeSpan.FromSeconds(2),
            TestTimeout = TimeSpan.FromSeconds(60),
            StepTimeout = stepTimeout ?? TimeSpan.FromSeconds(30),
        });

    /// <summary>Waits on the scripted agent: a child process, which a busy machine can start or answer seconds late.</summary>
    public static async Task EventuallyAsync(Func<bool> check, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!check())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("timed out waiting for " + what);
            }

            await Task.Delay(25);
        }
    }

    public static bool IsRunning(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
