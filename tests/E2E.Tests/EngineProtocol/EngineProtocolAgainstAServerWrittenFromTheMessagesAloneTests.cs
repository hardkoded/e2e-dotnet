// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Engine.Protocol;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe('engine protocol against a server written from the messages alone')</c>
/// (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>): a server that speaks raw JSON-RPC, as one in another language would, with no
/// <see cref="ProtocolServer"/> and no engine handle.
/// </summary>
public sealed class EngineProtocolAgainstAServerWrittenFromTheMessagesAloneTests
{
    private static byte[] Frame(JsonNode message)
    {
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        return [.. Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), .. body];
    }

    private static byte[] Note(JsonNode? parameters) =>
        Frame(
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "note",
                ["params"] = parameters,
            }
        );

    [Fact]
    public async Task Rejects_a_request_answered_with_a_malformed_error_instead_of_throwing_out_of_the_stream()
    {
        var input = new Pipe();
        var output = new Pipe();
        using var connection = new Connection(new MessageStreams(input.Reader.AsStream(), output.Writer.AsStream()));

        var pending = connection.RequestAsync("engine/observe", new JsonObject());
        await input
            .Writer.AsStream()
            .WriteAsync(
                Frame(
                    new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = 1,
                        ["error"] = null,
                    }
                )
            );

        var failure = await Assert.ThrowsAsync<RpcException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(-32603, failure.Code);
        Assert.Contains("malformed error", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_message_that_arrives_a_byte_at_a_time_and_two_that_arrive_in_one_chunk()
    {
        var input = new Pipe();
        var output = new Pipe();
        var seen = new ConcurrentQueue<JsonNode?>();
        using var connection = new Connection(
            new MessageStreams(input.Reader.AsStream(), output.Writer.AsStream()),
            new ConnectionOptions
            {
                Notifications = new Dictionary<string, Action<JsonNode?>>
                {
                    ["note"] = parameters => seen.Enqueue(parameters),
                },
            }
        );
        var writer = input.Writer.AsStream();

        foreach (var value in Note("één"))
        {
            await writer.WriteAsync(new[] { value });
            await writer.FlushAsync();
        }

        await writer.WriteAsync(Note(2).Concat(Note(3)).ToArray());
        await writer.WriteAsync(Note(4));
        await writer.FlushAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (seen.Count < 4 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(
            ["\"één\"", "2", "3", "4"],
            seen.Select(value =>
                value!.ToJsonString(
                    new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
                )
            )
        );
        Assert.Null(connection.Closed);
    }

    /// <summary>Upstream's <c>handWritten</c>: a server of raw JSON-RPC, one connection per open, recording what reaches it.</summary>
    private sealed class HandWritten(
        IReadOnlyList<string> methods,
        IReadOnlyDictionary<string, Func<JsonNode?, Task<JsonNode?>>>? overrides = null
    ) : IEngineTransport
    {
        public ConcurrentQueue<string> Received { get; } = [];

        public string Label => "hand-written";

        public Task<ITransportConnection> OpenAsync(TransportContext context)
        {
            var toServer = new Pipe();
            var toClient = new Pipe();
            Connection? server = null;
            Func<JsonNode?, Task<JsonNode?>> Record(string method) =>
                parameters =>
                {
                    Received.Enqueue(method);
                    return Task.FromResult(parameters);
                };
            var requests = new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
            {
                ["initialize"] = _ =>
                    Task.FromResult<JsonNode?>(
                        new JsonObject
                        {
                            ["protocolVersion"] = 1,
                            ["manifest"] = new JsonObject
                            {
                                ["name"] = "hand",
                                ["version"] = "1",
                                ["spiVersion"] = 1,
                                ["methods"] = new JsonArray([.. methods.Select(method => (JsonNode)method)]),
                            },
                        }
                    ),
                ["shutdown"] = Record("shutdown"),
                ["lifecycle/startAttempt"] = parameters =>
                {
                    Received.Enqueue("lifecycle/startAttempt");
                    server!.Notify(
                        "attempt/environment",
                        new JsonObject
                        {
                            ["attemptId"] = parameters!["attemptId"]!.GetValue<string>(),
                            ["facts"] = new JsonObject { ["written"] = "by hand" },
                        }
                    );
                    return Task.FromResult<JsonNode?>(null);
                },
                ["engine/observe"] = _ => Task.FromResult<JsonNode?>(Observed()),
                ["artifacts/screenshot"] = _ => Task.FromResult<JsonNode?>(Png("png bytes")),
            };
            foreach (var (method, handler) in overrides ?? new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>())
            {
                requests[method] = handler;
            }

            server = new Connection(
                new MessageStreams(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
                new ConnectionOptions
                {
                    Requests = requests,
                    Notifications = new Dictionary<string, Action<JsonNode?>> { ["exit"] = _ => server!.Close("exit") },
                }
            );
            return Task.FromResult<ITransportConnection>(new Opened(toServer, toClient, Received));
        }

        public static JsonObject Observed() =>
            new()
            {
                ["root"] = new JsonObject
                {
                    ["ref"] = new JsonObject { ["id"] = "root", ["revision"] = "" },
                },
                ["viewport"] = new JsonObject { ["width"] = 1, ["height"] = 1 },
            };

        public static JsonObject Png(string bytes) =>
            new() { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(bytes)), ["mediaType"] = "image/png" };

        private sealed class Opened(Pipe toServer, Pipe toClient, ConcurrentQueue<string> received)
            : ITransportConnection
        {
            public Stream Input { get; } = toClient.Reader.AsStream();

            public Stream Output { get; } = toServer.Writer.AsStream();

            public Task CloseAsync(TimeSpan timeout)
            {
                received.Enqueue("transport closed");
                toServer.Writer.Complete();
                return Task.CompletedTask;
            }
        }
    }

    private static EngineHandle Connect(IEngineTransport transport, params string[] methods) =>
        ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = new E2E.Engine.Protocol.EngineManifest("hand", "1", 1, methods),
                Transport = transport,
            }
        );

    private static async Task<Exception?> FailureOf(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception cause)
        {
            return cause;
        }
    }

    private static string Scratch() => Directory.CreateTempSubdirectory("e2e-protocol-").FullName;

    [Fact]
    public async Task Closes_the_transport_when_a_server_ignores_its_dispose_budget()
    {
        var transport = new HandWritten(
            ["engine/observe", "lifecycle/dispose"],
            new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
            {
                ["lifecycle/dispose"] = _ => new TaskCompletionSource<JsonNode?>().Task,
            }
        );
        var remote = Connect(transport, "engine/observe", "lifecycle/dispose");
        await remote.Init!(InitInfo());

        var refused = await Assert.ThrowsAsync<EngineException>(() =>
            remote.Dispose!(DisposeContext(TimeSpan.FromMilliseconds(50)))
        );

        Assert.Equal("OPERATION_TIMEOUT", refused.Code);
        Assert.Contains("transport closed", transport.Received);
    }

    [Fact]
    public async Task Refuses_a_server_that_speaks_another_protocol_version()
    {
        var transport = new HandWritten(
            ["engine/observe"],
            new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
            {
                ["initialize"] = _ =>
                    Task.FromResult<JsonNode?>(
                        new JsonObject
                        {
                            ["protocolVersion"] = 999,
                            ["manifest"] = new JsonObject
                            {
                                ["name"] = "hand",
                                ["version"] = "1",
                                ["spiVersion"] = 1,
                                ["methods"] = new JsonArray("engine/observe"),
                            },
                        }
                    ),
            }
        );
        var remote = Connect(transport, "engine/observe");

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Init!(InitInfo()));

        Assert.Equal("ENGINE_FAILURE", refused.Code);
        Assert.Contains("protocol 999", refused.Message, StringComparison.Ordinal);
    }

    private sealed class MalformedNotifications : IEngineTransport
    {
        public string Label => "malformed";

        public Task<ITransportConnection> OpenAsync(TransportContext context)
        {
            var toServer = new Pipe();
            var toClient = new Pipe();
            Connection? server = null;
            server = new Connection(
                new MessageStreams(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
                new ConnectionOptions
                {
                    Requests = new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
                    {
                        ["initialize"] = _ =>
                        {
                            server!.Notify("engine/log");
                            server.Notify("engine/log", new JsonObject { ["line"] = "still here" });
                            return Task.FromResult<JsonNode?>(
                                new JsonObject
                                {
                                    ["protocolVersion"] = 1,
                                    ["manifest"] = new JsonObject
                                    {
                                        ["name"] = "bad",
                                        ["version"] = "1",
                                        ["spiVersion"] = 1,
                                        ["methods"] = new JsonArray("engine/observe"),
                                    },
                                }
                            );
                        },
                        ["engine/observe"] = _ => Task.FromResult<JsonNode?>(HandWritten.Observed()),
                    },
                }
            );
            return Task.FromResult<ITransportConnection>(new Closing(toServer, toClient));
        }

        private sealed class Closing(Pipe toServer, Pipe toClient) : ITransportConnection
        {
            public Stream Input { get; } = toClient.Reader.AsStream();

            public Stream Output { get; } = toServer.Writer.AsStream();

            public Task CloseAsync(TimeSpan timeout)
            {
                toServer.Writer.Complete();
                return Task.CompletedTask;
            }
        }
    }

    [Fact]
    public async Task Reports_a_malformed_notification_instead_of_crashing()
    {
        var logs = new ConcurrentQueue<string>();
        var remote = ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = new E2E.Engine.Protocol.EngineManifest("bad", "1", 1, ["engine/observe"]),
                Transport = new MalformedNotifications(),
            }
        );
        await remote.Init!(InitInfo(logs.Enqueue));
        await remote.Observe!(Operation(), null);

        Assert.Matches("^engine server malformed sent a malformed \"engine/log\" notification: ", logs.ToArray()[0]);
        Assert.Equal("still here", logs.ToArray()[1]);
        Assert.Equal(2, logs.Count);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Writes_a_screenshot_a_server_returned_as_bytes_into_the_attempt_directory()
    {
        var transport = new HandWritten(["engine/observe", "artifacts/screenshot", "lifecycle/startAttempt"]);
        var remote = Connect(transport, "engine/observe", "artifacts/screenshot", "lifecycle/startAttempt");
        var artifactsDir = Scratch();
        var facts = new ConcurrentQueue<Dictionary<string, string>>();
        try
        {
            await remote.Init!(InitInfo());
            await remote.StartAttempt!(
                Attempt(artifactsDir: artifactsDir, environment: entry =>
                    facts.Enqueue(new Dictionary<string, string>(entry))
                )
            );

            var relative = await remote.Artifacts!.Screenshot!("after login", Operation());

            Assert.Equal(Path.Combine("screenshots", "001-after_login.png"), relative);
            Assert.Equal("png bytes", await File.ReadAllTextAsync(Path.Combine(artifactsDir, relative)));
            Assert.Equal([new Dictionary<string, string> { ["written"] = "by hand" }], facts.ToArray());
            await remote.Dispose!(DisposeContext());
            Assert.Equal(["lifecycle/startAttempt", "shutdown", "transport closed"], transport.Received.ToArray());
        }
        finally
        {
            Directory.Delete(artifactsDir, true);
        }
    }

    [Fact]
    public async Task Writes_a_screenshots_bytes_into_the_attempt_directory_for_a_server_that_does_not_answer_startAttempt()
    {
        var remote = Connect(
            new HandWritten(["engine/observe", "artifacts/screenshot"]),
            "engine/observe",
            "artifacts/screenshot"
        );
        var artifactsDir = Scratch();
        try
        {
            await remote.Init!(InitInfo());
            await remote.StartAttempt!(Attempt(artifactsDir: artifactsDir));

            Assert.Equal(
                Path.Combine("screenshots", "001-screenshot.png"),
                await remote.Artifacts!.Screenshot!(null, Operation())
            );
            await remote.Dispose!(DisposeContext());
        }
        finally
        {
            Directory.Delete(artifactsDir, true);
        }
    }

    [Fact]
    public async Task Keeps_a_screenshot_answered_after_its_attempt_ended_out_of_the_next_attempts_directory()
    {
        string[] methods = ["engine/observe", "artifacts/screenshot", "lifecycle/startAttempt", "lifecycle/endAttempt"];
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = false;
        var transport = new HandWritten(
            methods,
            new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
            {
                ["lifecycle/endAttempt"] = _ => Task.FromResult<JsonNode?>(null),
                ["artifacts/screenshot"] = async _ =>
                {
                    asked = true;
                    await answer.Task;
                    return HandWritten.Png("late png bytes");
                },
            }
        );
        var remote = Connect(transport, methods);
        var first = Scratch();
        var second = Scratch();
        try
        {
            await remote.Init!(InitInfo());
            await remote.StartAttempt!(Attempt("attempt-1", first));
            var late = FailureOf(remote.Artifacts!.Screenshot!("late", Operation()));
            await WaitForAsync(() => asked);
            await remote.EndAttempt!(Cleanup);
            await remote.StartAttempt!(Attempt("attempt-2", second));
            answer.SetResult();

            var refused = await late;

            Assert.Empty(Directory.GetFileSystemEntries(second));
            Assert.Equal("INVALID_STATE", Assert.IsType<EngineException>(refused).Code);
            await remote.Dispose!(DisposeContext());
        }
        finally
        {
            Directory.Delete(first, true);
            Directory.Delete(second, true);
        }
    }

    [Fact]
    public async Task Keeps_a_screenshot_answered_while_its_attempt_is_ending_out_of_that_attempts_directory()
    {
        string[] methods = ["engine/observe", "artifacts/screenshot", "lifecycle/startAttempt", "lifecycle/endAttempt"];
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = false;
        var ending = false;
        var transport = new HandWritten(
            methods,
            new Dictionary<string, Func<JsonNode?, Task<JsonNode?>>>
            {
                ["lifecycle/endAttempt"] = async _ =>
                {
                    ending = true;
                    await ended.Task;
                    return null;
                },
                ["artifacts/screenshot"] = async _ =>
                {
                    asked = true;
                    await answer.Task;
                    return HandWritten.Png("late png bytes");
                },
            }
        );
        var remote = Connect(transport, methods);
        var artifactsDir = Scratch();
        try
        {
            await remote.Init!(InitInfo());
            await remote.StartAttempt!(Attempt(artifactsDir: artifactsDir));
            var late = FailureOf(remote.Artifacts!.Screenshot!("late", Operation()));
            await WaitForAsync(() => asked);
            var endingAttempt = remote.EndAttempt!(Cleanup);
            await WaitForAsync(() => ending);
            answer.SetResult();

            var refused = await late;

            Assert.Empty(Directory.GetFileSystemEntries(artifactsDir));
            Assert.Equal("INVALID_STATE", Assert.IsType<EngineException>(refused).Code);
            ended.SetResult();
            await endingAttempt;
            await remote.Dispose!(DisposeContext());
        }
        finally
        {
            Directory.Delete(artifactsDir, true);
        }
    }

    [Fact]
    public async Task Refuses_a_screenshot_path_outside_the_attempt_directory()
    {
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Artifacts = new EngineArtifactsSpec
                    {
                        Screenshot = (_, _) => Task.FromResult("../../elsewhere.png"),
                    },
                    StartAttempt = _ => Task.CompletedTask,
                }
            )
        );
        await remote.Init!(InitInfo());
        await remote.StartAttempt!(Attempt(artifactsDir: Path.GetTempPath()));

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Artifacts!.Screenshot!(null, Operation()));

        Assert.Equal("ENGINE_FAILURE", refused.Code);
        await remote.Dispose!(DisposeContext());
    }
}
