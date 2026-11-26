// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Engine.Protocol;
using E2E.Reports;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe('engine protocol')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>), whose <c>it</c> run in this process
/// over <see cref="OverLoopback.Of"/>. Not ported: <c>carries the abort reason, so an engine rethrowing it raises what it would in process</c> (a
/// <see cref="CancellationToken"/> carries no reason). The <c>Partial_</c> tests check what the port has of an <c>it</c> whose whole needs a feature
/// it lacks; the nested <c>describe</c> blocks are <see cref="LaunchArgumentsTests"/> and <see cref="PermissionsAllTests"/>.
/// </summary>
public sealed class EngineProtocolTests
{
    private static readonly JsonSchemaCheck Validate = JsonSchemaCheck.FromFile("engine-manifest-v1.schema.json");

    [Fact]
    public void Encodes_only_wire_payload_bytes_and_keeps_ordinary_path_actions_unchanged()
    {
        // The port's payload is a byte[]: there is no subarray view, so the bytes are the three the upstream view selects.
        byte[] bytes = [0, 128, 255];
        var action = new LocatorAction.SetInputFilePayloads([
            new FilePayload("a.bin", "application/octet-stream", bytes),
            new FilePayload("empty.bin", "application/octet-stream", []),
        ]);

        var wire = Assert.IsType<WireSetInputFilePayloads>(Protocol.EncodeLocatorAction(action));

        Assert.Equal(
            [
                new WireFilePayload("a.bin", "application/octet-stream", "AID/"),
                new WireFilePayload("empty.bin", "application/octet-stream", ""),
            ],
            wire.Files
        );
        var overTheWire = JsonSerializer.Deserialize<WireSetInputFilePayloads>(JsonSerializer.Serialize(wire))!;
        var decoded = Assert.IsType<LocatorAction.SetInputFilePayloads>(Protocol.DecodeLocatorAction(overTheWire));
        Assert.Equal(action.Files.Count, decoded.Files.Count);
        for (var i = 0; i < action.Files.Count; i++)
        {
            Assert.Equal(action.Files[i].Name, decoded.Files[i].Name);
            Assert.Equal(action.Files[i].MimeType, decoded.Files[i].MimeType);
            Assert.Equal(action.Files[i].Buffer, decoded.Files[i].Buffer);
        }

        var paths = new LocatorAction.SetInputFiles(["a.bin"]);
        Assert.Same(paths, Protocol.EncodeLocatorAction(paths));
        Assert.Same(paths, Protocol.DecodeLocatorAction(paths));
    }

    [Fact]
    public async Task Advertises_multiTap_and_carries_its_count_and_modifiers_over_JSON_RPC_unchanged()
    {
        var performed = new ConcurrentQueue<LocatorAction>();
        Func<NodeRef, LocatorAction, OperationContext, Task> perform = (_, action, _) =>
        {
            performed.Enqueue(action);
            return Task.CompletedTask;
        };
        var (_, remote) = Of(Toy(toy => toy with { Actions = ["tap", "multiTap"], Perform = perform }));
        Assert.Contains("multiTap", remote.Actions!);
        await remote.Init!(InitInfo());
        var action = new LocatorAction.MultiTap(3, [KeyModifier.Shift]);

        await remote.Perform!(new NodeRef("root", "1"), action, Operation());

        var received = Assert.IsType<LocatorAction.MultiTap>(Assert.Single(performed));
        Assert.Equal(3, received.Count);
        Assert.Equal([KeyModifier.Shift], received.Modifiers);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Carries_a_read_request_and_computed_values_through_JSON_RPC()
    {
        var calls = new ConcurrentQueue<(NodeRef Ref, IReadOnlyList<string> Properties, OperationContext Operation)>();
        Func<NodeRef, IReadOnlyList<string>, OperationContext, Task<IReadOnlyDictionary<string, string>>> read = (
            reference,
            properties,
            operation
        ) =>
        {
            calls.Enqueue((reference, properties, operation));
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["color"] = "rgb(255, 0, 0)" }
            );
        };
        var (_, remote) = Of(Toy(toy => toy with { ReadStyle = read }));
        Assert.Contains("computed-style", remote.Capabilities);
        await remote.Init!(InitInfo());
        var reference = new NodeRef("n1", "1");

        var values = await remote.ReadStyle!(reference, ["color"], Operation());

