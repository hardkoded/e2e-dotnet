// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Reports;

namespace E2E.Engine.Protocol;

/// <summary>
/// The server half of the engine protocol: <see cref="ServeEngineAsync(EngineHandle, MessageStreams?)"/> answers the protocol with an engine
/// built by <see cref="E2E.Engine.EngineManifest.DefineEngine"/>, so any engine written in .NET can run in a process of its own. An engine in another
/// language implements the same messages; this file is their reference implementation. Upstream's <c>engine/protocol/server.ts</c>.
/// </summary>
/// <remarks>.NET-only holder name: C# has no free functions.</remarks>
public static class ProtocolServer
{
    /// <summary>
    /// Serves <paramref name="engine"/> over one connection until the client sends <c>exit</c> or the streams close. Completes once the
    /// connection is closed; a server process then exits. Defaults to the process's stdin and stdout, the transport a client's
    /// <see cref="SpawnTransport"/> opens: keep the engine's own logging on stderr. Upstream's <c>serveEngine</c>.
    /// </summary>
    public static Task ServeEngineAsync(EngineHandle engine, MessageStreams? streams = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return new EngineServer(engine, null, streams ?? ProcessStreams()).Closed;
    }

    /// <summary>
    /// <see cref="ServeEngineAsync(EngineHandle, MessageStreams?)"/> with a factory that builds the engine from the client's
    /// <c>initializationOptions</c>.
    /// </summary>
    public static Task ServeEngineAsync(Func<JsonNode?, Task<EngineHandle>> factory, MessageStreams? streams = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new EngineServer(null, factory, streams ?? ProcessStreams()).Closed;
    }

    private static MessageStreams ProcessStreams() => new(Console.OpenStandardInput(), Console.OpenStandardOutput());

    /// <summary>The attempt a worker connection runs, between <c>startAttempt</c> and <c>endAttempt</c>.</summary>
    private sealed record ServedAttempt(string AttemptId)
    {
        /// <summary>Ids of the signals first seen during the attempt, forgotten when it ends.</summary>
        public HashSet<int> Signals { get; } = [];
    }

    private sealed class EngineServer
    {
        /// <summary>The longest a closing connection waits for the engine's cancellation callbacks.</summary>
        private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(5);

        private readonly EngineHandle? _source;
        private readonly Func<JsonNode?, Task<EngineHandle>>? _factory;
        private readonly Connection _connection;
        private readonly ManualResetEventSlim _ready = new();
        private readonly SemaphoreSlim _initializing = new(1, 1);
        private readonly object _gate = new();
        private readonly Dictionary<int, CancellationTokenSource> _signals = [];

        /// <summary>The <c>derived</c> option of each <c>resolveSecret</c> in flight, by the id the client asks back with.</summary>
        private readonly Dictionary<int, Func<string, IReadOnlyList<string>>> _derivations = [];

        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _nextDerivation = 1;
        private EngineHandle? _engine;
        private ServedAttempt? _attempt;
        private volatile bool _shutdown;
        private bool _connectionClosed;

        public EngineServer(EngineHandle? engine, Func<JsonNode?, Task<EngineHandle>>? factory, MessageStreams streams)
        {
            _source = engine;
            _factory = factory;
            _connection = new Connection(
                streams,
                new ConnectionOptions
                {
                    EncodeError = cause => Protocol.EncodeError(cause),
                    OnClose = _ =>
                    {
                        // A client that vanished cannot abort what it started; one that shut down cleanly has nothing left running,
                        // and its signals stay as it left them.
                        CancellationTokenSource[] sources;
                        lock (_gate)
                        {
                            sources = _shutdown ? [] : [.. _signals.Values];
                            _signals.Clear();
                            _connectionClosed = true;
                        }

                        _ = CloseAsync(sources);
                    },
                    Requests = new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
                    {
                        ["initialize"] = InitializeAsync,
                        ["attempt/deriveSecret"] = DeriveSecretAsync,
                        ["shutdown"] = _ =>
                        {
                            _shutdown = true;
                            return Task.FromResult<JsonNode?>(null);
                        },
                    },
                    Notifications = new Dictionary<string, Action<JsonNode?>>
                    {
                        ["initialized"] = _ => { },
                        ["exit"] = _ => Link.Close("the client sent exit"),
                        ["$/abort"] = parameters =>
                        {
                            var abort = WireCodec.Read<AbortParams>(parameters);
                            CancellationTokenSource? source;
                            lock (_gate)
                            {
                                _signals.Remove(abort.Id, out source);
                            }

                            // A token carries no reason, so the client's reason does not cross: the engine sees a plain cancellation.
                            if (source is not null)
                            {
                                Abort(source);
                            }
                        },
                    },
                }
            );
            _ready.Set();
        }

