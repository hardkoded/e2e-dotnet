// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Engine.Protocol;

/// <summary>Options of <see cref="ProtocolClient.ConnectEngine"/>. Upstream's <c>ConnectEngineOptions</c>.</summary>
public sealed record ConnectEngineOptions
{
    /// <summary>
    /// What the server is: its name, version, platform, and the methods it answers. The harness reads it at config load; the server's own
    /// manifest from <c>initialize</c> must match it.
    /// </summary>
    public required EngineManifest Manifest { get; init; }

    /// <summary>How to reach the server: <see cref="SpawnTransport"/>, <see cref="SocketTransport"/>, or <see cref="LoopbackTransport"/>.</summary>
    public required IEngineTransport Transport { get; init; }

    /// <summary>Engine options the server receives in <c>initialize</c>, as JSON.</summary>
    public JsonNode? InitializationOptions { get; init; }

    /// <summary>The <c>Secrets.Get</c> handles the server may resolve; see <see cref="EngineHandle.Secrets"/>.</summary>
    public IReadOnlyList<Secret>? Secrets { get; init; }

    /// <summary>Fixtures this process contributes; see <see cref="EngineHandle.Fixtures"/>. They run on the client, beside the test.</summary>
    public IReadOnlyDictionary<string, Delegate>? Fixtures { get; init; }

    /// <summary>Checks the target's app at config load, on the client; see <see cref="EngineHandle.ValidateApp"/>.</summary>
    public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp { get; init; }

    /// <summary>
    /// Answers locators on the client for a server without <c>engine/locate</c>: each <c>locate</c> observes and resolves the expression over
    /// the tree with the reference semantics (<see cref="ExpressionResolution.ResolveExpression"/>), so a server only has to produce a tree.
    /// </summary>
    public bool ResolveLocators { get; init; }

    /// <summary>
    /// Session hooks this process answers instead of the server, such as a relaunch the host makes from outside a device. Each one wins over
    /// the server's method of the same name.
    /// </summary>
    public EngineSessionSpec? Session { get; init; }
}

/// <summary>
/// The client half of the engine protocol: <see cref="ConnectEngine"/> turns an engine server into an ordinary engine handle, so the harness
/// drives a server in another process, on another machine, or in another language exactly as it drives an engine in its own process.
/// A handle opens three kinds of connection, mirroring where the harness calls an engine: a <c>run</c> connection per target from
/// <c>prepare</c> to <c>finish</c>, a <c>worker</c> connection from <c>init</c> to <c>dispose</c>, and a <c>doctor</c> connection per target
/// for <c>e2e doctor</c>'s one call. None shares state with another. Upstream's <c>engine/protocol/client.ts</c>.
/// </summary>
/// <remarks>.NET-only holder name: C# has no free functions.</remarks>
public static partial class ProtocolClient
{
    /// <summary>Budget of the <c>shutdown</c> handshake when a connection closes outside a cleanup hook. Upstream's <c>DEFAULT_CLOSE_MS</c>.</summary>
    private static readonly TimeSpan DefaultClose = TimeSpan.FromSeconds(5);

    /// <summary>How long a server's <c>doctor</c> may take to stop once its signal aborted, before its connection closes. Upstream's <c>DOCTOR_ABORT_GRACE_MS</c>.</summary>
    private static readonly TimeSpan DoctorAbortGrace = TimeSpan.FromSeconds(5);

    /// <summary>The runner version <c>initialize</c> reports; servers check the protocol version, never this. Upstream's <c>CLIENT_VERSION</c>.</summary>
    private static readonly string ClientVersion = typeof(ProtocolClient).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>Handles <see cref="ConnectEngine"/> made, which already speak the protocol. Upstream's <c>clients</c>.</summary>
    private static readonly ConditionalWeakTable<EngineHandle, object> Clients = new();

    /// <summary>The client each in-process engine is served through, so every config load gets the same one. Upstream's <c>loopbacks</c>.</summary>
    private static readonly ConditionalWeakTable<EngineHandle, EngineHandle> Loopbacks = new();

    /// <summary>
    /// Connects to an engine server and returns it as an engine handle, the value a target's engine takes. The handle is built from the
    /// manifest and checked by <see cref="E2E.Engine.EngineManifest.DefineEngine"/>, so a manifest is held to every rule an engine in this
    /// process is. Upstream's <c>connectEngine</c>.
    /// </summary>
    public static EngineHandle ConnectEngine(ConnectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var handle = E2E.Engine.EngineManifest.DefineEngine(
            new EngineClient(ProtocolManifest.CheckManifest(options.Manifest), options).Engine()
        );
        Clients.Add(handle, handle);
        return handle;
    }

    /// <summary>
    /// The engine as the harness drives it: over the protocol. A <see cref="ConnectEngine"/> handle already is; an engine in this process is
    /// served over <see cref="LoopbackTransport"/>, so every call crosses the wire format a remote engine would receive. Fixtures, the app
    /// check, and the declared secrets stay the engine's own: they belong to the client half of any engine. One engine always gets the same
    /// client, so a handle two targets or two config loads share stays one instance. Upstream's <c>protocolEngine</c>.
    /// </summary>
    public static EngineHandle ProtocolEngine(EngineHandle engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (Clients.TryGetValue(engine, out _))
        {
            return engine;
        }

        lock (Loopbacks)
        {
            if (!Loopbacks.TryGetValue(engine, out var client))
            {
                client = ConnectEngine(
                    new ConnectEngineOptions
                    {
                        Manifest = ProtocolManifest.DescribeEngine(engine),
                        Transport = new LoopbackTransport(engine),
                        Secrets = engine.Secrets,
                        Fixtures = engine.Fixtures,
                        ValidateApp = engine.ValidateApp,
                    }
                );
                Loopbacks.Add(engine, client);
            }

            return client;
        }
    }

