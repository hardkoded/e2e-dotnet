// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using E2E.Reports;

namespace E2E.Engine.Protocol;

/// <summary>A method that carries one engine operation: the member it calls and that member's arguments by name, in order. Upstream's <c>OperationMethod</c>.</summary>
/// <param name="Member">The engine member: a top-level one (<c>observe</c>) or a hook of a nested one (<c>keyboard.type</c>).</param>
/// <param name="Args">The member's arguments, named as the params carry them; <c>operation</c> is its operation context.</param>
/// <param name="Result">A result that is not plain JSON: a snapshot's pixels cross as base64, a screenshot may cross as its bytes.</param>
public sealed record OperationMethod(string Member, IReadOnlyList<string> Args, string? Result = null) : ProtocolMethod;

/// <summary>A method that carries one lifecycle hook, whose single argument is an info or cleanup object. Upstream's <c>LifecycleMethod</c>.</summary>
public sealed record LifecycleMethod(string Member) : ProtocolMethod;

/// <summary>One entry of the methods table. Upstream's <c>OperationMethod | LifecycleMethod</c>.</summary>
public abstract record ProtocolMethod
{
    private protected ProtocolMethod() { }
}

/// <summary>What a connection serves: a run's provisioning, a worker, or <c>e2e doctor</c>'s checks. Upstream's <c>ConnectionRole</c>.</summary>
public static class ConnectionRole
{
    public const string Run = "run";

    public const string Worker = "worker";

    public const string Doctor = "doctor";
}

/// <summary>Params of <c>initialize</c>. Upstream's <c>InitializeParams</c>.</summary>
public sealed record InitializeParams(
    int ProtocolVersion,
    ClientInfo ClientInfo,
    string Role,
    JsonNode? InitializationOptions = null
);

/// <summary>Who the client is, for <see cref="InitializeParams"/>.</summary>
public sealed record ClientInfo(string Name, string Version);

/// <summary>Result of <c>initialize</c>. Upstream's <c>InitializeResult</c>.</summary>
public sealed record InitializeResult(int ProtocolVersion, EngineManifest Manifest);

/// <summary>An abort signal on the wire. Upstream's <c>WireSignal</c>: the client mints one id per signal and sends <c>$/abort</c> once it aborts.</summary>
public sealed record WireSignal(int Id, bool? Aborted = null, RpcErrorObject? Reason = null);

/// <summary>An operation context on the wire. Upstream's <c>WireOperation</c>.</summary>
public sealed record WireOperation(WireSignal Signal, int TimeoutMs, string RunId, string AttemptId, string Origin);

/// <summary>An engine cleanup context on the wire. Upstream's <c>WireCleanup</c>.</summary>
public sealed record WireCleanup(WireSignal Signal, int TimeoutMs);

/// <summary>Observation pixels on the wire: <c>Data</c> is base64. Upstream's <c>WirePixels</c>.</summary>
public sealed record WirePixels(string Data, string MediaType, int Width, int Height, double Scale);

/// <summary>An engine snapshot on the wire: the snapshot with its pixels as base64. Upstream's <c>WireSnapshot</c>.</summary>
public sealed record WireSnapshot(EngineNode Root, ViewportSize Viewport)
{
    public string? Location { get; init; }

    public bool? KeyboardVisible { get; init; }

    public EngineTabs? Tabs { get; init; }

    public bool Truncated { get; init; }

    public bool TreeUnavailable { get; init; }

    public WirePixels? Pixels { get; init; }

    public int? MaskedRegionCount { get; init; }
}

/// <summary>One file of <see cref="WireSetInputFilePayloads"/>: <paramref name="Buffer"/> is the file bytes as base64.</summary>
public sealed record WireFilePayload(string Name, string MimeType, string Buffer);

/// <summary>Upstream's <c>WireLocatorAction</c> for <c>setInputFilePayloads</c>: file bytes, like snapshot pixels, cross as base64.</summary>
public sealed record WireSetInputFilePayloads(IReadOnlyList<WireFilePayload> Files);