        /// <summary>Settles once the connection is closed.</summary>
        public Task Closed => _closed.Task;

        /// <summary>
        /// The connection, once the constructor stored it: its read loop starts inside its own constructor, so the first message can be
        /// handled before the assignment returns.
        /// </summary>
        private Connection Link
        {
            get
            {
                _ready.Wait();
                return _connection;
            }
        }

        /// <summary>
        /// Cancels a source off the read loop: a callback that waits for a response only that loop can read would otherwise block it.
        /// Upstream's <c>controller.abort()</c>.
        /// </summary>
        /// <summary>
        /// Signals every source off the read loop, then completes <see cref="Closed"/> once the callbacks ran or <c>CloseWait</c> passed: a
        /// server process that exits on it must not do so before the engine's tokens were canceled, and a callback that never returns must not
        /// keep the server from ending.
        /// </summary>
        private async Task CloseAsync(CancellationTokenSource[] sources)
        {
            try
            {
                var canceled = Task.WhenAll(sources.Select(source => Task.Run(() => Cancel(source))));
                await Task.WhenAny(canceled, Task.Delay(CloseWait)).ConfigureAwait(false);
            }
            finally
            {
                _closed.TrySetResult();
            }
        }

        private static void Abort(CancellationTokenSource source) => _ = Task.Run(() => Cancel(source));

        private static void Cancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (Exception cause)
            {
                // A callback the engine registered threw: that is its failure, and it must not stop the server from closing. Stdout
                // carries the protocol, so it is reported on stderr.
                Console.Error.WriteLine($"an engine's cancellation callback threw: {cause.Message}");
            }
        }

        private static string Lower(string name) => char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>An operation's result as JSON: a snapshot's pixels as base64, a screenshot as the path the engine wrote.</summary>
        private static JsonNode? EncodeResult(OperationMethod spec, object? value) =>
            spec.Member switch
            {
                "observe" => WireCodec.Write(Protocol.EncodeSnapshot((EngineSnapshot)value!)),
                "state.capture" => EncodeState((EngineState)value!),
                "artifacts.screenshot" => new JsonObject { ["path"] = (string?)value },
                "artifacts.stopVideo" => WireCodec.EncodeVideoSegments((IReadOnlyList<VideoSegment>)value!),
                _ => value is null ? null : WireCodec.Write(value),
            };

        /// <summary>A captured state keeps its <c>data</c> member even when it is null: the state contract requires the field.</summary>
        private static JsonObject EncodeState(EngineState state)
        {
            var wire = new JsonObject
            {
                ["format"] = state.Format,
                ["version"] = state.Version,
                ["data"] = state.Data?.DeepClone(),
            };
            if (state.ExpiresAt is not null)
            {
                wire["expiresAt"] = state.ExpiresAt;
            }

            return wire;
        }