    /// <summary>What opening a connection needs from the hook that opens it. Upstream's <c>OpenContext</c>.</summary>
    private sealed record OpenContext(
        IReadOnlyDictionary<string, string?> Env,
        string Cwd,
        CancellationToken Signal,
        EngineAppInfo? App,
        Action<string> Log
    );

    /// <summary>One open connection and the state that lives exactly as long as it. Upstream's <c>Link</c>.</summary>
    private sealed record Link(Connection Connection, ITransportConnection Transport, SignalEncoder Signals);

    /// <summary>The result of <c>artifacts/screenshot</c>: the path a server wrote, or the image itself for the client to write. Upstream's <c>ScreenshotResult</c>.</summary>
    private sealed record ScreenshotResult(string? Path, string? Data);

    /// <summary>The engine half of <see cref="ConnectEngine"/>: one server's connections, and the engine members that drive them. Upstream's <c>EngineClient</c>.</summary>
    private sealed class EngineClient(EngineManifest manifest, ConnectEngineOptions options)
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Secret> _secrets = (options.Secrets ?? []).ToDictionary(
            secret => secret.Name,
            StringComparer.Ordinal
        );

        /// <summary>One run connection per target: a handle two targets share is prepared and finished once for each.</summary>
        private readonly Dictionary<string, Task<Link>> _runs = new(StringComparer.Ordinal);

        /// <summary>
        /// Worker connections, oldest first. A handle one process shares (an MCP session closing while the next opens) runs <c>init</c> again
        /// before the previous <c>dispose</c>: calls go to the newest, <c>dispose</c> ends the oldest.
        /// </summary>
        private readonly List<Task<Link>> _workers = [];

        /// <summary>Attempts whose <c>endAttempt</c> has begun: a screenshot's bytes no longer land in them, though callbacks still reach them until it settles.</summary>
        private readonly ConditionalWeakTable<EngineAttemptContext, object> _ending = new();

        private EngineAttemptContext? _attempt;
        private int _screenshots;

        /// <summary>The engine the manifest describes, each declared method one member; <c>DefineEngine</c> checks it.</summary>
        public EngineSpec Engine()
        {
            bool Has(string method) => manifest.Methods.Contains(method);
            var spec = new EngineSpec
            {
                Name = manifest.Name,
                Version = manifest.Version,
                SpiVersion = manifest.SpiVersion,
                Platform = manifest.Platform,
                Workers = manifest.Workers,
                Actions = manifest.Actions,
                PointerActions = manifest.PointerActions,
                TapModifiers = manifest.TapModifiers,
                SelectsDevice = manifest.SelectsDevice,
                GrantsAllPermissions = manifest.GrantsAllPermissions,
                LaunchArgumentValues = manifest.LaunchArgumentValues,
                FileApps = manifest.FileApps,
                Isolation = manifest.Isolation,
                Secrets = options.Secrets,
                Fixtures = options.Fixtures,
                ValidateApp = options.ValidateApp,
                Observe = Has("engine/observe")
                    ? async (operation, observe) =>
                        Protocol.DecodeSnapshot(
                            WireCodec.Read<WireSnapshot>(
                                await OperateAsync("engine/observe", link =>
                                    {
                                        var parameters = new JsonObject
                                        {
                                            ["operation"] = EncodeOperation(link, operation),
                                        };
                                        if (observe is not null)
                                        {
                                            parameters["options"] = WireCodec.Write(observe);
                                        }

                                        return parameters;
                                    })
                                    .ConfigureAwait(false)
                            )
                        )
                    : null,
                ReadStyle = Has("engine/readStyle")
                    ? async (reference, properties, operation) =>
                        WireCodec.Read<Dictionary<string, string>>(
                            await OperateAsync("engine/readStyle", link => new JsonObject
                                {
                                    ["ref"] = WireCodec.Write(reference),
                                    ["properties"] = WireCodec.Write(properties),
                                    ["operation"] = EncodeOperation(link, operation),
                                })
                                .ConfigureAwait(false)
                        )
                    : null,
                ViewportIntersection = Has("engine/viewportIntersection")
                    ? async (reference, operation) =>
                        WireCodec.Read<ViewportIntersection>(
                            await OperateAsync("engine/viewportIntersection", link => new JsonObject
                                {
                                    ["ref"] = WireCodec.Write(reference),
                                    ["operation"] = EncodeOperation(link, operation),
                                })
                                .ConfigureAwait(false)
                        )
                    : null,
                Locate = Has("engine/locate")
                    ? async (expression, operation) =>
                        WireCodec.Read<EngineNode[]>(
                            await OperateAsync("engine/locate", link => new JsonObject
                                {
                                    ["expression"] = WireCodec.EncodeExpression(expression),
                                    ["operation"] = EncodeOperation(link, operation),
                                })
                                .ConfigureAwait(false)
                        )
                    : null,
                Perform = Has("engine/perform")
                    ? async (reference, action, operation) =>
                        await OperateAsync("engine/perform", link => new JsonObject
                            {
                                ["ref"] = WireCodec.Write(reference),
                                ["action"] = WireCodec.EncodeLocatorAction(action),
                                ["operation"] = EncodeOperation(link, operation),
                            })
                            .ConfigureAwait(false)
                    : null,
                PerformAt = Has("engine/performAt")
                    ? async (point, action, operation) =>
                        await OperateAsync("engine/performAt", link => new JsonObject
                            {
                                ["point"] = WireCodec.Write(point),
                                ["action"] = WireCodec.EncodePointerAction(action),
                                ["operation"] = EncodeOperation(link, operation),
                            })
                            .ConfigureAwait(false)
                    : null,
                Keyboard = Keyboard(Has),
                State = State(Has),
                Artifacts = Artifacts(Has),
                Session = Session(Has),
                Prepare = Has("lifecycle/prepare") || Has("lifecycle/finish") ? info => PrepareAsync(info) : null,
                Finish = Has("lifecycle/prepare") || Has("lifecycle/finish") ? info => FinishAsync(info) : null,
                Doctor = Has("lifecycle/doctor") ? info => DoctorAsync(info) : null,
                SettleAttempt = Has("lifecycle/settleAttempt")
                    ? context => ForwardAsync("lifecycle/settleAttempt", context)
                    : null,
                Init = InitAsync,
                StartAttempt = StartAttemptAsync,
                EndAttempt = EndAttemptAsync,
                Dispose = DisposeAsync,
            };
            if (spec.Locate is null && options.ResolveLocators && spec.Observe is { } observeHook)
            {
                spec = spec with
                {
                    Locate = async (expression, operation) =>
                        ResolveOnTheClient(
                            expression,
                            (await observeHook(operation, null).ConfigureAwait(false)).Root.Children ?? []
                        ),
                };
            }

            return spec;
        }

