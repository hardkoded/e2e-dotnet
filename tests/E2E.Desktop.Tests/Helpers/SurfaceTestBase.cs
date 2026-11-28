// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests;

/// <summary>What <c>Initialized</c> and <c>Launched</c> of <see cref="SurfaceTestBase"/> take. Upstream's <c>Launch</c> interface in <c>surface.test.ts</c>.</summary>
internal sealed record LaunchOptions
{
    /// <summary>The run's own <c>--headed</c>, as <c>init</c> receives it.</summary>
    public bool RunHeaded { get; init; }

    public int OpenTimeoutMs { get; init; } = 10_000;

    public bool Observe { get; init; } = true;

    public DesktopOptions? Options { get; init; }

    public IReadOnlyDictionary<string, LaunchArgumentValue>? LaunchArgumentValues { get; init; }

    public EngineAppInfo? App { get; init; }

    public string? ProjectRoot { get; init; }
}

/// <summary>
/// What the <c>DesktopSurface: ...</c> classes share: the helpers at the top of upstream's <c>surface.test.ts</c> and its <c>afterEach</c>. Disposing
/// removes each surface's private state directory, kills the processes the fakes started, and removes the artifact directories.
/// </summary>
public abstract class SurfaceTestBase : IAsyncLifetime
{
    private readonly List<DesktopSurface> _surfaces = [];

    /// <summary>The processes this test's fakes and stand-ins started.</summary>
    internal ProcessRegistry Spawns { get; } = new();

    /// <summary>A long-lived stand-in process, killed after the test. Upstream's <c>standIn</c>.</summary>
    internal int StandIn(bool exits = false) => Spawns.StandIn(exits);

    /// <summary>A fake driver on a scenario. Upstream's <c>fakeDriver</c>.</summary>
    internal FakeDriver NewFake(Scenario? scenario = null) => new(Spawns, scenario);

    /// <summary>The artifact directories attempts were given, removed after each test.</summary>
    private readonly List<string> _artifactDirs = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Disposing removes each surface's private state directory.
        foreach (var surface in _surfaces)
        {
            try
            {
                await surface.DisposeAsync(CancellationToken.None, 2_000);
            }
            catch (Exception)
            {
                // As upstream's `.catch(() => undefined)`: a surface that cannot quit is not what the test is about.
            }
        }

        _surfaces.Clear();
        Spawns.KillAll();
        foreach (var directory in _artifactDirs)
        {
            Directory.Delete(directory, recursive: true);
        }