        private static async Task<object?> InvokeAsync(Delegate member, object?[] arguments)
        {
            // Hooks run off the read loop, so one that blocks cannot keep it from reading `$/abort` or a response.
            await Task.Yield();
            Task task;
            try
            {
                task = (Task)member.DynamicInvoke(arguments)!;
            }
            catch (TargetInvocationException cause) when (cause.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(cause.InnerException).Throw();
                throw;
            }

            await task.ConfigureAwait(false);
            // The declared result decides: a hook declared to return a bare Task has no result, whatever Task it happened to return.
            var declared = member.GetType().GetGenericArguments()[^1];
            return declared.IsGenericType && declared.GetGenericTypeDefinition() == typeof(Task<>)
                ? task.GetType().GetProperty("Result")!.GetValue(task)
                : null;
        }

        private static IReadOnlyDictionary<string, string?> ReadEnv(JsonNode? env) =>
            env is JsonObject obj
                ? obj.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null
                )
                : new Dictionary<string, string?>();

        private async Task<JsonNode?> InitializeAsync(JsonNode? parameters)
        {
            var version = parameters?["protocolVersion"];
            if (
                version is not JsonValue number
                || !number.TryGetValue<int>(out var asked)
                || asked != Protocol.EngineProtocolVersion
            )
            {
                throw new RpcException(
                    new RpcErrorObject(
                        RpcErrors.InvalidRequest,
                        $"this engine server speaks protocol {Protocol.EngineProtocolVersion}; the client asked for {version?.ToJsonString() ?? "none"}"
                    )
                );
            }

            await _initializing.WaitAsync().ConfigureAwait(false);
            try
            {
                await BuildEngineAsync(parameters).ConfigureAwait(false);
            }
            finally
            {
                _initializing.Release();
            }

            return new JsonObject
            {
                ["protocolVersion"] = Protocol.EngineProtocolVersion,
                ["manifest"] = WireCodec.Write(ProtocolManifest.DescribeEngine(_engine!)),
            };
        }

        private async Task BuildEngineAsync(JsonNode? parameters)
        {
            if (_engine is null)
            {
                var engine = _source
                    ?? await Task.Run(() => _factory!(parameters!["initializationOptions"])).ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "the engine factory must return an EngineManifest.DefineEngine handle"
                    );
                if (engine.SpiVersion != E2E.Engine.EngineManifest.SpiVersion)
                {
                    throw new InvalidOperationException($"engine \"{engine.Name}\" declares an unsupported spiVersion");
                }

                _engine = engine;
                Register(engine);
            }
        }

        private async Task<JsonNode?> DeriveSecretAsync(JsonNode? parameters)
        {
            var asked = WireCodec.Read<DeriveSecretParams>(parameters);
            Func<string, IReadOnlyList<string>>? derive;
            lock (_gate)
            {
                _derivations.TryGetValue(asked.Derivation, out derive);
            }

            if (derive is null)
            {
                throw new InvalidOperationException(
                    $"no secret is being resolved as derivation {asked.Derivation.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                );
            }

            // Off the reader: a derivation that waits on a device must not keep the connection from reading `$/abort` or its end.
            var values = await Task.Run(() => derive(asked.Plaintext)).ConfigureAwait(false);
            return new JsonObject { ["values"] = WireCodec.Write(values) };
        }

        /// <summary>
        /// Registers one request handler per method <paramref name="engine"/> answers. The attempt hooks are always answered: the server
        /// tracks the attempt for its callbacks and signals whether or not the engine has hooks of its own.
        /// </summary>
        private void Register(EngineHandle engine)
        {
            foreach (var method in Protocol.EngineMethods)
            {
                var spec = Protocol.Methods[method];
                var member = Protocol.EngineMember(
                    engine,
                    spec switch
                    {
                        OperationMethod operation => operation.Member,
                        LifecycleMethod lifecycle => lifecycle.Member,
                        _ => string.Empty,
                    }
                );
                if (member is null && !Protocol.ServedMethods.Contains(method))
                {
                    continue;
                }

                Link.OnRequest(
                    method,
                    spec switch
                    {
                        OperationMethod operation => async parameters =>
                            EncodeResult(
                                operation,
                                await InvokeAsync(member!, OperationArgs(operation, parameters)).ConfigureAwait(false)
                            ),
                        LifecycleMethod lifecycle => parameters => LifecycleAsync(lifecycle.Member, member, parameters),
                        _ => throw new InvalidOperationException($"unknown method kind for {method}"),
                    }
                );
            }
        }

        /// <summary>The member's arguments, in order, read off its params by name.</summary>
        private object?[] OperationArgs(OperationMethod spec, JsonNode? parameters) =>
            [.. spec.Args.Select(name => OperationArg(spec, name, parameters?[name]))];

        private object? OperationArg(OperationMethod spec, string name, JsonNode? value) =>
            name switch
            {
                "operation" => Operation(WireCodec.Read<WireOperation>(value)),
                "options" when spec.Member == "observe" => value is null ? null : WireCodec.Read<ObserveOptions>(value),
                "options" => value?["replace"] is { } replace && (bool)replace,
                "ref" => WireCodec.Read<NodeRef>(value),
                "properties" => WireCodec.Read<string[]>(value),
                "expression" => WireCodec.DecodeExpression(value),
                "action" when spec.Member == "perform" => WireCodec.DecodeLocatorAction(value),
                "action" => WireCodec.DecodePointerAction(value),
                "point" => WireCodec.Read<Point>(value),
                "state" => WireCodec.Read<EngineState>(value),
                "size" => WireCodec.Read<ViewportSize>(value),
                "index" => (int)value!,
                "label" => value is JsonValue text && text.TryGetValue<string>(out var label) ? label : null,
                _ => (string)value!,
            };

        private async Task<JsonNode?> LifecycleAsync(string member, Delegate? hook, JsonNode? parameters)
        {
            void Log(string line) => Link.Notify("engine/log", new JsonObject { ["line"] = line });

            switch (member)
            {
                case "prepare":
                    Signal(parameters);
                    await InvokeAsync(hook!, [Prepare(parameters, Log)]).ConfigureAwait(false);
                    return null;
                case "finish":
                    Signal(parameters);
                    await InvokeAsync(hook!, [Finish(parameters, Log)]).ConfigureAwait(false);
                    return null;
                case "doctor":
                    Signal(parameters);
                    var checks =
                        (IReadOnlyList<EngineDoctorCheck>)
                            (await InvokeAsync(hook!, [Doctor(parameters, Log)]).ConfigureAwait(false))!;
                    return WireCodec.Write(checks);
                case "init":
                    Signal(parameters);
                    await InvokeAsync(hook!, [Init(parameters, Log)]).ConfigureAwait(false);
                    return null;
                case "startAttempt":
                    var attemptId = WireText(parameters, "attemptId");
                    lock (_gate)
                    {
                        _attempt = new ServedAttempt(attemptId);
                    }

                    var context = AttemptContext(
                        attemptId,
                        WireText(parameters, "artifactsDir"),
                        Signal(parameters),
                        parameters?["screens"] is { } screens && (bool)screens
                    );
                    if (hook is not null)
                    {
                        await InvokeAsync(hook, [context]).ConfigureAwait(false);
                    }

                    return null;
                case "settleAttempt":
                    await InvokeAsync(hook!, [Cleanup(parameters)]).ConfigureAwait(false);
                    return null;
                case "endAttempt":
                    // A hook that outlived its budget settles after the next attempt started: it retires only the attempt it ended.
                    ServedAttempt? ending;
                    lock (_gate)
                    {
                        ending = _attempt;
                    }

                    try
                    {
                        if (hook is not null)
                        {
                            await InvokeAsync(hook, [Cleanup(parameters)]).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        lock (_gate)
                        {
                            foreach (var id in ending?.Signals ?? [])
                            {
                                _signals.Remove(id);
                            }

                            if (ReferenceEquals(_attempt, ending))
                            {
                                _attempt = null;
                            }
                        }
                    }

                    return null;
                case "dispose":
                    var reuse = parameters?["reuseSignal"] is { } wire ? WireCodec.Read<WireSignal>(wire) : null;
                    await InvokeAsync(hook!, [Dispose(parameters, reuse)]).ConfigureAwait(false);
                    return null;
                default:
                    throw new InvalidOperationException($"unknown lifecycle member {member}");
            }
        }

        private static string WireText(JsonNode? parameters, string name) =>
            parameters?[name] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : throw new JsonException($"\"{name}\" must be a string");

        private static bool Headed(JsonNode? parameters) => parameters?["headed"] is { } headed && (bool)headed;

        private static string? HostId(JsonNode? parameters) =>
            parameters?["hostId"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static EnginePrepareInfo Prepare(JsonNode? parameters, Action<string> log) =>
            new()
            {
                HostId = HostId(parameters),
                RunId = WireText(parameters, "runId"),
                TargetName = WireText(parameters, "targetName"),
                Slots = (int)parameters!["slots"]!,
                Env = ReadEnv(parameters["env"]),
                ProjectRoot =
                    parameters["projectRoot"] is JsonValue root && root.TryGetValue<string>(out var path) ? path : null,
                Headed = Headed(parameters),
                Log = log,
            };

        private static EngineFinishInfo Finish(JsonNode? parameters, Action<string> log) =>
            new()
            {
                HostId = HostId(parameters),
                RunId = WireText(parameters, "runId"),
                TargetName = WireText(parameters, "targetName"),
                Env = ReadEnv(parameters!["env"]),
                Log = log,
                CleanupTimeout = TimeSpan.FromMilliseconds((double)parameters["timeoutMs"]!),
            };

        private static EngineDoctorInfo Doctor(JsonNode? parameters, Action<string> log) =>
            new()
            {
                TargetName = WireText(parameters, "targetName"),
                ProjectRoot = WireText(parameters, "projectRoot"),
                App = WireCodec.Read<EngineAppInfo>(parameters!["app"]),
                Env = ReadEnv(parameters["env"]),
                Headed = Headed(parameters),
                Log = log,
            };

        private static EngineInitInfo Init(JsonNode? parameters, Action<string> log) =>
            new()
            {
                HostId = HostId(parameters),
                RunId = WireText(parameters, "runId"),
                TargetName = WireText(parameters, "targetName"),
                WorkerSlot = (int)parameters!["workerSlot"]!,
                Env = ReadEnv(parameters["env"]),
                Headed = Headed(parameters),
                Log = log,
            };

        /// <summary>The attempt context an engine's <c>startAttempt</c> receives, its callbacks routed to the client.</summary>
        private EngineAttemptContext AttemptContext(
            string attemptId,
            string artifactsDir,
            CancellationToken signal,
            bool screens
        )
        {
            void Send(string method, JsonObject parameters)
            {
                bool current;
                lock (_gate)
                {
                    current = _attempt?.AttemptId == attemptId;
                }

                if (current)
                {
                    parameters["attemptId"] = attemptId;
                    Link.Notify(method, parameters);
                }
            }

            return new EngineAttemptContext
            {
                AttemptId = attemptId,
                ArtifactsDir = artifactsDir,
                Signal = signal,
                ResolveSecret = async (secret, options) =>
                {
                    if (options?.Derived is not { } derived)
                    {
                        return await ResolveSecretAsync(attemptId, secret.Name, null).ConfigureAwait(false);
                    }

                    int derivation;
                    lock (_gate)
                    {
                        derivation = _nextDerivation++;
                        _derivations[derivation] = derived;
                    }

                    try
                    {
                        return await ResolveSecretAsync(attemptId, secret.Name, derivation).ConfigureAwait(false);
                    }
                    finally
                    {
                        lock (_gate)
                        {
                            _derivations.Remove(derivation);
                        }
                    }
                },
                AppLog = entry =>
                    Send(
                        "attempt/appLog",
                        new JsonObject
                        {
                            ["entry"] = new JsonObject
                            {
                                ["source"] = Lower(entry.Source.ToString()),
                                ["level"] = Lower(entry.Level.ToString()),
                                ["text"] = entry.Text,
                            },
                        }
                    ),
                Navigation = line => Send("attempt/navigation", new JsonObject { ["line"] = line }),
                Environment = facts =>
                    Send("attempt/environment", new JsonObject { ["facts"] = WireCodec.Write(facts) }),
                Screen = screens
                    ? snapshot =>
                        Send(
                            "attempt/screen",
                            new JsonObject { ["snapshot"] = WireCodec.Write(Protocol.EncodeSnapshot(snapshot)) }
                        )
                    : null,
            };
        }

        /// <summary>
        /// Resolves a secret on the client. With a <paramref name="derivation"/>, the client asks back for the forms the engine derives from
        /// the value (<c>attempt/deriveSecret</c>) and redacts them before it answers, so the provider runs once.
        /// </summary>
        private async Task<string> ResolveSecretAsync(string attemptId, string name, int? derivation)
        {
            var parameters = new JsonObject { ["attemptId"] = attemptId, ["name"] = name };
            if (derivation is { } id)
            {
                parameters["derivation"] = id;
            }

            var result = await CallAsync("attempt/resolveSecret", parameters).ConfigureAwait(false);
            return WireText(result, "value");
        }

        /// <summary>Calls the client, rethrowing its failure as the error the harness raised.</summary>
        private async Task<JsonNode?> CallAsync(string method, JsonNode parameters)
        {
            try
            {
                return await Link.RequestAsync(method, parameters).ConfigureAwait(false);
            }
            catch (RpcException cause)
            {
                throw Protocol.DecodeError(new RpcErrorObject(cause.Code, cause.Message, cause.Data));
            }
        }

        private OperationContext Operation(WireOperation wire) =>
            new(wire.Origin == "agent" ? OperationOrigin.Agent : OperationOrigin.Test)
            {
                Signal = Signal(wire.Signal),
                Timeout = TimeSpan.FromMilliseconds(wire.TimeoutMs),
                RunId = wire.RunId,
                AttemptId = wire.AttemptId,
            };

        private EngineDisposeContext Dispose(JsonNode? parameters, WireSignal? reuse)
        {
            var cleanup = Cleanup(parameters);
            return new EngineDisposeContext
            {
                CleanupTimeout = cleanup.Timeout,
                Signal = cleanup.Signal,
                ReuseSignal = reuse is null ? null : Signal(reuse),
            };
        }

        private EngineCleanupContext Cleanup(JsonNode? parameters)
        {
            var wire = WireCodec.Read<WireCleanup>(parameters);
            return new EngineCleanupContext(Signal(wire.Signal), TimeSpan.FromMilliseconds(wire.TimeoutMs));
        }

        private CancellationToken Signal(JsonNode? parameters) =>
            Signal(WireCodec.Read<WireSignal>(parameters?["signal"]));

        /// <summary>
        /// The local token for a wire signal: one per id, so an engine comparing tokens sees the client's identity. One first seen during an
        /// attempt is forgotten when the attempt ends; the rest live as long as the connection.
        /// </summary>
        private CancellationToken Signal(WireSignal wire)
        {
            if (wire.Aborted == true)
            {
                return new CancellationToken(true);
            }

            lock (_gate)
            {
                // A connection that closed already cannot abort a token minted now, so the hook gets a canceled one.
                if (_connectionClosed)
                {
                    return new CancellationToken(true);
                }

                if (!_signals.TryGetValue(wire.Id, out var source))
                {
                    source = new CancellationTokenSource();
                    _signals[wire.Id] = source;
                    _attempt?.Signals.Add(wire.Id);
                }

                return source.Token;
            }
        }
    }
}