        private static IReadOnlyList<EngineNode> ResolveOnTheClient(
            LocatorExpression expression,
            IReadOnlyList<EngineNode> children
        )
        {
            var index = new Dictionary<string, EngineNode>(StringComparer.Ordinal);
            SemanticNode Convert(EngineNode node)
            {
                index[node.Ref.Id] = node;
                return new SemanticNode
                {
                    Ref = node.Ref.Id,
                    Role = node.Role,
                    Name = node.Name,
                    Children = [.. (node.Children ?? []).Select(Convert)],
                };
            }

            var roots = children.Select(Convert).ToList();
            return [.. ExpressionResolution.ResolveExpression(expression, roots).Select(match => index[match.Ref])];
        }

        private EngineKeyboardSpec? Keyboard(Func<string, bool> has) =>
            has("keyboard/type") || has("keyboard/press") || has("keyboard/dismiss")
                ? new EngineKeyboardSpec
                {
                    Type = has("keyboard/type")
                        ? (text, replace, operation) => OperateAsync("keyboard/type", link => new JsonObject
                            {
                                ["text"] = text,
                                ["options"] = new JsonObject { ["replace"] = replace },
                                ["operation"] = EncodeOperation(link, operation),
                            })
                        : null,
                    Press = has("keyboard/press")
                        ? (key, operation) =>
                            OperateAsync("keyboard/press", link => new JsonObject
                            {
                                ["key"] = key,
                                ["operation"] = EncodeOperation(link, operation),
                            })
                        : null,
                    Dismiss = has("keyboard/dismiss") ? operation => OperateAsync("keyboard/dismiss", operation) : null,
                }
                : null;

        private EngineStateSpec? State(Func<string, bool> has) =>
            has("state/capture") || has("state/restore")
                ? new EngineStateSpec
                {
                    Capture = has("state/capture")
                        ? async operation =>
                            WireCodec.Read<EngineState>(
                                await OperateAsync("state/capture", link => OperationOnly(link, operation))
                                    .ConfigureAwait(false)
                            )
                        : null,
                    Restore = has("state/restore")
                        ? (state, operation) =>
                            OperateAsync("state/restore", link => new JsonObject
                            {
                                ["state"] = WireCodec.Write(state),
                                ["operation"] = EncodeOperation(link, operation),
                            })
                        : null,
                }
                : null;

        private EngineArtifactsSpec? Artifacts(Func<string, bool> has) =>
            has("artifacts/screenshot") || has("artifacts/startVideo") || has("artifacts/stopVideo")
                ? new EngineArtifactsSpec
                {
                    Screenshot = has("artifacts/screenshot") ? ScreenshotAsync : null,
                    StartVideo = has("artifacts/startVideo")
                        ? operation => OperateAsync("artifacts/startVideo", operation)
                        : null,
                    StopVideo = has("artifacts/stopVideo")
                        ? async operation =>
                            WireCodec.DecodeVideoSegments(
                                await OperateAsync("artifacts/stopVideo", link => OperationOnly(link, operation))
                                    .ConfigureAwait(false)
                            )
                        : null,
                }
                : null;

