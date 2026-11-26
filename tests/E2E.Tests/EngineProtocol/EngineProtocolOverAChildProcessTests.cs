// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Engine.Protocol;
using E2E.Tests.ManagedProcess;
using static E2E.Tests.EngineProtocol.OverLoopback;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// Upstream's <c>describe('engine protocol over a child process')</c> (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>): the toy
/// server of upstream's <c>fixtures/protocol-server.ts</c> (<c>protocol-server</c> of <c>E2E.Tests.App</c>) in a process of its own.
/// </summary>
public sealed class EngineProtocolOverAChildProcessTests
{
    private static readonly string[] Methods =
    [
        "engine/observe",
        "engine/perform",
        "lifecycle/init",
        "lifecycle/startAttempt",
        "lifecycle/endAttempt",
    ];

    private static EngineHandle Connect(JsonNode? options = null, bool doctor = false) =>
        ProtocolClient.ConnectEngine(
            new ConnectEngineOptions
            {
                Manifest = new E2E.Engine.Protocol.EngineManifest(
                    "toy-server",
                    "1.0.0",
                    1,
                    doctor ? [.. Methods, "lifecycle/doctor"] : Methods
                )
                {
                    Platform = "toy",
                    Actions = ["tap"],
                },
                Transport = new SpawnTransport(
                    new SpawnTransportOptions { Command = AppProgram.Executable, Args = ["protocol-server"] }
                ),
                InitializationOptions = options,
            }
        );

    /// <summary>A client of the toy server in <paramref name="crashMode"/>, initialized.</summary>
    private static async Task<EngineHandle> Crashing(string crashMode)
    {
        var remote = Connect(new JsonObject { ["crashOnObserve"] = true, ["crashMode"] = crashMode });
        await remote.Init!(InitInfo());
        return remote;
    }

    /// <summary>Whether the process still runs after up to 5 s: a stopped server may need a moment to exit.</summary>
    private static async Task<bool> StillAliveAsync(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Runs <paramref name="body"/> and stops the server after it, even when it throws.</summary>
    private static async Task Using(EngineHandle remote, Func<Task> body)
    {
        try
        {
            await body();
        }
        finally
        {
            await remote.Dispose!(DisposeContext());
        }
    }

    [Fact]
    public async Task Drives_an_engine_in_its_own_process_and_stops_it_on_dispose()
    {
        var remote = Connect();
        var pid = 0;
        await Using(remote, async () =>
        {
            var logs = new List<string>();
            await remote.Init!(InitInfo(logs.Add));
            var snapshot = await remote.Observe!(Operation(), null);
            pid = int.Parse(snapshot.Location!["pid:".Length..], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Matches(@"\Apid:\d+\z", snapshot.Location);
            Assert.NotEqual($"pid:{Environment.ProcessId}", snapshot.Location);
            await remote.Perform!(new NodeRef("n1", ""), new LocatorAction.Tap(), Operation());
            Assert.Equal(["init slot 0"], logs);
        });
        Assert.False(await StillAliveAsync(pid));
    }

    [Fact]
    public async Task Stops_a_server_that_hangs_in_doctor_and_ignores_SIGTERM_before_doctor_settles()
    {
        var remote = Connect(new JsonObject { ["hangDoctor"] = true }, doctor: true);
        using var controller = new CancellationTokenSource();
        var pid = 0;
        try
        {
            var cancelled = await Assert.ThrowsAsync<EngineException>(() =>
                remote.Doctor!(
                    DoctorInfo(controller.Token) with
                    {
                        Log = line =>
                        {
                            pid = int.Parse(line["pid:".Length..], System.Globalization.CultureInfo.InvariantCulture);
                            controller.Cancel();
                        },
                    }
                )
            );

            Assert.Equal("CANCELLED", cancelled.Code);
        }
        finally
        {
            if (pid > 0 && await StillAliveAsync(pid))
            {
                Process.GetProcessById(pid).Kill(entireProcessTree: true);
            }
        }

        Assert.True(pid > 0);
        Assert.False(await StillAliveAsync(pid));
    }

    [Fact]
    public async Task Fails_a_request_with_the_server_stderr_when_the_process_dies_mid_call()
    {
        var remote = Connect(new JsonObject { ["crashOnObserve"] = true });
        await Using(remote, async () =>
        {
            await remote.Init!(InitInfo());

            var failed = await Assert.ThrowsAnyAsync<Exception>(() => remote.Observe!(Operation(), null));

            Assert.Matches(@"closed the connection[\s\S]*crashing on purpose", failed.Message);
        });
    }

    [Fact]
    public async Task Waits_for_the_exit_code_and_stderr_of_a_server_whose_stdout_closed_before_it_exited()
    {
        var remote = await Crashing("stdout-first");
        await Using(remote, async () =>
        {
            var start = Stopwatch.StartNew();

            var failed = await Assert.ThrowsAnyAsync<Exception>(() => remote.Observe!(Operation(), null));

            Assert.Matches(@"closed the connection[\s\S]*exited with code 7[\s\S]*crashing on purpose", failed.Message);
            // Well under the 2 s a transport waits at most: the exit ends the wait.
            Assert.True(start.ElapsedMilliseconds < 1_500, $"took {start.ElapsedMilliseconds} ms");
        });
    }

    [Fact]
    public async Task Waits_for_stderr_a_child_of_the_server_writes_after_the_server_exited()
    {
        var remote = await Crashing("late-child");
        await Using(remote, async () =>
        {
            var failed = await Assert.ThrowsAnyAsync<Exception>(() => remote.Observe!(Operation(), null));

            Assert.Matches(@"exited with code 7[\s\S]*toy server child: late line", failed.Message);
        });
    }

    [Fact]
    public async Task Fails_a_request_of_a_server_that_closed_its_stdout_and_keeps_running()
    {
        var remote = await Crashing("stays-up");
        await Using(remote, async () =>
        {
            var start = Stopwatch.StartNew();

            var failed = await Assert.ThrowsAnyAsync<Exception>(() => remote.Observe!(Operation(), null));

            Assert.Matches(@"closed the connection[\s\S]*stdout closed, staying", failed.Message);
            // The 2 s cap, not the test timeout, ends the wait.
            Assert.True(start.ElapsedMilliseconds >= 1_900, $"took {start.ElapsedMilliseconds} ms");
            Assert.True(start.ElapsedMilliseconds < 3_000, $"took {start.ElapsedMilliseconds} ms");
        });
    }

    [Fact]
    public async Task Names_the_signal_that_stopped_a_server_mid_request_and_its_stderr()
    {
        // .NET reports no signal on Windows: a killed process only has an exit code there (see the PR report).
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var remote = await Crashing("signal");
        await Using(remote, async () =>
        {
            var failed = await Assert.ThrowsAnyAsync<Exception>(() => remote.Observe!(Operation(), null));

            Assert.Matches(@"killed by SIGKILL[\s\S]*crashing on purpose", failed.Message);
        });
    }
}
