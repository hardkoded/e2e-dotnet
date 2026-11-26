// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Engine.Protocol;
using E2E.Reports;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// The helpers of upstream's <c>engine-protocol.test.ts</c>: its <c>node</c>, <c>ROOT</c>, <c>operation</c>, <c>initInfo</c>, <c>cleanup</c>,
/// <c>toy</c>, and <c>overLoopback</c>.
/// </summary>
internal static class OverLoopback
{
    /// <summary>Upstream's <c>ROOT</c>: a root with a Save and a Cancel button.</summary>
    public static EngineNode Root { get; } =
        new(new NodeRef("root", ""), "root", null, [Node("n1", "Save"), Node("n2", "Cancel")]);

    /// <summary>Upstream's <c>node</c>.</summary>
    public static EngineNode Node(string id, string name, string role = "button") =>
        new(new NodeRef(id, ""), role, name);

    /// <summary>Upstream's <c>operation</c>.</summary>
    public static OperationContext Operation(CancellationToken signal = default) =>
        new(OperationOrigin.Test)
        {
            Signal = signal,
            Timeout = TimeSpan.FromSeconds(1),
            RunId = "run",
            AttemptId = "attempt-1",
        };

    /// <summary>Upstream's <c>initInfo</c>.</summary>
    public static EngineInitInfo InitInfo(
        Action<string>? log = null,
        CancellationToken signal = default,
        int workerSlot = 0
    ) =>
        new()
        {
            RunId = "run",
            TargetName = "toy",
            ProjectRoot = Directory.GetCurrentDirectory(),
            App = new EngineAppInfo(),
            Env = new Dictionary<string, string?>(),
            Headed = false,
            WorkerSlot = workerSlot,
            Signal = signal,
            Log = log ?? (_ => { }),
        };

    /// <summary>Upstream's <c>prepare</c> info of a target.</summary>
    public static EnginePrepareInfo PrepareInfo(
        string targetName,
        Action<string>? log = null,
        CancellationToken signal = default
    ) =>
        new()
        {
            RunId = "run",
            TargetName = targetName,
            ProjectRoot = Directory.GetCurrentDirectory(),
            App = new EngineAppInfo(),
            Slots = 1,
            Env = new Dictionary<string, string?>(),
            Headed = false,
            Signal = signal,
            Log = log ?? (_ => { }),
        };

    /// <summary>Upstream's <c>finish</c> info of a target, with <see cref="Cleanup"/>'s budget.</summary>
    public static EngineFinishInfo FinishInfo(string targetName) =>
        new()
        {
            RunId = "run",
            TargetName = targetName,
            Env = new Dictionary<string, string?>(),
            Log = _ => { },
            CleanupTimeout = TimeSpan.FromSeconds(1),
        };

    /// <summary>Upstream's <c>doctor</c> info.</summary>
    public static EngineDoctorInfo DoctorInfo(CancellationToken signal = default, bool headed = false) =>
        new()
        {
            TargetName = "toy",
            ProjectRoot = Directory.GetCurrentDirectory(),
            App = new EngineAppInfo(),
            Env = new Dictionary<string, string?>(),
            Headed = headed,
            Signal = signal,
            Log = _ => { },
        };

    /// <summary>Upstream's <c>cleanup</c>.</summary>
    public static EngineCleanupContext Cleanup { get; } = new(CancellationToken.None, TimeSpan.FromSeconds(1));

    /// <summary>Upstream's <c>cleanup</c>, as the context of <c>dispose</c>.</summary>
    public static EngineDisposeContext DisposeContext(TimeSpan? timeout = null) =>
        new() { CleanupTimeout = timeout ?? TimeSpan.FromSeconds(1), Signal = CancellationToken.None };

    /// <summary>An attempt whose callbacks do nothing; the caller overrides the ones it reads.</summary>
    public static EngineAttemptContext Attempt(
        string attemptId = "attempt-1",
        string? artifactsDir = null,
        Action<IReadOnlyDictionary<string, string>>? environment = null
    ) =>
        new()
        {
            AttemptId = attemptId,
            ArtifactsDir = artifactsDir ?? Path.GetTempPath(),
            Signal = CancellationToken.None,
            ResolveSecret = (_, _) => Task.FromResult(""),
            AppLog = _ => { },
            Navigation = _ => { },
            Environment = environment ?? (_ => { }),
        };

    /// <summary>Upstream's <c>toy</c>: an engine that observes <see cref="Root"/>, with <paramref name="extra"/> changing it.</summary>
    public static EngineSpec Toy(Func<EngineSpec, EngineSpec>? extra = null)
    {
        var toy = new EngineSpec
        {
            Name = "toy",
            Version = "1.0.0",
            SpiVersion = 1,
            Observe = (_, _) =>
                Task.FromResult(new EngineSnapshot(Root, new ViewportSize { Width = 1280, Height = 720 })),
        };
        return extra is null ? toy : extra(toy);
    }

    /// <summary>Serves <paramref name="engine"/> in this process and returns the client handle over it. Upstream's <c>overLoopback</c>.</summary>
    public static (EngineHandle Local, EngineHandle Remote) Of(
        EngineSpec engine,
        Func<ConnectEngineOptions, ConnectEngineOptions>? extra = null
    )
    {
        var local = E2E.Engine.EngineManifest.DefineEngine(engine);
        var options = new ConnectEngineOptions
        {
            Manifest = ProtocolManifest.DescribeEngine(local),
            Transport = new LoopbackTransport(local),
        };
        return (local, ProtocolClient.ConnectEngine(extra is null ? options : extra(options)));
    }

    /// <summary>The ids of a tree, in order: a decoded tree is equal by value, not by reference.</summary>
    public static IEnumerable<string> TreeIds(EngineNode node) =>
        [node.Ref.Id, .. (node.Children ?? []).SelectMany(TreeIds)];

    /// <summary>Completes when <paramref name="token"/> is canceled.</summary>
    public static Task WhenCanceled(CancellationToken token)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => done.TrySetResult());
        return done.Task;
    }

    /// <summary>Upstream's <c>vi.waitFor</c>: polls until <paramref name="condition"/> holds, or fails after ten seconds.</summary>
    public static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "the condition never held");
            await Task.Delay(5);
        }
    }
}