        /// <summary>The session hooks the server answers, each overridden by the one the client declares.</summary>
        private EngineSessionSpec? Session(Func<string, bool> has)
        {
            Func<OperationContext, Task>? Plain(string method) =>
                has(method) ? operation => OperateAsync(method, operation) : null;
            Func<string, OperationContext, Task>? Url(string method) =>
                has(method)
                    ? (url, operation) =>
                        OperateAsync(method, link => new JsonObject
                        {
                            ["url"] = url,
                            ["operation"] = EncodeOperation(link, operation),
                        })
                    : null;
            Func<int, OperationContext, Task>? Tab(string method) =>
                has(method)
                    ? (index, operation) =>
                        OperateAsync(method, link => new JsonObject
                        {
                            ["index"] = index,
                            ["operation"] = EncodeOperation(link, operation),
                        })
                    : null;
            var served = new EngineSessionSpec
            {
                Open = Url("session/open"),
                OpenLink = Url("session/openLink"),
                Back = Plain("session/back"),
                Resize = has("session/resize")
                    ? (size, operation) =>
                        OperateAsync("session/resize", link => new JsonObject
                        {
                            ["size"] = WireCodec.Write(size),
                            ["operation"] = EncodeOperation(link, operation),
                        })
                    : null,
                SwitchTab = Tab("session/switchTab"),
                CloseTab = Tab("session/closeTab"),
                Restart = Plain("session/restart"),
                Reset = Plain("session/reset"),
            };
            var own = options.Session;
            if (own is null)
            {
                return served == new EngineSessionSpec() ? null : served;
            }

            return new EngineSessionSpec
            {
                Open = own.Open ?? served.Open,
                OpenLink = own.OpenLink ?? served.OpenLink,
                Back = own.Back ?? served.Back,
                Resize = own.Resize ?? served.Resize,
                SwitchTab = own.SwitchTab ?? served.SwitchTab,
                CloseTab = own.CloseTab ?? served.CloseTab,
                Restart = own.Restart ?? served.Restart,
                Reset = own.Reset ?? served.Reset,
            };
        }

        private static JsonObject OperationOnly(Link link, OperationContext operation) =>
            new() { ["operation"] = EncodeOperation(link, operation) };

        private Task OperateAsync(string method, OperationContext operation) =>
            OperateAsync(method, link => OperationOnly(link, operation));

        /// <summary>Sends one operation method over the worker connection: its arguments by name, its result decoded by the caller.</summary>
        private async Task<JsonNode?> OperateAsync(string method, Func<Link, JsonObject> parameters)
        {
            var link = await WorkerLinkAsync().ConfigureAwait(false);
            return await CallAsync(link, method, parameters(link)).ConfigureAwait(false);
        }

        /// <summary>Sends <c>artifacts/screenshot</c>; the attempt that asked is read before the server answers, so a late answer cannot land in the next one.</summary>
        private async Task<string> ScreenshotAsync(string? label, OperationContext operation)
        {
            var link = await WorkerLinkAsync().ConfigureAwait(false);
            var parameters = new JsonObject { ["operation"] = EncodeOperation(link, operation) };
            if (label is not null)
            {
                parameters["label"] = label;
            }

            EngineAttemptContext? attempt;
            lock (_gate)
            {
                attempt = _attempt;
            }

            var result = await CallAsync(link, "artifacts/screenshot", parameters).ConfigureAwait(false);
            return await WriteScreenshotAsync(
                    attempt,
                    new ScreenshotResult(Text(result?["path"]), Text(result?["data"])),
                    label
                )
                .ConfigureAwait(false);
        }