        _artifactDirs.Clear();
    }

    internal string Artifacts()
    {
        var directory = Directory.CreateTempSubdirectory("artifacts-").FullName;
        _artifactDirs.Add(directory);
        return directory;
    }

    /// <summary>Whether a process is gone within <paramref name="ms"/>. Upstream's <c>gone</c>.</summary>
    internal static async Task<bool> Gone(int pid, int ms = 3_000)
    {
        var until = LaunchOwnership.Now() + ms;
        for (; ; )
        {
            if (!LaunchOwnership.Alive(pid))
            {
                return true;
            }

            if (LaunchOwnership.Now() >= until)
            {
                return false;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>The operation of a call. Upstream's <c>operation()</c>, whose <c>{ ...operation(), timeoutMs, signal }</c> overrides are the parameters.</summary>
    internal static DesktopOperation Operation(
        OperationOrigin origin = OperationOrigin.Test,
        int timeoutMs = 10_000,
        CancellationToken signal = default
    ) => new(origin, timeoutMs, signal);

    internal static Answer Ok(object? data) => Answer.Of(data);

    internal static Answer Failure(
        string code,
        string delivery,
        string? message = null,
        Dictionary<string, object?>? details = null
    ) => FakeDriver.Failure(code, delivery, message, details);

    internal static Scripted SpawnApp(
        bool token = true,
        bool exits = false,
        int? sleepMs = null,
        Answer? answer = null
    ) => FakeDriver.SpawnApp(token, exits, sleepMs, answer);

    internal static Answer SnapshotAnswer(string name) => FakeDriver.SnapshotAnswer(name);

    /// <summary>A window list answer of the newest launched process. Upstream's <c>windows</c>.</summary>
    internal static Scripted Windows(Func<int, object[]> list, int? sleepMs = null) =>
        Scripted.From(
            (fake, _) =>
                new Answer
                {
                    Ok = true,
                    Data = list(fake.Pid).Select(window => WithPid(window, fake.Pid)).ToArray(),
                    SleepMs = sleepMs,
                }
        );

    /// <summary>A window with the process id it is the window of unless it names one: upstream's <c>{ pid: fake.pid, ...window }</c>.</summary>
    private static JsonObject WithPid(object window, int pid)
    {
        var node = JsonSerializer.SerializeToNode(window)!.AsObject();
        if (!node.ContainsKey("pid"))
        {
            node["pid"] = pid;
        }

        return node;
    }

    /// <summary>A snapshot fixture's answer with its refs and window renamed, as a later snapshot of the same window reports it. Upstream's <c>renamed</c>.</summary>
    internal static Answer Renamed(string name, string from, string to) => Replaced(SnapshotAnswer(name), from, to);

    /// <summary>A snapshot answer of one window with the nodes given. Upstream's <c>screen</c>.</summary>
    internal static Answer Screen(string windowId, object tree, object? extra = null)
    {
        var root = new JsonObject { ["role"] = "window" };
        foreach (var (key, value) in JsonSerializer.SerializeToNode(tree)!.AsObject())
        {
            root[key] = value?.DeepClone();
        }

        var data = new JsonObject
        {
            ["snapshot_id"] = "s",
            ["window"] = new JsonObject { ["id"] = windowId },
            ["tree"] = root,
        };
        if (extra is not null)
        {
            foreach (var (key, value) in JsonSerializer.SerializeToNode(extra)!.AsObject())
            {
                data[key] = value?.DeepClone();
            }
        }

        return Answer.Of(data);
    }

    /// <summary>The size of the window a snapshot answer reads, from its tree's bounds. Upstream reads it as <c>form.data.tree.bounds</c>.</summary>
    internal static (int Width, int Height) TreeSize(Answer answer)
    {
        var bounds = ((JsonNode)answer.Data!)["tree"]!["bounds"]!;
        return ((int)bounds["width"]!, (int)bounds["height"]!);
    }

    /// <summary>A capture answer: writes an opaque PNG of the window's size, at scale 1. Upstream's <c>capture</c> of <c>describe('DesktopSurface: pixels')</c>.</summary>
    internal static Answer Capture(int width, int height) =>
        Answer.Of(new { scale_factor = 1 }) with
        {
            WritePng = (width, height),
        };

    /// <summary>An answer whose snapshot tree has been changed by <paramref name="change"/>: upstream's <c>{ ...answer, data: { ...answer.data, tree: { ...answer.data.tree, ... } } }</c>.</summary>
    internal static Answer WithTree(Answer answer, Action<JsonObject> change)
    {
        var data = ((JsonNode)answer.Data!).DeepClone().AsObject();
        change(data["tree"]!.AsObject());
        return answer with { Data = data };
    }

    /// <summary>A locator by test id. Upstream's <c>testId</c>.</summary>
    internal static LocatorExpression TestId(string value) =>
        LocatorExpression.For("testid", value, new TextMatchOptions(), "value");

    /// <summary>A surface on the scripted driver, initialized but with nothing launched. Upstream's <c>initialized</c>.</summary>
    internal async Task<(DesktopSurface Surface, FakeDriver Fake)> Initialized(
        Scenario? scenario = null,
        LaunchOptions? launch = null
    ) => await Initialized(NewFake(scenario), launch);

    internal async Task<(DesktopSurface Surface, FakeDriver Fake)> Initialized(
        FakeDriver fake,
        LaunchOptions? launch = null
    )
    {
        launch ??= new LaunchOptions();
        var surface = new DesktopSurface(
            DesktopOptionsResolver.Resolve(
                (launch.Options ?? new DesktopOptions()) with
                {
                    Settle = launch.Options?.SettleMilliseconds is not null,
                }
            ),
            "macos",
            () => fake
        );
        _surfaces.Add(surface);
        await surface.InitAsync(
            new DesktopInitInfo
            {
                ProjectRoot = launch.ProjectRoot ?? Directory.GetCurrentDirectory(),
                App = launch.App
                    ?? new EngineAppInfo
                    {
                        BundleId = "dev.e2e.spike.form",
                        LaunchArgumentValues = launch.LaunchArgumentValues,
                    },
                // A secret in the run's environment, which the app the engine launches may not see.
                Env = new Dictionary<string, string?>
                {
                    ["HOME"] = "/Users/ada",
                    ["PATH"] = "/usr/bin:/bin",
                    ["LANG"] = "en_US.UTF-8",
                    ["LC_ALL"] = "C",
                    ["E2E_PROBE_SECRET"] = "hunter2",
                },
                Headed = launch.RunHeaded,
            }
        );
        return (surface, fake);
    }

    /// <summary>A surface on the scripted driver, with the form launched (a real child process as the app) and observed once. Upstream's <c>launched</c>.</summary>
    internal async Task<(DesktopSurface Surface, FakeDriver Fake, Func<string, string> ByTestId)> Launched(
        Scenario? scenario = null,
        LaunchOptions? launch = null
    ) => await Launched(NewFake(scenario), launch);

    internal async Task<(DesktopSurface Surface, FakeDriver Fake, Func<string, string> ByTestId)> Launched(
        FakeDriver fakeDriver,
        LaunchOptions? launch = null
    )
    {
        launch ??= new LaunchOptions();
        var (surface, fake) = await Initialized(fakeDriver, launch);
        await surface.RestartAsync(Operation(timeoutMs: launch.OpenTimeoutMs));
        List<SemanticNode> nodes = [];
        if (launch.Observe)
        {
            var observed = await surface.ObserveAsync(Operation(), null);
            nodes = [observed.Roots[0], .. observed.Roots[0].Children];
        }

        string ByTestId(string value) =>
            nodes.FirstOrDefault(candidate => candidate.TestId == value)?.Ref
            ?? throw new InvalidOperationException($"no node {value}");
        return (surface, fake, ByTestId);
    }

    /// <summary>The arguments of the newest call of a method. Upstream's <c>last</c>.</summary>
    internal static T? Last<T>(FakeDriver fake, string method)
        where T : class => fake.Of(method).LastOrDefault()?.Args[0] as T;

    /// <summary>The input calls the surface made: actions, keys, and pointer input. Upstream's <c>inputs</c>.</summary>
    internal static List<string> Inputs(FakeDriver fake) =>
        [
            .. fake.Calls.SelectMany(call =>
                call.Method switch
                {
                    "act" => [((ActRequest)call.Args[0]!).Action],
                    "press" => [$"press {((PressRequest)call.Args[0]!).Combo}"],
                    "pointer" or "focusWindow" => new[] { call.Method },
                    _ => Array.Empty<string>(),
                }
            ),
        ];

    internal void StartAttempt(DesktopSurface surface) =>
        surface.StartAttempt(new DesktopAttemptContext { ArtifactsDir = Artifacts() });

    /// <summary>A state directory a worker left behind, owned by the process <paramref name="owner"/> names. Upstream's <c>leftDirectory</c>.</summary>
    internal static string LeftDirectory(string owner, object? record = null, string prefix = "e2e-desktop-worker-")
    {
        var directory = Directory.CreateTempSubdirectory(prefix).FullName;
        File.WriteAllText(Path.Combine(directory, "owner"), owner);
        if (record is not null)
        {
            File.WriteAllText(
                Path.Combine(directory, "app.json"),
                record as string ?? JsonSerializer.Serialize(record)
            );
        }

        return directory;
    }

    /// <summary>A process id no process holds: one that just exited. Upstream's <c>deadPid</c>.</summary>
    internal async Task<int> DeadPid()
    {
        var pid = StandIn(exits: true);
        await Gone(pid);
        return pid;
    }

    /// <summary>Awaits a rejection with the code and message pattern upstream's <c>rejects.toMatchObject</c> / <c>rejects.toThrow</c> name.</summary>
    internal static async Task<Exception> Rejects(
        Task task,
        string? code = null,
        string? message = null,
        bool? retryable = null
    )
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => task);
        if (code is not null)
        {
            Assert.Equal(code, Assert.IsAssignableFrom<E2EException>(thrown).Code);
        }

        if (retryable is { } expected)
        {
            Assert.Equal(expected, Assert.IsType<EngineException>(thrown).Retryable);
        }

        if (message is not null)
        {
            Assert.Matches(new Regex(message, RegexOptions.Singleline, TimeSpan.FromSeconds(5)), thrown.Message);
        }

        return thrown;
    }

    internal static async Task<Exception> Rejects<T>(
        Task<T> task,
        string? code = null,
        string? message = null,
        bool? retryable = null
    ) => await Rejects((Task)task, code, message, retryable);

    /// <summary>An answer with its data's top-level fields set to those of <paramref name="extra"/>: upstream's <c>{ ...answer, data: { ...answer.data, ...extra } }</c>.</summary>
    internal static Answer Merged(Answer answer, object extra)
    {
        var data = ((JsonNode)answer.Data!).DeepClone().AsObject();
        foreach (var (key, value) in JsonSerializer.SerializeToNode(extra)!.AsObject())
        {
            data[key] = value?.DeepClone();
        }

        return answer with
        {
            Data = data,
        };
    }

    /// <summary>An answer with text replaced in its JSON. Upstream's <c>JSON.parse(JSON.stringify(answer).replaceAll(...))</c>; <paramref name="all"/> false is <c>replace</c>, which takes the first.</summary>
    internal static Answer Replaced(Answer answer, string from, string to, bool all = true)
    {
        var text = ((JsonNode)answer.Data!).ToJsonString();
        var at = text.IndexOf(from, StringComparison.Ordinal);
        return Answer.Of(
            JsonNode.Parse(
                all ? text.Replace(from, to, StringComparison.Ordinal)
                : at < 0 ? text
                : string.Concat(text.AsSpan(0, at), to, text.AsSpan(at + from.Length))
            )
        );
    }
}