/// <summary>Params of <c>attempt/deriveSecret</c>: the value the client resolved, for the forms the engine derives from it. Upstream's <c>DeriveSecretParams</c>.</summary>
public sealed record DeriveSecretParams(int Derivation, string Plaintext);

/// <summary>Params of <c>$/abort</c>. Upstream's <c>AbortParams</c>.</summary>
public sealed record AbortParams(int Id, RpcErrorObject? Reason = null);

/// <summary>
/// The engine protocol: the engine contract as messages, so an engine can run in another process, on another machine, or be written in
/// another language. It follows the Language Server Protocol: the base layer is LSP's (<see cref="Connection"/>), a connection opens
/// with <c>initialize</c>/<c>initialized</c> and ends with <c>shutdown</c>/<c>exit</c>, and the server announces what it answers in its
/// manifest. Each engine member is one request whose params are the member's arguments as JSON. Upstream's <c>engine/protocol/protocol.ts</c>.
/// </summary>
/// <remarks>.NET-only holder name: C# has no free functions.</remarks>
public static class Protocol
{
    /// <summary>The protocol version this runner speaks; <c>initialize</c> refuses any other. Upstream's <c>ENGINE_PROTOCOL_VERSION</c>.</summary>
    public const int EngineProtocolVersion = 1;

    /// <summary>The <see cref="Exception.Data"/> key a decoded error keeps the stack it crossed with under. .NET-only: a .NET stack trace is read-only.</summary>
    public const string RemoteStackKey = "e2e.remoteStack";

    /// <summary>How deep a cause chain crosses, as deep as a report describes one.</summary>
    private const int MaxWireCauses = 3;

    /// <summary>Characters of a stack that cross, as many as a report keeps.</summary>
    private const int MaxWireStack = 65_536;

    private static readonly JsonSerializerOptions DetailsOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Every engine method a server may answer, one per engine member: the one table the manifest, the client, and the server are all read
    /// off. Upstream's <c>METHODS</c>, in protocol order. The port has no <c>engine/viewportIntersection</c>, which upstream lists
    /// after <c>engine/readStyle</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, ProtocolMethod> Methods { get; } =
        new Dictionary<string, ProtocolMethod>(StringComparer.Ordinal)
        {
            ["engine/observe"] = Operation("observe", ["operation", "options"], "snapshot"),
            ["engine/readStyle"] = Operation("readStyle", ["ref", "properties", "operation"]),
            ["engine/locate"] = Operation("locate", ["expression", "operation"]),
            ["engine/perform"] = Operation("perform", ["ref", "action", "operation"]),
            ["engine/performAt"] = Operation("performAt", ["point", "action", "operation"]),
            ["keyboard/type"] = Operation("keyboard.type", ["text", "options", "operation"]),
            ["keyboard/press"] = Operation("keyboard.press", ["key", "operation"]),
            ["keyboard/dismiss"] = Operation("keyboard.dismiss", ["operation"]),
            ["state/capture"] = Operation("state.capture", ["operation"]),
            ["state/restore"] = Operation("state.restore", ["state", "operation"]),
            ["artifacts/screenshot"] = Operation("artifacts.screenshot", ["label", "operation"], "screenshot"),
            ["artifacts/startVideo"] = Operation("artifacts.startVideo", ["operation"]),
            ["artifacts/stopVideo"] = Operation("artifacts.stopVideo", ["operation"]),
            ["session/open"] = Operation("session.open", ["url", "operation"]),
            ["session/openLink"] = Operation("session.openLink", ["url", "operation"]),
            ["session/back"] = Operation("session.back", ["operation"]),
            ["session/resize"] = Operation("session.resize", ["size", "operation"]),
            ["session/switchTab"] = Operation("session.switchTab", ["index", "operation"]),
            ["session/closeTab"] = Operation("session.closeTab", ["index", "operation"]),
            ["session/restart"] = Operation("session.restart", ["operation"]),
            ["session/reset"] = Operation("session.reset", ["operation"]),
            ["lifecycle/prepare"] = new LifecycleMethod("prepare"),
            ["lifecycle/finish"] = new LifecycleMethod("finish"),
            ["lifecycle/doctor"] = new LifecycleMethod("doctor"),
            ["lifecycle/init"] = new LifecycleMethod("init"),
            ["lifecycle/startAttempt"] = new LifecycleMethod("startAttempt"),
            ["lifecycle/settleAttempt"] = new LifecycleMethod("settleAttempt"),
            ["lifecycle/endAttempt"] = new LifecycleMethod("endAttempt"),
            ["lifecycle/dispose"] = new LifecycleMethod("dispose"),
        };

