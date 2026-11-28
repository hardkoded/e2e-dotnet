// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Desktop;
using E2E.Internal;

namespace E2E.Desktop.Tests;

/// <summary>One scripted answer: an envelope, with what the fake does before it answers. Port of upstream's <c>Answer</c> in <c>fake-driver.ts</c>.</summary>
internal sealed record Answer
{
    public bool Ok { get; init; }

    /// <summary>The answer's data: any value the driver's type for the call can be read from as JSON, with the driver's snake_case names.</summary>
    public object? Data { get; init; }

    public DriverError? Error { get; init; }

    /// <summary>How long the call takes before it answers.</summary>
    public int? SleepMs { get; init; }

    /// <summary>For <c>snapshot</c>: the answer to the same call made with <c>interactive: true</c>.</summary>
    public Answer? Interactive { get; init; }

    /// <summary>For <c>screenshot</c>: writes an opaque black PNG of this size to the call's <c>path</c>.</summary>
    public (int Width, int Height)? WritePng { get; init; }

    public static Answer Of(object? data) => new() { Ok = true, Data = data };
}

/// <summary>An answer, or one computed when the call is made from the fake and the call's arguments. Port of upstream's <c>Scripted</c>.</summary>
internal sealed class Scripted
{
    private readonly Func<FakeDriver, object?[], Answer> _answer;

    private Scripted(Func<FakeDriver, object?[], Answer> answer) => _answer = answer;

    public Answer Resolve(FakeDriver fake, object?[] args) => _answer(fake, args);

    public static implicit operator Scripted(Answer answer) => new((_, _) => answer);

    public static implicit operator Scripted(Func<FakeDriver, Answer> answer) => new((fake, _) => answer(fake));

    public static Scripted From(Func<FakeDriver, object?[], Answer> answer) => new(answer);
}

/// <summary>The async methods a scenario scripts, by upstream's names.</summary>
internal sealed class Scenario : Dictionary<string, Scripted[]>;

/// <summary>
/// The processes the fakes of one test started, killed when it ends. .NET-only: upstream's <c>children</c> is a module global, which is safe
/// because vitest runs a file's tests one at a time; xUnit runs the test classes in parallel, so each test owns its processes.
/// </summary>
internal sealed class ProcessRegistry
{
    private readonly List<Process> _children = [];

    /// <summary>A long-lived stand-in process; <paramref name="exits"/> makes one that quits at once. Upstream's <c>standIn</c>.</summary>
    public int StandIn(bool exits = false)
    {
        var info = new ProcessStartInfo(exits ? "true" : "sleep", exits ? "" : "60")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var child = Process.Start(info)!;
        lock (_children)
        {
            _children.Add(child);
        }

        return child.Id;
    }

    /// <summary>Kills every process the test started. Upstream's <c>killSpawned</c>.</summary>
    public void KillAll()
    {
        lock (_children)
        {
            foreach (var child in _children)
            {
                try
                {
                    child.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Gone already.
                }
                finally
                {
                    child.Dispose();
                }
            }

            _children.Clear();
        }
    }
}

/// <summary>One call the fake received.</summary>
internal sealed record FakeCall(string Method, object?[] Args);