        private static string? Text(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private async Task PrepareAsync(EnginePrepareInfo info)
        {
            var pending = OpenAsync(
                ConnectionRole.Run,
                new OpenContext(
                    info.Env,
                    info.ProjectRoot ?? Directory.GetCurrentDirectory(),
                    info.Signal,
                    info.App,
                    info.Log
                )
            );
            lock (_gate)
            {
                _runs[info.TargetName] = pending;
            }

            var link = await pending.ConfigureAwait(false);
            if (!manifest.Methods.Contains("lifecycle/prepare"))
            {
                return;
            }

            var parameters = new JsonObject
            {
                ["runId"] = info.RunId,
                ["targetName"] = info.TargetName,
                ["slots"] = info.Slots,
                ["env"] = WireCodec.Write(info.Env),
                ["headed"] = info.Headed,
                ["signal"] = WireCodec.Write(link.Signals.Encode(info.Signal)),
            };
            AddHost(parameters, info.HostId, info.ProjectRoot, info.App);
            await CallAsync(link, "lifecycle/prepare", parameters).ConfigureAwait(false);
        }

        private static void AddHost(JsonObject parameters, string? hostId, string? projectRoot, EngineAppInfo? app)
        {
            if (hostId is not null)
            {
                parameters["hostId"] = hostId;
            }

            if (projectRoot is not null)
            {
                parameters["projectRoot"] = projectRoot;
            }

            if (app is not null)
            {
                parameters["app"] = WireCodec.Write(app);
            }
        }

        private async Task FinishAsync(EngineFinishInfo info)
        {
            Task<Link>? pending;
            lock (_gate)
            {
                _runs.Remove(info.TargetName, out pending);
            }

            var link = await Settled(pending).ConfigureAwait(false);
            if (link is null)
            {
                return;
            }

            var deadline = DateTimeOffset.UtcNow + info.CleanupTimeout;
            try
            {
                if (manifest.Methods.Contains("lifecycle/finish"))
                {
                    var parameters = new JsonObject
                    {
                        ["runId"] = info.RunId,
                        ["targetName"] = info.TargetName,
                        ["env"] = WireCodec.Write(info.Env),
                        ["timeoutMs"] = info.CleanupTimeout.TotalMilliseconds,
                        ["signal"] = WireCodec.Write(link.Signals.Encode(info.Signal)),
                    };
                    if (info.HostId is not null)
                    {
                        parameters["hostId"] = info.HostId;
                    }

                    await CleanupCallAsync(link, "lifecycle/finish", parameters, info.Signal, info.CleanupTimeout)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await CloseAsync(link, Remaining(deadline)).ConfigureAwait(false);
            }
        }

        /// <summary>The link of a connection that opened, or null for one that never did or failed to.</summary>
        private static async Task<Link?> Settled(Task<Link>? pending)
        {
            if (pending is null)
            {
                return null;
            }

            try
            {
                return await pending.ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static TimeSpan Remaining(DateTimeOffset deadline)
        {
            var left = deadline - DateTimeOffset.UtcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>
        /// Opens a <c>doctor</c> connection for the hook alone and closes it after: <c>e2e doctor</c> starts no run. At the signal the server is
        /// told to stop and gets <see cref="DoctorAbortGrace"/> to answer before the connection closes.
        /// </summary>
        private async Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info)
        {
            if (info.Signal.IsCancellationRequested)
            {
                throw Cancelled();
            }

            var link = await OpenAsync(
                    ConnectionRole.Doctor,
                    new OpenContext(info.Env, info.ProjectRoot, info.Signal, info.App, info.Log)
                )
                .ConfigureAwait(false);
            var parameters = new JsonObject
            {
                ["targetName"] = info.TargetName,
                ["projectRoot"] = info.ProjectRoot,
                ["app"] = WireCodec.Write(info.App),
                ["env"] = WireCodec.Write(info.Env),
                ["headed"] = info.Headed,
                ["signal"] = WireCodec.Write(link.Signals.Encode(info.Signal)),
            };
            var call = CallAsync(link, "lifecycle/doctor", parameters);
            try
            {
                var checks = await WithAbortAsync(call, info.Signal).ConfigureAwait(false);
                return WireCodec.Read<EngineDoctorCheck[]>(checks);
            }
            catch (Exception)
            {
                if (info.Signal.IsCancellationRequested)
                {
                    _ = await WithinCleanupBudgetAsync(call, DoctorAbortGrace).ConfigureAwait(false);
                }

                throw;
            }
            finally
            {
                await CloseAsync(link, DefaultClose).ConfigureAwait(false);
            }
        }

        private async Task InitAsync(EngineInitInfo info)
        {
            var pending = OpenAsync(
                ConnectionRole.Worker,
                new OpenContext(
                    info.Env,
                    info.ProjectRoot ?? Directory.GetCurrentDirectory(),
                    info.Signal,
                    info.App,
                    info.Log
                )
            );
            lock (_gate)
            {
                _workers.Add(pending);
            }

            var link = await pending.ConfigureAwait(false);
            if (manifest.Methods.Contains("lifecycle/init"))
            {
                var parameters = new JsonObject
                {
                    ["runId"] = info.RunId,
                    ["targetName"] = info.TargetName,
                    ["workerSlot"] = info.WorkerSlot,
                    ["env"] = WireCodec.Write(info.Env),
                    ["headed"] = info.Headed,
                    ["signal"] = WireCodec.Write(link.Signals.Encode(info.Signal)),
                };
                AddHost(parameters, info.HostId, info.ProjectRoot, info.App);
                await CallAsync(link, "lifecycle/init", parameters).ConfigureAwait(false);
            }
        }

        /// <summary>Tracks the attempt, which the server's callbacks and a screenshot's bytes land in, and forwards it when the server answers.</summary>
        private async Task StartAttemptAsync(EngineAttemptContext context)
        {
            lock (_gate)
            {
                _attempt = context;
                _ending.Remove(context);
                _screenshots = 0;
            }

            if (!manifest.Methods.Contains("lifecycle/startAttempt"))
            {
                return;
            }

            var link = await WorkerLinkAsync().ConfigureAwait(false);
            await CallAsync(
                    link,
                    "lifecycle/startAttempt",
                    new JsonObject
                    {
                        ["attemptId"] = context.AttemptId,
                        ["artifactsDir"] = context.ArtifactsDir,
                        ["signal"] = WireCodec.Write(link.Signals.Encode(context.Signal)),
                        ["screens"] = context.Screen is not null,
                    }
                )
                .ConfigureAwait(false);
        }

        private async Task EndAttemptAsync(EngineCleanupContext context)
        {
            // A hook that outlived its budget settles after the next attempt started: it retires only the attempt it ended.
            EngineAttemptContext? ending;
            lock (_gate)
            {
                ending = _attempt;
                // The runner may stop waiting for a server that never answers; bytes it sends meanwhile have no attempt to land in.
                if (ending is not null)
                {
                    _ending.AddOrUpdate(ending, ending);
                }
            }

            try
            {
                if (manifest.Methods.Contains("lifecycle/endAttempt"))
                {
                    await ForwardAsync("lifecycle/endAttempt", context).ConfigureAwait(false);
                }
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_attempt, ending))
                    {
                        _attempt = null;
                    }
                }
            }
        }

        /// <summary>
        /// Ends the oldest worker connection. A worker that never called <c>init</c> has no connection, and a fresh connection's engine has
        /// nothing to release, so the server gets no <c>lifecycle/dispose</c> for it.
        /// </summary>
        private async Task DisposeAsync(EngineDisposeContext context)
        {
            Task<Link>? pending = null;
            lock (_gate)
            {
                if (_workers.Count > 0)
                {
                    pending = _workers[0];
                    _workers.RemoveAt(0);
                }

                if (_workers.Count == 0)
                {
                    _attempt = null;
                }
            }

            var link = await Settled(pending).ConfigureAwait(false);
            if (link is null)
            {
                return;
            }

            var deadline = DateTimeOffset.UtcNow + context.CleanupTimeout;
            try
            {
                if (manifest.Methods.Contains("lifecycle/dispose"))
                {
                    var parameters = new JsonObject
                    {
                        ["signal"] = WireCodec.Write(link.Signals.Encode(context.Signal)),
                        ["timeoutMs"] = context.CleanupTimeout.TotalMilliseconds,
                    };
                    if (context.ReuseSignal is { } reuse)
                    {
                        parameters["reuseSignal"] = WireCodec.Write(link.Signals.Encode(reuse));
                    }

                    await CleanupCallAsync(
                            link,
                            "lifecycle/dispose",
                            parameters,
                            context.Signal,
                            context.CleanupTimeout
                        )
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                var left = Remaining(deadline);
                await CloseAsync(link, left < DefaultClose ? left : DefaultClose).ConfigureAwait(false);
            }
        }

        /// <summary>Sends a cleanup hook over the worker connection.</summary>
        private async Task ForwardAsync(string method, EngineCleanupContext context)
        {
            var link = await WorkerLinkAsync().ConfigureAwait(false);
            await CallAsync(
                    link,
                    method,
                    new JsonObject
                    {
                        ["signal"] = WireCodec.Write(link.Signals.Encode(context.Signal)),
                        ["timeoutMs"] = context.Timeout.TotalMilliseconds,
                    }
                )
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a hook that ends a connection, bounded by its own signal and budget: a server that ignores both must not keep the
        /// connection, or its process, open past them.
        /// </summary>
        private async Task CleanupCallAsync(
            Link link,
            string method,
            JsonObject parameters,
            CancellationToken signal,
            TimeSpan timeout
        )
        {
            var call = CallAsync(link, method, parameters);
            // A call the budget abandons may still fail later; nobody awaits it then.
            _ = call.ContinueWith(
                static finished => _ = finished.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(signal);
            bound.CancelAfter(timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout);
            try
            {
                await call.WaitAsync(bound.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (bound.IsCancellationRequested && !call.IsCompleted)
            {
                throw new EngineException(
                    EngineErrorCodes.OperationTimeout,
                    $"engine server {options.Transport.Label} did not answer {method} within {timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms",
                    false
                );
            }
        }

        /// <summary>
        /// Where a screenshot landed: a path the server wrote, which must stay under the artifact directory of the attempt that asked for it, or
        /// the bytes it returned, written there by the client until that attempt starts ending.
        /// </summary>
        private async Task<string> WriteScreenshotAsync(
            EngineAttemptContext? attempt,
            ScreenshotResult result,
            string? label
        )
        {
            if (result.Path is { } written)
            {
                if (
                    attempt is not null
                    && (
                        Path.IsPathRooted(written)
                        || Paths.RelativeToProjectRoot(
                            attempt.ArtifactsDir,
                            Path.GetFullPath(Path.Combine(attempt.ArtifactsDir, written))
                        )
                            is null
                    )
                )
                {
                    throw new EngineException(
                        EngineErrorCodes.EngineFailure,
                        $"engine server wrote a screenshot outside the attempt's artifact directory: {written}"
                    );
                }

                return written;
            }

            if (attempt is null)
            {
                throw new EngineException(EngineErrorCodes.InvalidState, "screenshot outside an attempt");
            }

            int number;
            lock (_gate)
            {
                if (!ReferenceEquals(attempt, _attempt) || _ending.TryGetValue(attempt, out _))
                {
                    throw new EngineException(
                        EngineErrorCodes.InvalidState,
                        "screenshot answered after its attempt ended"
                    );
                }

                number = ++_screenshots;
            }

            var name = UnsafeNameCharacters().Replace(label ?? "screenshot", "_");
            name = name.Length > 64 ? name[..64] : name;
            if (name.Length == 0)
            {
                name = "screenshot";
            }

            var relative = Path.Combine(
                "screenshots",
                $"{number.ToString("000", CultureInfo.InvariantCulture)}-{name}.png"
            );
            Directory.CreateDirectory(Path.Combine(attempt.ArtifactsDir, "screenshots"));
            await File.WriteAllBytesAsync(
                    Path.Combine(attempt.ArtifactsDir, relative),
                    Convert.FromBase64String(
                        result.Data
                            ?? throw new EngineException(
                                EngineErrorCodes.EngineFailure,
                                "engine server answered a screenshot with neither a path nor data"
                            )
                    )
                )
                .ConfigureAwait(false);
            return relative;
        }

        private Task<Link> WorkerLinkAsync()
        {
            lock (_gate)
            {
                if (_workers.Count == 0)
                {
                    throw new EngineException(
                        EngineErrorCodes.InvalidState,
                        $"engine \"{manifest.Name}\" is not initialized: init opens its connection"
                    );
                }

                return _workers[^1];
            }
        }

        /// <summary>Opens a connection: the transport, then <c>initialize</c>, whose manifest must match the declared one.</summary>
        private async Task<Link> OpenAsync(string role, OpenContext context)
        {
            var transport = options.Transport;
            // The transport's signal covers opening only: the hook's own signal can cancel long after (a worker's init signal cancels with the worker).
            ITransportConnection streams;
            using (var opening = CancellationTokenSource.CreateLinkedTokenSource(context.Signal))
            {
                streams = await transport
                    .OpenAsync(
                        new TransportContext
                        {
                            Role = role,
                            Env = context.Env,
                            Cwd = context.Cwd,
                            App = context.App,
                            Signal = opening.Token,
                        }
                    )
                    .ConfigureAwait(false);
            }

            var signals = new SignalEncoder();
            var connection = Connect(streams, context.Log, signals.Dispose);
            signals.Attach(connection);
            var link = new Link(connection, streams, signals);
            try
            {
                var parameters = new InitializeParams(
                    Protocol.EngineProtocolVersion,
                    new ClientInfo("e2e", ClientVersion),
                    role,
                    options.InitializationOptions
                );
                var answered = await WithAbortAsync(
                        CallAsync(link, "initialize", WireCodec.Write(parameters)!.AsObject()),
                        context.Signal
                    )
                    .ConfigureAwait(false);
                var result = answered is null ? null : WireCodec.Read<InitializeResult>(answered);
                if (result?.ProtocolVersion != Protocol.EngineProtocolVersion)
                {
                    throw Infrastructure(
                        $"engine server {transport.Label} speaks protocol {result?.ProtocolVersion.ToString(CultureInfo.InvariantCulture) ?? "undefined"}; this runner speaks {Protocol.EngineProtocolVersion.ToString(CultureInfo.InvariantCulture)}"
                    );
                }

                if (ProtocolManifest.ManifestMismatch(manifest, result.Manifest) is { } mismatch)
                {
                    throw Infrastructure(
                        $"engine server {transport.Label} announced a manifest that differs from the one declared for \"{manifest.Name}\": {mismatch}"
                    );
                }

                connection.Notify("initialized", new JsonObject());
                return link;
            }
            catch (Exception)
            {
                connection.Close("initialize failed");
                await streams.CloseAsync(TimeSpan.Zero).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>A connection over <paramref name="streams"/> that routes the server's callbacks to the live attempt and its log lines to <paramref name="log"/>.</summary>
        private Connection Connect(ITransportConnection streams, Action<string> log, Action onClose)
        {
            EngineAttemptContext? Attempt(JsonNode? parameters)
            {
                var attemptId = Text(parameters?["attemptId"])
                    ?? throw new System.Text.Json.JsonException("\"attemptId\" must be a string");
                lock (_gate)
                {
                    return _attempt?.AttemptId == attemptId ? _attempt : null;
                }
            }

            Connection? connection = null;
            // The server derives a secret's redacted forms itself, asked back while the value resolves, so the provider runs once.
            async Task<IReadOnlyList<string>> Derive(int derivation, string plaintext)
            {
                try
                {
                    var answer = await connection!
                        .RequestAsync(
                            "attempt/deriveSecret",
                            WireCodec.Write(new DeriveSecretParams(derivation, plaintext))
                        )
                        .ConfigureAwait(false);
                    return WireCodec.Read<string[]>(answer?["values"]);
                }
                catch (RpcException cause)
                {
                    throw Protocol.DecodeError(new RpcErrorObject(cause.Code, cause.Message, cause.Data));
                }
            }

            connection = new Connection(
                new MessageStreams(streams.Input, streams.Output),
                new ConnectionOptions
                {
                    EncodeError = cause => Protocol.EncodeError(cause),
                    OnClose = _ => onClose(),
                    OnNotificationError = (method, cause) =>
                        log(
                            $"engine server {options.Transport.Label} sent a malformed \"{method}\" notification: {cause.Message}"
                        ),
                    Notifications = new Dictionary<string, Action<JsonNode?>>
                    {
                        ["engine/log"] = parameters =>
                            log(
                                Text(parameters?["line"])
                                    ?? throw new System.Text.Json.JsonException("\"line\" must be a string")
                            ),
                        ["attempt/appLog"] = parameters =>
                        {
                            var attempt = Attempt(parameters);
                            var entry = WireCodec.Read<AppLogEntry>(parameters!["entry"]);
                            attempt?.AppLog(entry);
                        },
                        ["attempt/navigation"] = parameters =>
                        {
                            var attempt = Attempt(parameters);
                            var line = Text(parameters!["line"])
                                ?? throw new System.Text.Json.JsonException("\"line\" must be a string");
                            attempt?.Navigation(line);
                        },
                        ["attempt/environment"] = parameters =>
                        {
                            var attempt = Attempt(parameters);
                            var facts = WireCodec.Read<Dictionary<string, string>>(parameters!["facts"]);
                            attempt?.Environment(facts);
                        },
                        ["attempt/screen"] = parameters =>
                        {
                            var attempt = Attempt(parameters);
                            var snapshot = Protocol.DecodeSnapshot(
                                WireCodec.Read<WireSnapshot>(parameters!["snapshot"])
                            );
                            attempt?.Screen?.Invoke(snapshot);
                        },
                    },
                    Requests = new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
                    {
                        ["attempt/resolveSecret"] = async parameters =>
                        {
                            // Off the read loop: a derivation waits for a response only that loop can read.
                            await Task.Yield();
                            var attempt = Attempt(parameters)
                                ?? throw new InvalidOperationException(
                                    $"attempt {Text(parameters?["attemptId"])} is not running"
                                );
                            var name = Text(parameters?["name"])
                                ?? throw new System.Text.Json.JsonException("\"name\" must be a string");
                            var derivation = parameters?["derivation"] is { } wire ? (int?)wire : null;
                            var resolveOptions = derivation is { } id
                                ? new ResolveSecretOptions
                                {
                                    Derived = plaintext => Derive(id, plaintext).GetAwaiter().GetResult(),
                                }
                                : null;
                            var value = await attempt
                                .ResolveSecret(SecretNamed(name), resolveOptions)
                                .ConfigureAwait(false);
                            return new JsonObject { ["value"] = value };
                        },
                    },
                }
            );
            return connection;
        }

        // An undeclared name still reaches the harness, which refuses it with the message it gives an engine in its own process.
        private Secret SecretNamed(string name) =>
            _secrets.TryGetValue(name, out var secret)
                ? secret
                : Secret.Create(name, _ => Task.FromResult(string.Empty), "generic-secret");

        /// <summary>Ends a connection: <c>shutdown</c>, <c>exit</c>, then the transport, all within <paramref name="timeout"/>.</summary>
        private static async Task CloseAsync(Link link, TimeSpan timeout)
        {
            timeout = timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout;
            var deadline = DateTimeOffset.UtcNow + timeout;
            var reason = "the client closed the connection";
            try
            {
                if (link.Connection.Closed is null)
                {
                    await link.Connection.RequestAsync("shutdown").WaitAsync(timeout).ConfigureAwait(false);
                    link.Connection.Notify("exit");
                }
            }
            catch (Exception cause)
            {
                // A server that cannot shut down cleanly is stopped by its transport.
                reason = $"the client closed the connection; the server did not shut down cleanly: {cause.Message}";
            }
            finally
            {
                link.Connection.Close(reason);
                await link.Transport.CloseAsync(Remaining(deadline)).ConfigureAwait(false);
            }
        }

        /// <summary>Sends one request and rethrows its failure as the error the engine member threw.</summary>
        private async Task<JsonNode?> CallAsync(Link link, string method, JsonNode parameters)
        {
            try
            {
                return await link.Connection.RequestAsync(method, parameters).ConfigureAwait(false);
            }
            catch (RpcException cause)
            {
                if (cause.Code == RpcErrors.ConnectionClosed)
                {
                    var diagnostics = link.Transport.Diagnostics();
                    throw Infrastructure(
                        $"engine server {options.Transport.Label} closed the connection: {cause.Message}{(diagnostics.Length == 0 ? "" : $"; {diagnostics}")}"
                    );
                }

                throw Protocol.DecodeError(new RpcErrorObject(cause.Code, cause.Message, cause.Data));
            }
        }
    }

    /// <summary>Upstream's <c>InfrastructureError('ENGINE_FAILURE', ...)</c>: an engine failure the runner named itself.</summary>
    private static EngineException Infrastructure(string message) =>
        new(EngineErrorCodes.EngineFailure, message) { Mapped = true };

    /// <summary>Upstream's <c>cancelledError</c>: the error a bare cancellation stands for.</summary>
    private static EngineException Cancelled() =>
        new(EngineErrorCodes.Cancelled, "operation cancelled") { Mapped = true };

    /// <summary>Waits for <paramref name="task"/>, or throws <see cref="Cancelled"/> once <paramref name="signal"/> is canceled. Upstream's <c>withAbort</c>.</summary>
    private static async Task<T> WithAbortAsync<T>(Task<T> task, CancellationToken signal)
    {
        try
        {
            return await task.WaitAsync(signal).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (signal.IsCancellationRequested && !task.IsCompleted)
        {
            throw Cancelled();
        }
    }

    /// <summary>Waits for <paramref name="task"/> to settle, however it does, for at most <paramref name="budget"/>; true when it succeeded. Upstream's <c>withinCleanupBudget</c>.</summary>
    private static async Task<bool> WithinCleanupBudgetAsync(Task task, TimeSpan budget)
    {
        try
        {
            await task.WaitAsync(budget < TimeSpan.Zero ? TimeSpan.Zero : budget).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            // The call's own failure, or the end of the budget: either way the wait is over.
            return false;
        }
    }

    /// <summary>
    /// An operation context on the wire. A context with no <see cref="OperationContext.Timeout"/> (one built by hand) crosses with the default
    /// action timeout, since the wire requires one.
    /// </summary>
    private static JsonNode? EncodeOperation(Link link, OperationContext operation) =>
        WireCodec.Write(
            new WireOperation(
                link.Signals.Encode(operation.Signal),
                (operation.Timeout ?? E2EDefaults.ActionTimeout).TotalMilliseconds,
                operation.RunId,
                operation.AttemptId,
                operation.Origin == OperationOrigin.Agent ? "agent" : "test"
            )
        );

    [GeneratedRegex("[^A-Za-z0-9._-]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnsafeNameCharacters();
}

/// <summary>
/// Mints one id per <see cref="CancellationToken"/> a connection carries and sends <c>$/abort</c> when it is canceled. The server forgets an
/// id when its signal aborts or when the attempt it was first seen in ends. Disposed when the connection closes, which detaches it from
/// every token it listened to. Upstream's <c>SignalEncoder</c>.
/// </summary>
internal sealed class SignalEncoder : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<CancellationToken, int> _ids = [];
    private readonly List<CancellationTokenRegistration> _registrations = [];
    private Connection? _connection;
    private int _next = 1;
    private bool _disposed;

    /// <summary>Sends this encoder's <c>$/abort</c> notifications over <paramref name="connection"/>.</summary>
    public void Attach(Connection connection) => _connection = connection;

    /// <summary>Removes every cancellation callback this encoder added.</summary>
    public void Dispose()
    {
        List<CancellationTokenRegistration> registrations;
        lock (_gate)
        {
            _disposed = true;
            registrations = [.. _registrations];
            _registrations.Clear();
            _ids.Clear();
        }

        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }

    public WireSignal Encode(CancellationToken signal)
    {
        lock (_gate)
        {
            if (!_ids.TryGetValue(signal, out var id))
            {
                id = _next++;
                if (!_disposed)
                {
                    _ids[signal] = id;
                }

                if (!signal.IsCancellationRequested && !_disposed && signal.CanBeCanceled)
                {
                    var aborted = id;
                    _registrations.Add(
                        signal.UnsafeRegister(
                            _ =>
                            {
                                // A canceled token is encoded as aborted from here on, so its id is not kept.
                                lock (_gate)
                                {
                                    _ids.Remove(signal);
                                }

                                _connection?.Notify("$/abort", WireCodec.Write(new AbortParams(aborted)));
                            },
                            null
                        )
                    );
                }
            }

            return signal.IsCancellationRequested ? new WireSignal(id, true) : new WireSignal(id);
        }
    }
}