    /// <summary>Every engine method a server may answer, in protocol order. Upstream's <c>ENGINE_METHODS</c>.</summary>
    public static IReadOnlyList<string> EngineMethods { get; } = [.. Methods.Keys];

    /// <summary>Methods <c>serveEngine</c> answers for any engine: it tracks the attempt whether or not the engine has hooks for it. Upstream's <c>SERVED_METHODS</c>.</summary>
    public static IReadOnlySet<string> ServedMethods { get; } =
        new HashSet<string>(["lifecycle/startAttempt", "lifecycle/endAttempt"], StringComparer.Ordinal);

    /// <summary>
    /// An engine member's function, read off a <c>member</c> path of <see cref="Methods"/>; null when the engine lacks it. Upstream's
    /// <c>engineMember</c>. The port's engine has no <c>viewportIntersection</c>.
    /// </summary>
    public static Delegate? EngineMember(EngineHandle engine, string member)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return member switch
        {
            "observe" => engine.Observe,
            "readStyle" => engine.ReadStyle,
            "locate" => engine.Locate,
            "perform" => engine.Perform,
            "performAt" => engine.PerformAt,
            "keyboard.type" => engine.Keyboard?.Type,
            "keyboard.press" => engine.Keyboard?.Press,
            "keyboard.dismiss" => engine.Keyboard?.Dismiss,
            "state.capture" => engine.State?.Capture,
            "state.restore" => engine.State?.Restore,
            "artifacts.screenshot" => engine.Artifacts?.Screenshot,
            "artifacts.startVideo" => engine.Artifacts?.StartVideo,
            "artifacts.stopVideo" => engine.Artifacts?.StopVideo,
            "session.open" => engine.Session?.Open,
            "session.openLink" => engine.Session?.OpenLink,
            "session.back" => engine.Session?.Back,
            "session.resize" => engine.Session?.Resize,
            "session.switchTab" => engine.Session?.SwitchTab,
            "session.closeTab" => engine.Session?.CloseTab,
            "session.restart" => engine.Session?.Restart,
            "session.reset" => engine.Session?.Reset,
            "prepare" => engine.Prepare,
            "finish" => engine.Finish,
            "doctor" => engine.Doctor,
            "init" => engine.Init,
            "startAttempt" => engine.StartAttempt,
            "settleAttempt" => engine.SettleAttempt,
            "endAttempt" => engine.EndAttempt,
            "dispose" => engine.Dispose,
            _ => null,
        };
    }

    /// <summary>
    /// Encodes file payload bytes without changing other locator actions. Returns a <see cref="WireSetInputFilePayloads"/> for
    /// <see cref="LocatorAction.SetInputFilePayloads"/> and the same action instance for any other kind: upstream's
    /// <c>WireLocatorAction</c> is a union of the two.
    /// </summary>
    public static object EncodeLocatorAction(LocatorAction action) =>
        action is LocatorAction.SetInputFilePayloads payloads
            ? new WireSetInputFilePayloads([
                .. payloads.Files.Select(file => new WireFilePayload(
                    file.Name,
                    file.MimeType,
                    Convert.ToBase64String(file.Buffer)
                )),
            ])
            : action;

    /// <summary>Restores file payload bytes at the engine protocol boundary. Upstream's <c>decodeLocatorAction</c>.</summary>
    public static LocatorAction DecodeLocatorAction(object action) =>
        action switch
        {
            WireSetInputFilePayloads wire => new LocatorAction.SetInputFilePayloads([
                .. wire.Files.Select(file => new FilePayload(
                    file.Name,
                    file.MimeType,
                    Convert.FromBase64String(file.Buffer)
                )),
            ]),
            LocatorAction other => other,
            _ => throw new ArgumentException($"Not a wire locator action: {action.GetType().Name}", nameof(action)),
        };

    /// <summary>
    /// Encodes an abort reason, or null for the platform's own cancellation, which the receiving side reproduces by aborting without a
    /// reason. Upstream's <c>encodeAbortReason</c>, whose platform error is <c>AbortError</c>.
    /// </summary>
    public static RpcErrorObject? EncodeAbortReason(Exception? reason) =>
        reason is null or OperationCanceledException ? null : EncodeError(reason);

    /// <summary>Encodes a snapshot for the wire. Upstream's <c>encodeSnapshot</c>.</summary>
    public static WireSnapshot EncodeSnapshot(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new WireSnapshot(snapshot.Root, snapshot.Viewport)
        {
            Location = snapshot.Location,
            KeyboardVisible = snapshot.KeyboardVisible,
            Tabs = snapshot.Tabs,
            Truncated = snapshot.Truncated,
            TreeUnavailable = snapshot.TreeUnavailable,
            MaskedRegionCount = snapshot.MaskedRegionCount,
            Pixels = snapshot.Pixels is { } pixels
                ? new WirePixels(
                    Convert.ToBase64String(pixels.Data),
                    pixels.MediaType,
                    pixels.Width,
                    pixels.Height,
                    pixels.Scale
                )
                : null,
        };
    }

    /// <summary>Decodes a snapshot off the wire. Upstream's <c>decodeSnapshot</c>.</summary>
    public static EngineSnapshot DecodeSnapshot(WireSnapshot wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return new EngineSnapshot(wire.Root, wire.Viewport)
        {
            Location = wire.Location,
            KeyboardVisible = wire.KeyboardVisible,
            Tabs = wire.Tabs,
            Truncated = wire.Truncated,
            TreeUnavailable = wire.TreeUnavailable,
            MaskedRegionCount = wire.MaskedRegionCount,
            Pixels = wire.Pixels is { } pixels
                ? new ObservationPixels(
                    Convert.FromBase64String(pixels.Data),
                    pixels.MediaType,
                    pixels.Width,
                    pixels.Height,
                    pixels.Scale
                )
                : null,
        };
    }

    /// <summary>
    /// Encodes what an engine member threw. An <see cref="EngineException"/> and a harness error keep their code and category, so the
    /// harness classifies a remote failure exactly as a local one; anything else crosses as its message, which the harness treats as an
    /// infrastructure failure, as it does locally. Every error keeps its stack and its first causes. Upstream's <c>encodeError</c>.
    /// </summary>
    public static RpcErrorObject EncodeError(Exception cause, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(cause);
        var trace = EncodeTrace(cause, depth);
        if (cause is EngineException { Mapped: false } engine && EngineErrorCodes.All.Contains(engine.Code))
        {
            trace["type"] = "engine";
            trace["code"] = engine.Code;
            trace["retryable"] = engine.Retryable;
            return new RpcErrorObject(RpcErrors.EngineError, cause.Message, trace);
        }

        if (cause is E2EException e2e)
        {
            var (category, code, _, retryable) = ErrorSerializer.Classify(e2e);
            trace["type"] = "e2e";
            trace["category"] = category;
            trace["code"] = e2e.Code;
            trace["retryable"] = e2e is EngineException engineError ? engineError.Retryable : retryable;
            if (e2e.Details is { } details)
            {
                trace["details"] = JsonSerializer.SerializeToNode(details, DetailsOptions);
            }

            return new RpcErrorObject(RpcErrors.EngineError, cause.Message, trace);
        }

        return new RpcErrorObject(RpcErrors.InternalError, cause.Message, trace.Count == 0 ? null : trace);
    }

    /// <summary>Decodes an error response into the error the engine member threw, with the causes it crossed with. Upstream's <c>decodeError</c>.</summary>
    public static Exception DecodeError(RpcErrorObject error, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(error);
        Exception? inner = null;
        string? stack = null;
        if (error.Data is JsonObject data)
        {
            stack = data["stack"] is JsonValue text && text.TryGetValue<string>(out var s) ? s : null;
            if (depth < MaxWireCauses && RpcErrorObject.IsRpcErrorObject(data["cause"], out var cause))
            {
                inner = DecodeError(cause, depth + 1);
            }
        }

        var decoded = DecodeClassified(error, inner);
        if (stack is not null)
        {
            // .NET cannot assign a stack trace, so the engine's own stack rides in Data for a report to show.
            decoded.Data[RemoteStackKey] = stack;
        }

        return decoded;
    }

    private static OperationMethod Operation(string member, IReadOnlyList<string> args, string? result = null) =>
        new(member, args, result);

    private static JsonObject EncodeTrace(Exception cause, int depth)
    {
        var trace = new JsonObject();
        if (cause.StackTrace is { } stack)
        {
            trace["stack"] = stack.Length > MaxWireStack ? stack[..MaxWireStack] : stack;
        }

        if (cause.InnerException is { } inner && depth < MaxWireCauses)
        {
            var encoded = EncodeError(inner, depth + 1);
            var wire = new JsonObject { ["code"] = encoded.Code, ["message"] = encoded.Message };
            if (encoded.Data is not null)
            {
                wire["data"] = encoded.Data.DeepClone();
            }

            trace["cause"] = wire;
        }

        return trace;
    }

    /// <summary>The e2e error in a response's <c>data</c>, or null for anything off the wire format, which then decodes as a plain error.</summary>
    private static JsonObject? WireErrorData(JsonNode? data)
    {
        if (
            data is not JsonObject obj
            || obj["retryable"] is not JsonValue retryable
            || !retryable.TryGetValue<bool>(out _)
        )
        {
            return null;
        }

        var type = Text(obj["type"]);
        var code = Text(obj["code"]);
        return type switch
        {
            "engine" when code is not null && EngineErrorCodes.All.Contains(code) => obj,
            "e2e"
                when code is not null
                    && Text(obj["category"]) is "test" or "configuration" or "infrastructure" or "interrupted" => obj,
            _ => null,
        };
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static Exception DecodeClassified(RpcErrorObject error, Exception? inner)
    {
        var data = error.Code == RpcErrors.EngineError ? WireErrorData(error.Data) : null;
        if (data is null)
        {
            return inner is null ? new Exception(error.Message) : new Exception(error.Message, inner);
        }

        var code = Text(data["code"])!;
        var retryable = data["retryable"]!.GetValue<bool>();
        if (Text(data["type"]) == "engine")
        {
            return new EngineException(code, error.Message, retryable, inner);
        }

        var details = data["details"] is { } wireDetails ? wireDetails.Deserialize<ErrorDetails>(DetailsOptions) : null;
        switch (Text(data["category"]))
        {
            case "test":
                return inner is null
                    ? new TestException(code, error.Message) { Details = details }
                    : new TestException(code, error.Message, inner) { Details = details };
            case "configuration":
                return inner is null
                    ? new ConfigurationException(code, error.Message) { Details = details }
                    : new ConfigurationException(code, error.Message, inner) { Details = details };
            case "infrastructure":
                return retryable && EngineErrorCodes.IsRetryable(code)
                        ? new EngineException(code, error.Message, true, inner) { Details = details, Mapped = true }
                    : inner is null ? new EngineException(code, error.Message) { Details = details, Mapped = true }
                    : new EngineException(code, error.Message, inner) { Details = details, Mapped = true };
            default:
                return inner is null
                    ? new E2EException(code, error.Message) { Details = details }
                    : new E2EException(code, error.Message, inner) { Details = details };
        }
    }
}