/// <summary>
/// A scripted native driver: each async method answers with the next answer its scenario lists (the last one repeats), every call is recorded with
/// its arguments, and a launch can start a real process standing in for the app, so the engine's quit and sweep paths kill something real. Nothing
/// here loads the native driver, so the tests run on any OS. Port of upstream's <c>FakeDriver</c> in <c>tests/helpers/fake-driver.ts</c>.
/// </summary>
internal sealed class FakeDriver : IDesktopDriver
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ProcessRegistry _processes;
    private Scenario _scenario;
    private uint _lastInputId;

    public FakeDriver(ProcessRegistry processes, Scenario? scenario = null)
    {
        _processes = processes;
        _scenario = scenario ?? [];
    }

    public string Version => "0.0.0-test";

    public List<FakeCall> Calls { get; } = [];

    /// <summary>What <c>screenLocked</c> answers.</summary>
    public bool? Locked { get; set; } = false;

    /// <summary>Whether <c>processesCarrying</c> cannot list the processes.</summary>
    public bool LookupFails { get; set; }

    /// <summary>Whether <c>processStarted</c> cannot tell when any process started.</summary>
    public bool StartsUnreadable { get; set; }

    /// <summary>When each process started, by id; a running process not listed here started at a time its id fixes.</summary>
    public Dictionary<int, long> StartTimes { get; } = [];

    /// <summary>The launch token each process carries in its environment, by id.</summary>
    public Dictionary<int, string> Carriers { get; } = [];

    /// <summary>The processes <see cref="Spawn"/> started, oldest first.</summary>
    public List<int> Spawned { get; } = [];

    public bool Accessibility { get; set; } = true;

    public bool ScreenRecording { get; set; } = true;

    /// <summary>Answers like the driver's layout check: a combo this fake's keyboard cannot type is listed here.</summary>
    public HashSet<string> Untypable { get; } = [];

    /// <summary>The input id and budget of each <c>focusWindow</c> call, in order.</summary>
    public List<(uint? InputId, int? TimeoutMs)> FocusOptions { get; } = [];

    /// <summary>The input id and budget of each <c>pointer</c> call, in order; its <c>Args</c> keep the point and kind.</summary>
    public List<(uint? InputId, int? TimeoutMs, PointerTarget? Target)> PointerOptions { get; } = [];

    /// <summary>Replaces the scenario, as the next host on the same engine handle sees it.</summary>
    public void Rescript(Scenario scenario) => _scenario = scenario;

    /// <summary>The newest process a launch started.</summary>
    public int Pid => Spawned.Count > 0 ? Spawned[^1] : throw new InvalidOperationException("nothing was launched");

    /// <summary>Starts a process standing in for the app, carrying <paramref name="token"/> unless it is null.</summary>
    public int Spawn(string? token = null, bool exits = false)
    {
        var pid = _processes.StandIn(exits);
        Spawned.Add(pid);
        if (token is not null)
        {
            Carriers[pid] = token;
        }

        return pid;
    }

    /// <summary>The calls of one method.</summary>
    public List<FakeCall> Of(string method) => [.. Calls.Where(call => call.Method == method)];

    /// <summary>The actions <c>act</c> was asked for, in order.</summary>
    public List<string> Acted() => [.. Of("act").Select(call => ((ActRequest)call.Args[0]!).Action)];

    public DriverStatus Status()
    {
        Calls.Add(new FakeCall("status", []));
        return new DriverStatus(Accessibility, ScreenRecording);
    }

    public bool? ScreenLocked()
    {
        Calls.Add(new FakeCall("screenLocked", []));
        return Locked;
    }

    public long? ProcessStarted(int pid)
    {
        if (StartsUnreadable || !LaunchOwnership.Alive(pid))
        {
            return null;
        }

        return StartTimes.TryGetValue(pid, out var started) ? started : 1_000_000 + pid;
    }

    public Task<IReadOnlyList<int>?> ProcessesCarryingAsync(string token)
    {
        Calls.Add(new FakeCall("processesCarrying", [token]));
        if (LookupFails)
        {
            return Task.FromResult<IReadOnlyList<int>?>(null);
        }

        return Task.FromResult<IReadOnlyList<int>?>([
            .. Carriers.Where(pair => pair.Value == token && LaunchOwnership.Alive(pair.Key)).Select(pair => pair.Key),
        ]);
    }

    public void ForgetProcess(int pid) => Calls.Add(new FakeCall("forgetProcess", [pid]));

    public Task<Envelope<Launched>> LaunchAsync(LaunchRequest request) =>
        AnswerAsync<Launched>("launch", [request], SpawnApp());

    public Task<Envelope<IReadOnlyList<RawWindow>>> ListWindowsAsync(int pid) =>
        AnswerAsync<IReadOnlyList<RawWindow>>(
            "listWindows",
            [pid],
            (Func<FakeDriver, Answer>)(
                fake =>
                    Answer.Of(
                        new object[]
                        {
                            new
                            {
                                id = "w-mine",
                                title = "Spike Form",
                                pid = fake.Pid,
                                is_focused = true,
                            },
                        }
                    )
            )
        );

    public Task<Envelope<RawSnapshot>> SnapshotAsync(SnapshotRequest request) =>
        AnswerAsync<RawSnapshot>("snapshot", [request], SnapshotAnswer("macos/appkit-form"));

    public Task<Envelope<Nothing>> ActAsync(ActRequest request) =>
        AnswerAsync<Nothing>("act", [request], Answer.Of(null));

    public uint InputId() => Interlocked.Increment(ref _lastInputId);

    public void CancelInput(uint inputId) => Calls.Add(new FakeCall("cancelInput", [inputId]));

    public Task<Envelope<Nothing>> CheckComboAsync(string combo) =>
        Task.FromResult(
            Untypable.Contains(combo)
                ? Envelope<Nothing>.Failure(
                    new DriverError(
                        "ACTION_NOT_SUPPORTED",
                        $"{combo}: no key on this layout types it",
                        new Disposition("not_delivered"),
                        new Dictionary<string, object?>()
                    )
                )
                : Envelope<Nothing>.Success(Nothing.Value)
        );

    public Task<Envelope<Nothing>> PressAsync(PressRequest request) =>
        AnswerAsync<Nothing>("press", [request], Answer.Of(null));

    public Task<Envelope<Nothing>> FocusWindowAsync(
        int pid,
        string windowId,
        uint? inputId = null,
        int? timeoutMs = null
    )
    {
        FocusOptions.Add((inputId, timeoutMs));
        return AnswerAsync<Nothing>("focusWindow", [pid, windowId], Answer.Of(null));
    }

    public Task<Envelope<Nothing>> PointerAsync(
        double x,
        double y,
        string kind,
        uint? inputId = null,
        int? timeoutMs = null,
        PointerTarget? target = null
    )
    {
        PointerOptions.Add((inputId, timeoutMs, target));
        return AnswerAsync<Nothing>("pointer", [x, y, kind], Answer.Of(null));
    }

    public Task<Envelope<ScreenshotResult>> ScreenshotAsync(ScreenshotRequest request) =>
        AnswerAsync<ScreenshotResult>("screenshot", [request], Answer.Of(new { scale_factor = 1 }));

    private async Task<Envelope<T>> AnswerAsync<T>(string method, object?[] args, Scripted fallback)
    {
        var index = Of(method).Count;
        Calls.Add(new FakeCall(method, args));
        var list = _scenario.TryGetValue(method, out var scripted) ? scripted : [fallback];
        var answer = list[Math.Min(index, list.Length - 1)].Resolve(this, args);
        if (method == "snapshot" && ((SnapshotRequest)args[0]!).Interactive && answer.Interactive is { } interactive)
        {
            answer = interactive;
        }

        if (answer.WritePng is { } size)
        {
            await File.WriteAllBytesAsync(((ScreenshotRequest)args[0]!).Path, BlackPng(size.Width, size.Height));
        }

        if (answer.SleepMs is { } sleep)
        {
            await Task.Delay(sleep);
        }

        return answer.Ok
            ? Envelope<T>.Success(Convert<T>(answer.Data))
            : Envelope<T>.Failure(answer.Error ?? new DriverError("INTERNAL", "failed"));
    }

    /// <summary>The scripted data as the driver's type for the call: a value that already is one, or JSON with the driver's snake_case names.</summary>
    private static T Convert<T>(object? data)
    {
        if (data is T typed)
        {
            return typed;
        }

        if (typeof(T) == typeof(Nothing))
        {
            return (T)(object)Nothing.Value;
        }

        var text = data is JsonNode node ? node.ToJsonString() : JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    /// <summary>
    /// A launch that starts a real process standing in for the app, carrying the launch's token unless <paramref name="token"/> is false, and reports it
    /// as the driver does. <paramref name="answer"/> replaces the success envelope: a failure the driver reports after the process started. Upstream's <c>spawnApp</c>.
    /// </summary>
    public static Scripted SpawnApp(
        bool token = true,
        bool exits = false,
        int? sleepMs = null,
        Answer? answer = null
    ) =>
        Scripted.From(
            (fake, args) =>
            {
                var pid = fake.Spawn(token ? ((LaunchRequest)args[0]!).Token : null, exits);
                var reported = answer
                    ?? Answer.Of(
                        new
                        {
                            app = "SpikeForm",
                            pid,
                            bundle_id = "dev.e2e.spike.form",
                            started = fake.ProcessStarted(pid),
                        }
                    );
                return sleepMs is null ? reported : reported with { SleepMs = sleepMs };
            }
        );

    /// <summary>A snapshot fixture's <c>data</c>, as an answer. Upstream's <c>snapshotAnswer</c>.</summary>
    public static Answer SnapshotAnswer(string name) =>
        Answer.Of(
            JsonNode.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Snapshots", name + ".json"))
            )
        );

    /// <summary>A failed envelope. Upstream's <c>failure</c>.</summary>
    public static Answer Failure(
        string code,
        string delivery,
        string? message = null,
        Dictionary<string, object?>? details = null
    ) =>
        new()
        {
            Ok = false,
            Error = new DriverError(code, message ?? $"{code} from the fake", new Disposition(delivery), details),
        };

    /// <summary>An opaque black 8-bit RGBA PNG.</summary>
    private static byte[] BlackPng(int width, int height)
    {
        var data = new byte[width * height * 4];
        for (var at = 3; at < data.Length; at += 4)
        {
            data[at] = 255;
        }

        return ImageOps.EncodePng(new RgbaImage(width, height, data));
    }
}