        Assert.Equal(new Dictionary<string, string> { ["color"] = "rgb(255, 0, 0)" }, values);
        var call = Assert.Single(calls);
        Assert.Equal(reference, call.Ref);
        Assert.Equal(["color"], call.Properties);
        Assert.Equal(OperationOrigin.Test, call.Operation.Origin);
        await remote.Dispose!(DisposeContext());
        var refused = Assert.Throws<ConfigurationException>(() =>
            E2E.Engine.EngineManifest.DefineEngine(
                new EngineSpec
                {
                    Name = "bad",
                    Version = "1",
                    SpiVersion = 1,
                    ReadStyle = read,
                }
            )
        );
        Assert.Contains("readStyle without observe", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Declares_the_same_capabilities_over_the_protocol_as_the_engine_itself()
    {
        var (local, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap", "fill"],
                    Perform = (_, _, _) => Task.CompletedTask,
                    Keyboard = new EngineKeyboardSpec
                    {
                        Type = (_, _, _) => Task.CompletedTask,
                        Press = (_, _) => Task.CompletedTask,
                    },
                    Session = new EngineSessionSpec
                    {
                        Open = (_, _) => Task.CompletedTask,
                        Reset = _ => Task.CompletedTask,
                    },
                }
            )
        );

        Assert.Equal(
            local.Capabilities.Order(StringComparer.Ordinal),
            remote.Capabilities.Order(StringComparer.Ordinal)
        );
        Assert.Equal(["tap", "fill"], remote.Actions);
        Assert.NotNull(remote.Session!.Open);
        Assert.NotNull(remote.Session.Reset);
        Assert.Null(remote.Session.Back);
        Assert.Null(remote.Session.Restart);
    }

    [Fact]
    public async Task Negotiates_payload_uploads_and_preserves_their_bytes_over_JSON_RPC()
    {
        var performed = new ConcurrentQueue<LocatorAction>();
        Func<NodeRef, LocatorAction, OperationContext, Task> perform = (_, action, _) =>
        {
            performed.Enqueue(action);
            return Task.CompletedTask;
        };
        var (local, remote) = Of(Toy(toy => toy with { Actions = EngineActionKinds.KnownLocator, Perform = perform }));
        Assert.Contains("setInputFilePayloads", remote.Actions!);
        await remote.Init!(InitInfo());
        var action = new LocatorAction.SetInputFilePayloads([
            new FilePayload("binary.bin", "application/octet-stream", [0, 128, 255]),
        ]);

        await local.Perform!(new NodeRef("root", "1"), action, Operation());

        Assert.Same(action, Assert.Single(performed));
        performed.Clear();
        await remote.Perform!(new NodeRef("root", "1"), action, Operation());
        var received = Assert.IsType<LocatorAction.SetInputFilePayloads>(Assert.Single(performed));
        var file = Assert.Single(received.Files);
        Assert.Equal("binary.bin", file.Name);
        Assert.Equal("application/octet-stream", file.MimeType);
        Assert.Equal(new byte[] { 0, 128, 255 }, file.Buffer);
        await remote.Dispose!(DisposeContext());
        var legacy = Of(Toy(toy => toy with { Actions = EngineActionKinds.Locator, Perform = perform }));
        Assert.DoesNotContain("setInputFilePayloads", legacy.Remote.Actions!);
    }

    [Fact]
    public async Task Carries_a_snapshot_with_its_pixels_byte_for_byte()
    {
        byte[] data = [0x89, 0x50, 0x4e, 0x47, 0, 255, 7];
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Observe = (_, _) =>
                        Task.FromResult(
                            new EngineSnapshot(Root, new ViewportSize { Width = 10, Height = 10 })
                            {
                                Pixels = new ObservationPixels(data, "image/png", 20, 20, 2),
                            }
                        ),
                }
            )
        );
        await remote.Init!(InitInfo());

        var snapshot = await remote.Observe!(Operation(), null);

        Assert.Equal(TreeIds(Root), TreeIds(snapshot.Root));
        Assert.Equal(data, snapshot.Pixels!.Data);
        Assert.Equal(2, snapshot.Pixels.Scale);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Rethrows_an_engine_error_with_its_code_and_retryability_and_a_harness_error_with_its_category()
    {
        Exception? throwing = null;
        var (_, remote) = Of(
            Toy(toy => toy with { Actions = ["tap"], Perform = (_, _, _) => Task.FromException(throwing!) })
        );
        await remote.Init!(InitInfo());
        var reference = new NodeRef("n1", "");

        throwing = new EngineException("NODE_STALE", "gone", true);
        var stale = await Assert.ThrowsAsync<EngineException>(() =>
            remote.Perform!(reference, new LocatorAction.Tap(), Operation())
        );
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        Assert.Equal("gone", stale.Message);

        throwing = new TestException("ACTION_FAILED", "blocked") { Details = new ErrorDetails { Locator = "x" } };
        var blocked = await Assert.ThrowsAsync<TestException>(() =>
            remote.Perform!(reference, new LocatorAction.Tap(), Operation())
        );
        Assert.Equal("ACTION_FAILED", blocked.Code);
        Assert.Equal("x", blocked.Details!.Locator);

        throwing = new Exception("plain");
        var plain = await Assert.ThrowsAnyAsync<Exception>(() =>
            remote.Perform!(reference, new LocatorAction.Tap(), Operation())
        );
        Assert.IsType<Exception>(plain);
        Assert.Equal("plain", plain.Message);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Keeps_a_thrown_errors_stack_and_causes_across_the_protocol()
    {
        Exception thrown = null!;
        Exception inner = null!;
        try
        {
            try
            {
                var refused = new InvalidOperationException("connect ECONNREFUSED");
                refused.Data[Protocol.ErrnoKey] = "ECONNREFUSED";
                throw refused;
            }
            catch (Exception cause)
            {
                inner = cause;
                throw new InvalidOperationException("fetch failed", cause);
            }
        }
        catch (Exception cause)
        {
            thrown = cause;
        }

        var (_, remote) = Of(
            Toy(toy => toy with { Actions = ["tap"], Perform = (_, _, _) => Task.FromException(thrown) })
        );
        await remote.Init!(InitInfo());

        var received = await Assert.ThrowsAnyAsync<Exception>(() =>
            remote.Perform!(new NodeRef("n1", ""), new LocatorAction.Tap(), Operation())
        );

        Assert.Equal(thrown.StackTrace, received.Data[Protocol.RemoteStackKey]);
        Assert.Equal("connect ECONNREFUSED", received.InnerException!.Message);
        Assert.Equal("ECONNREFUSED", received.InnerException.Data[Protocol.ErrnoKey]);
        Assert.Equal(inner.StackTrace, received.InnerException.Data[Protocol.RemoteStackKey]);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Detaches_from_a_callers_signal_once_the_connection_closes()
    {
        using var live = new CancellationTokenSource();
        var (_, remote) = Of(Toy());
        for (var cycle = 0; cycle < 3; cycle += 1)
        {
            await remote.Init!(InitInfo(signal: live.Token));
            await remote.Observe!(Operation(live.Token), null);
            await remote.Dispose!(DisposeContext());
        }

        Assert.Equal(0, SignalEncoder.GetEventListeners(live.Token));
    }

    [Fact]
    public async Task Aborts_the_server_side_signal_when_the_client_aborts_including_one_already_aborted()
    {
        var seen = new ConcurrentQueue<CancellationToken>();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap"],
                    Perform = async (_, _, context) =>
                    {
                        seen.Enqueue(context.Signal);
                        if (context.Signal.IsCancellationRequested)
                        {
                            return;
                        }

                        waiting.TrySetResult();
                        await WhenCanceled(context.Signal);
                    },
                }
            )
        );
        await remote.Init!(InitInfo());
        using var controller = new CancellationTokenSource();
        var pending = remote.Perform!(new NodeRef("n1", ""), new LocatorAction.Tap(), Operation(controller.Token));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        controller.Cancel();
        await pending;
        Assert.True(seen.ToArray()[0].IsCancellationRequested);

        await remote.Perform!(new NodeRef("n1", ""), new LocatorAction.Tap(), Operation(controller.Token));

        Assert.True(seen.ToArray()[1].IsCancellationRequested);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Refuses_a_call_before_init_instead_of_opening_a_connection_on_its_own()
    {
        var (_, remote) = Of(Toy());

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Observe!(Operation(), null));

        Assert.Equal("INVALID_STATE", refused.Code);
    }

    [Fact]
    public async Task Serves_a_second_init_on_its_own_connection_and_disposes_the_oldest_first_as_a_handover_between_sessions_does()
    {
        var slots = new ConcurrentQueue<int>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Init = info =>
                    {
                        slots.Enqueue(info.WorkerSlot);
                        return Task.CompletedTask;
                    },
                    Dispose = _ => Task.CompletedTask,
                }
            )
        );
        await remote.Init!(InitInfo());
        await remote.Init!(InitInfo(workerSlot: 1));
        await remote.Dispose!(DisposeContext());
        Assert.Equal(TreeIds(Root), TreeIds((await remote.Observe!(Operation(), null)).Root));
        await remote.Dispose!(DisposeContext());

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Observe!(Operation(), null));

        Assert.Equal("INVALID_STATE", refused.Code);
        Assert.Equal([0, 1], slots.ToArray());
    }

    [Fact]
    public async Task Keeps_one_signal_per_id_within_an_attempt_and_forgets_the_attempts_signals_when_it_ends()
    {
        var seen = new ConcurrentQueue<CancellationToken>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap"],
                    Perform = (_, _, context) =>
                    {
                        seen.Enqueue(context.Signal);
                        return Task.CompletedTask;
                    },
                    StartAttempt = _ => Task.CompletedTask,
                    EndAttempt = _ => Task.CompletedTask,
                }
            )
        );
        using var source = new CancellationTokenSource();
        var signal = source.Token;
        var reference = new NodeRef("n1", "");
        await remote.Init!(InitInfo());
        await remote.StartAttempt!(Attempt());
        await remote.Perform!(reference, new LocatorAction.Tap(), Operation(signal));
        await remote.Perform!(reference, new LocatorAction.Tap(), Operation(signal));
        await remote.EndAttempt!(Cleanup);
        await remote.StartAttempt!(Attempt("attempt-2"));
        await remote.Perform!(reference, new LocatorAction.Tap(), Operation(signal));
        await remote.EndAttempt!(Cleanup);

        var tokens = seen.ToArray();
        Assert.Equal(tokens[0], tokens[1]);
        Assert.NotEqual(tokens[0], tokens[2]);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Forgets_an_attempts_signals_for_an_engine_without_attempt_hooks()
    {
        var seen = new ConcurrentQueue<CancellationToken>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap"],
                    Perform = (_, _, context) =>
                    {
                        seen.Enqueue(context.Signal);
                        return Task.CompletedTask;
                    },
                }
            )
        );
        using var source = new CancellationTokenSource();
        var signal = source.Token;
        var reference = new NodeRef("n1", "");
        await remote.Init!(InitInfo());
        await remote.StartAttempt!(Attempt());
        await remote.Perform!(reference, new LocatorAction.Tap(), Operation(signal));
        await remote.EndAttempt!(Cleanup);
        await remote.StartAttempt!(Attempt("attempt-2"));
        await remote.Perform!(reference, new LocatorAction.Tap(), Operation(signal));
        await remote.EndAttempt!(Cleanup);

        var tokens = seen.ToArray();
        Assert.NotEqual(tokens[0], tokens[1]);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Keeps_the_next_attempt_when_the_previous_attempts_endAttempt_settles_late()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = false;
        EngineAttemptContext? current = null;
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap"],
                    Perform = (_, _, _) =>
                    {
                        current?.Environment(new Dictionary<string, string> { ["attempt"] = current.AttemptId });
                        return Task.CompletedTask;
                    },
                    StartAttempt = context =>
                    {
                        current = context;
                        return Task.CompletedTask;
                    },
                    EndAttempt = async _ =>
                    {
                        if (current?.AttemptId == "attempt-1")
                        {
                            waiting = true;
                            await release.Task;
                        }
                    },
                }
            )
        );
        var facts = new ConcurrentQueue<Dictionary<string, string>>();
        EngineAttemptContext Started(string attemptId) =>
            Attempt(attemptId, environment: entry => facts.Enqueue(new Dictionary<string, string>(entry)));
        await remote.Init!(InitInfo());
        await remote.StartAttempt!(Started("attempt-1"));
        var late = remote.EndAttempt!(Cleanup);
        await WaitForAsync(() => waiting);
        await remote.StartAttempt!(Started("attempt-2"));
        release.SetResult();
        await late;

        await remote.Perform!(new NodeRef("n1", ""), new LocatorAction.Tap(), Operation());

        Assert.Equal([new Dictionary<string, string> { ["attempt"] = "attempt-2" }], facts.ToArray());
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Prepares_and_finishes_each_target_that_shares_one_handle_on_its_own_connection()
    {
        var finished = new ConcurrentBag<string>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Prepare = _ => Task.FromResult<EnginePrepareResult?>(null),
                    Finish = info =>
                    {
                        finished.Add(info.TargetName);
                        return Task.CompletedTask;
                    },
                }
            )
        );
        await remote.Prepare!(PrepareInfo("a"));
        await remote.Prepare!(PrepareInfo("b"));

        await Task.WhenAll(remote.Finish!(FinishInfo("a")), remote.Finish!(FinishInfo("b")));

        Assert.Equal(["a", "b"], finished.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Routes_attempt_callbacks_and_secret_resolution_back_to_the_harness()
    {
        var secret = Secret.Create("password", "hunter22", "generic-secret");
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Secrets = [secret],
                    StartAttempt = async context =>
                    {
                        context.AppLog(new AppLogEntry(AppLogSource.Console, AppLogLevel.Info, "hello"));
                        context.Navigation("navigated to /home");
                        context.Environment(new Dictionary<string, string> { ["device"] = "toy" });
                        context.Screen?.Invoke(new EngineSnapshot(Root, new ViewportSize { Width = 1, Height = 1 }));
                        var value = await context.ResolveSecret(
                            secret,
                            new ResolveSecretOptions { Derived = plain => [$"b64:{plain}"] }
                        );
                        context.AppLog(
                            new AppLogEntry(AppLogSource.System, AppLogLevel.Info, $"resolved {value.Length}")
                        );
                    },
                    EndAttempt = _ => Task.CompletedTask,
                }
            ),
            options => options with { Secrets = [secret] }
        );
        var received = new ConcurrentQueue<string>();
        var resolved = new ConcurrentQueue<(string Name, IReadOnlyList<string>? Derived)>();
        var context = new EngineAttemptContext
        {
            AttemptId = "attempt-1",
            ArtifactsDir = Path.GetTempPath(),
            Signal = CancellationToken.None,
            ResolveSecret = (handle, options) =>
            {
                resolved.Enqueue((handle.Name, options?.Derived?.Invoke("hunter22")));
                return Task.FromResult("hunter22");
            },
            AppLog = entry => received.Enqueue($"log:{entry.Text}"),
            Navigation = line => received.Enqueue($"nav:{line}"),
            Environment = facts => received.Enqueue($"env:{JsonSerializer.Serialize(facts)}"),
            Screen = snapshot => received.Enqueue($"screen:{snapshot.Root.Ref.Id}"),
        };
        await remote.Init!(InitInfo());
        await remote.StartAttempt!(context);
        await remote.EndAttempt!(Cleanup);

        Assert.Equal(
            ["log:hello", "nav:navigated to /home", "env:{\"device\":\"toy\"}", "screen:root", "log:resolved 8"],
            received.ToArray()
        );
        var only = Assert.Single(resolved);
        Assert.Equal("password", only.Name);
        Assert.Equal(["b64:hunter22"], only.Derived);
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Runs_prepare_and_finish_on_a_run_connection_and_init_again_after_dispose()
    {
        var calls = new ConcurrentQueue<string>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Prepare = info =>
                    {
                        info.Log("preparing");
                        return Task.FromResult<EnginePrepareResult?>(
                            new EnginePrepareResult
                            {
                                Workers = 1,
                                Env = new Dictionary<string, string> { ["TOY"] = "1" },
                            }
                        );
                    },
                    Finish = _ =>
                    {
                        calls.Enqueue("finish");
                        return Task.CompletedTask;
                    },
                    Init = info =>
                    {
                        info.Log($"init {info.WorkerSlot}");
                        return Task.CompletedTask;
                    },
                    Dispose = _ =>
                    {
                        calls.Enqueue("dispose");
                        return Task.CompletedTask;
                    },
                }
            )
        );
        var logs = new ConcurrentQueue<string>();
        var result = await remote.Prepare!(PrepareInfo("toy", logs.Enqueue));
        Assert.Equal(1, result!.Workers);
        Assert.Equal(new Dictionary<string, string> { ["TOY"] = "1" }, result.Env);
        await remote.Init!(InitInfo(logs.Enqueue));
        await remote.Dispose!(DisposeContext());
        await remote.Init!(InitInfo(logs.Enqueue));
        await remote.Dispose!(DisposeContext());
        await remote.Finish!(FinishInfo("toy"));

        Assert.Equal(["preparing", "init 0", "init 0"], logs.ToArray());
        Assert.Equal(["dispose", "dispose", "finish"], calls.ToArray());
    }

    private sealed class RecordingTransport(IEngineTransport inner, ConcurrentQueue<string> roles) : IEngineTransport
    {
        public string Label => "recording";

        public Task<ITransportConnection> OpenAsync(TransportContext context)
        {
            roles.Enqueue(context.Role);
            return inner.OpenAsync(context);
        }
    }

    [Fact]
    public async Task Opens_a_doctor_connection_never_one_for_an_already_aborted_signal_and_gives_an_aborted_doctor_its_grace()
    {
        var roles = new ConcurrentQueue<string>();
        var stopped = new ConcurrentQueue<string>();
        var local = E2E.Engine.EngineManifest.DefineEngine(
            Toy(toy =>
                toy with
                {
                    Doctor = info =>
                    {
                        var done = new TaskCompletionSource<IReadOnlyList<EngineDoctorCheck>>(
                            TaskCreationOptions.RunContinuationsAsynchronously
                        );
                        info.Signal.Register(() =>
                            _ = Task.Delay(100)
                                .ContinueWith(_ =>
                                {
                                    stopped.Enqueue("stopped");
                                    done.SetResult([]);
                                })
                        );
                        return done.Task;
                    },
                }
            )
        );
        var remote = ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = ProtocolManifest.DescribeEngine(local),
                Transport = new RecordingTransport(new LoopbackTransport(local), roles),
            }
        );

        var refused = await Assert.ThrowsAsync<EngineException>(() =>
            remote.Doctor!(DoctorInfo(new CancellationToken(true)))
        );
        Assert.Equal("CANCELLED", refused.Code);
        Assert.Empty(roles);

        using var controller = new CancellationTokenSource();
        controller.CancelAfter(50);
        var aborted = await Assert.ThrowsAsync<EngineException>(() => remote.Doctor!(DoctorInfo(controller.Token)));
        Assert.Equal("CANCELLED", aborted.Code);
        Assert.Equal(["doctor"], roles.ToArray());
        Assert.Equal(["stopped"], stopped.ToArray());
    }

    [Fact]
    public async Task Runs_doctor_over_the_protocol_with_its_info_and_returns_its_findings()
    {
        var seen = new ConcurrentQueue<(string TargetName, bool Headed, bool Log)>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Doctor = info =>
                    {
                        seen.Enqueue((info.TargetName, info.Headed, info.Log is not null));
                        return Task.FromResult<IReadOnlyList<EngineDoctorCheck>>([
                            new EngineDoctorCheck("toolchain", EngineDoctorStatus.Warn, "downloads on first run"),
                        ]);
                    },
                }
            )
        );

        var findings = await remote.Doctor!(DoctorInfo(headed: true));

        Assert.Equal([new EngineDoctorCheck("toolchain", EngineDoctorStatus.Warn, "downloads on first run")], findings);
        Assert.Equal([("toy", true, true)], seen.ToArray());
        Assert.Null(Of(Toy()).Remote.Doctor);
    }

    [Fact]
    public async Task Answers_session_hooks_the_client_declares_instead_of_the_server()
    {
        var calls = new ConcurrentQueue<string>();
        var (_, remote) = Of(
            Toy(toy =>
                toy with
                {
                    Session = new EngineSessionSpec
                    {
                        Back = _ =>
                        {
                            calls.Enqueue("server back");
                            return Task.CompletedTask;
                        },
                        Resize = (size, _) =>
                        {
                            calls.Enqueue($"server resize {size.Width}x{size.Height}");
                            return Task.CompletedTask;
                        },
                    },
                }
            ),
            options =>
                options with
                {
                    Session = new EngineSessionSpec
                    {
                        Restart = _ =>
                        {
                            calls.Enqueue("client restart");
                            return Task.CompletedTask;
                        },
                    },
                }
        );
        await remote.Init!(InitInfo());

        await remote.Session!.Back!(Operation());
        await remote.Session.Resize!(new ViewportSize { Width = 390, Height = 844 }, Operation());
        await remote.Session.Restart!(Operation());

        Assert.Equal(["server back", "server resize 390x844", "client restart"], calls.ToArray());
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Answers_locators_on_the_client_from_observations_when_asked_to()
    {
        var (_, remote) = Of(Toy(), options => options with { ResolveLocators = true });
        Assert.Contains("location", remote.Capabilities);
        await remote.Init!(InitInfo());

        var nodes = await remote.Locate!(
            new LocatorExpression
            {
                Kind = "role",
                Role = "button",
                Value = TextMatch.FromString("Cancel"),
                Exact = true,
            },
            Operation()
        );

        Assert.Equal(["n2"], nodes.Select(match => match.Ref.Id));
        await remote.Dispose!(DisposeContext());
    }

    [Fact]
    public async Task Refuses_a_server_whose_manifest_differs_from_the_declared_one()
    {
        var local = E2E.Engine.EngineManifest.DefineEngine(Toy(toy => toy with { Version = "2.0.0" }));
        var remote = ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = ProtocolManifest.DescribeEngine(local) with { Version = "1.0.0" },
                Transport = new LoopbackTransport(local),
            }
        );

        var refused = await Assert.ThrowsAsync<EngineException>(() => remote.Init!(InitInfo()));

        Assert.Contains("version is \"2.0.0\", declared \"1.0.0\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describes_an_engine_with_a_manifest_the_published_schema_accepts_and_the_schema_names_every_method_and_kind()
    {
        var schema = ReportSchema.ReadJson("engine-manifest-v1.schema.json");
        Assert.Equal(
            Protocol.EngineMethods,
            schema["properties"]!["methods"]!["items"]!["enum"]!.AsArray().Select(method => method!.GetValue<string>())
        );
        Assert.Equal(
            EngineActionKinds.KnownLocator,
            schema["properties"]!["actions"]!["items"]!["enum"]!.AsArray().Select(kind => kind!.GetValue<string>())
        );
        Assert.Equal(
            EngineActionKinds.Pointer,
            schema["properties"]!["pointerActions"]!["items"]!["enum"]!
                .AsArray()
                .Select(kind => kind!.GetValue<string>())
        );
        var (local, _) = Of(
            Toy(toy =>
                toy with
                {
                    Actions = ["tap", "blur"],
                    Perform = (_, _, _) => Task.CompletedTask,
                    PointerActions = ["tap"],
                    PerformAt = (_, _, _) => Task.CompletedTask,
                    Keyboard = new EngineKeyboardSpec
                    {
                        Type = (_, _, _) => Task.CompletedTask,
                        Press = (_, _) => Task.CompletedTask,
                    },
                    GrantsAllPermissions = true,
                    Init = _ => Task.CompletedTask,
                }
            )
        );
        var described = ProtocolManifest.DescribeEngine(local);
        Assert.True(Validate.IsValid(WireManifest(described)));
        Assert.True(described.GrantsAllPermissions);
        Assert.True(
            ProtocolClient
                .ConnectEngine(
                    new ConnectEngineOptions { Manifest = described, Transport = new LoopbackTransport(local) }
                )
                .GrantsAllPermissions
        );
        foreach (
            var (refused, declared) in new (string Json, E2E.Engine.Protocol.EngineManifest Manifest)[]
            {
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe", "engine/teleport"] }""",
                    new("x", "1", 1, ["engine/observe", "engine/teleport"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/readStyle"] }""",
                    new("x", "1", 1, ["engine/readStyle"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "actions": ["tap"] }""",
                    new("x", "1", 1, ["engine/observe"]) { Actions = ["tap"] }
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "actions": ["blur"] }""",
                    new("x", "1", 1, ["engine/observe"]) { Actions = ["blur"] }
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/viewportIntersection"] }""",
                    new("x", "1", 1, ["engine/viewportIntersection"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "tapModifiers": true }""",
                    new("x", "1", 1, ["engine/observe"]) { TapModifiers = true }
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "selectsDevice": true }""",
                    new("x", "1", 1, ["engine/observe"]) { SelectsDevice = true }
                ),
                // The client serves prepare for finish alone, which would drop the device it never forwards.
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["lifecycle/finish"], "selectsDevice": true }""",
                    new("x", "1", 1, ["lifecycle/finish"]) { SelectsDevice = true }
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "pointerActions": ["tap"] }""",
                    new("x", "1", 1, ["engine/observe"]) { PointerActions = ["tap"] }
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/locate"] }""",
                    new("x", "1", 1, ["engine/locate"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe", "keyboard/type"] }""",
                    new("x", "1", 1, ["engine/observe", "keyboard/type"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["state/capture"] }""",
                    new("x", "1", 1, ["state/capture"])
                ),
                (
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["artifacts/screenshot", "artifacts/startVideo"] }""",
                    new("x", "1", 1, ["artifacts/screenshot", "artifacts/startVideo"])
                ),
            }
        )
        {
            Assert.False(Validate.IsValid(JsonNode.Parse(refused)!), refused);
            Assert.Throws<ConfigurationException>(() =>
                ProtocolClient.ConnectEngine(
                    new ConnectEngineOptions { Manifest = declared, Transport = new LoopbackTransport(local) }
                )
            );
        }

        // The wire has no type for a flag that is not a boolean: the port types it, so only the schema refuses it.
        Assert.False(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "grantsAllPermissions": "yes" }"""
                )!
            )
        );
        // Only a worker-isolated engine names its isolation; `attempt` is the default, left out.
        Assert.False(
            Validate.IsValid(
                JsonNode.Parse(
                    """{ "name": "x", "version": "1", "spiVersion": 1, "methods": ["engine/observe"], "isolation": "attempt" }"""
                )!
            )
        );
    }

    private static JsonNode? WireManifest(E2E.Engine.Protocol.EngineManifest manifest) =>
        JsonSerializer.SerializeToNode(
            manifest,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            }
        );

    [Fact]
    public void Serves_every_in_process_target_through_one_protocol_client_and_leaves_a_connected_engine_as_declared()
    {
        var engine = E2E.Engine.EngineManifest.DefineEngine(Toy());
        var (_, remote) = Of(Toy());
        var root = Directory.GetCurrentDirectory();
        var config = E2EConfig
            .Default(root)
            .WithTarget(
                new TargetConfig
                {
                    Name = "toy",
                    Platform = "toy",
                    App = new AppConfig(),
                    EngineHandle = engine,
                }
            )
            .WithTarget(
                new TargetConfig
                {
                    Name = "remote",
                    Platform = "toy",
                    App = new AppConfig(),
                    EngineHandle = remote,
                }
            );

        var resolved = ResolvedConfig.Resolve(config);
        var served = resolved.Targets.Single(target => target.Name == "toy").EngineHandle;

        Assert.NotNull(served);
        Assert.NotSame(engine, served);
        Assert.Same(served, ProtocolClient.ProtocolEngine(served));
        Assert.Equal("toy", served.Name);
        Assert.Equal(
            engine.Capabilities.Order(StringComparer.Ordinal),
            served.Capabilities.Order(StringComparer.Ordinal)
        );
        Assert.Same(remote, resolved.Targets.Single(target => target.Name == "remote").EngineHandle);
        Assert.Same(
            served,
            ResolvedConfig
                .Resolve(
                    E2EConfig
                        .Default(root)
                        .WithTarget(
                            new TargetConfig
                            {
                                Name = "toy",
                                Platform = "toy",
                                App = new AppConfig(),
                                EngineHandle = engine,
                            }
                        )
                )
                .Targets.Single(target => target.Name == "toy")
                .EngineHandle
        );
    }

    [Fact]
    public void Rejects_a_malformed_manifest_at_config_load()
    {
        var transport = new LoopbackTransport(E2E.Engine.EngineManifest.DefineEngine(Toy()));
        EngineHandle Connect(E2E.Engine.Protocol.EngineManifest manifest, bool resolveLocators = false) =>
            ProtocolClient.ConnectEngine(
                new ConnectEngineOptions
                {
                    Manifest = manifest,
                    Transport = transport,
                    ResolveLocators = resolveLocators,
                }
            );

        Assert.Throws<ConfigurationException>(() => Connect(new("x", "1", 1, ["engine/teleport"])));
        Assert.Contains(
            "perform without observe",
            Assert.Throws<ConfigurationException>(() => Connect(new("x", "1", 1, ["engine/perform"]))).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "perform without actions",
            Assert
                .Throws<ConfigurationException>(() => Connect(new("x", "1", 1, ["engine/observe", "engine/perform"])))
                .Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "twice",
            Assert
                .Throws<ConfigurationException>(() => Connect(new("x", "1", 1, ["engine/observe", "engine/observe"])))
                .Message,
            StringComparison.Ordinal
        );
        // The schema cannot require engine/locate beside it: resolveLocators builds locate on the client.
        var measuring = new E2E.Engine.Protocol.EngineManifest(
            "x",
            "1",
            1,
            ["engine/observe", "engine/viewportIntersection"]
        );
        Assert.Contains(
            "viewportIntersection without locate",
            Assert.Throws<ConfigurationException>(() => Connect(measuring)).Message,
            StringComparison.Ordinal
        );
        Assert.Contains("viewport-intersection", Connect(measuring, resolveLocators: true).Capabilities);
    }
}
