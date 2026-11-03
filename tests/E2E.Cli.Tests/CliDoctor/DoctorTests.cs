// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.CliDoctor;

/// <summary>
/// <c>describe('doctor')</c> of upstream <c>tests/unit/cli-doctor.test.ts</c>: <see cref="Doctor.RunAsync"/> in process, for what the CLI
/// cannot show without waiting out the engine budget. Upstream's engine runs behind a protocol connection that closes 200 ms after the
/// abort; the port has no protocol engines, so the engine's own teardown stands in for the connection's close.
/// </summary>
public sealed class DoctorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-doctor-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A project whose one target's engine checks until aborted, then finishes its teardown 200 ms later, marking <c>closed</c>.</summary>
    private DoctorOptions Slow(TimeSpan? timeout = null)
    {
        File.WriteAllText(Path.Combine(_dir, "e2e.config.json"), """{ "targets": [{ "name": "slow", "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }] }""");
        return new DoctorOptions
        {
            Version = "0.0.0",
            Cwd = _dir,
            Env = new Dictionary<string, string?>(),
            Notice = (_, _) => { },
            EngineTimeout = timeout,
            CreateEngine = (_, _) => new DoctorEngine("slow", async (_, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    await Task.Delay(200, CancellationToken.None);
                    File.WriteAllText(Path.Combine(_dir, "closed"), "");
                    throw;
                }

                return [];
            }),
        };
    }

    [Fact]
    public async Task Partial_Waits_for_a_cancelled_engine_to_close_its_connection_before_it_reports()
    {
        using var interrupt = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var report = await Doctor.RunAsync(Slow(), interrupt.Token);
        Assert.True(File.Exists(Path.Combine(_dir, "closed")));
        var check = report.Checks.First(check => check.Target == "slow");
        Assert.Equal(("engine", DoctorStatus.Fail, "CANCELLED"), (check.Id, check.Status, check.Code));
    }

    [Fact]
    public async Task Partial_Fails_an_engine_that_outlives_its_budget_as_ENGINE_FAILURE_after_its_connection_closed()
    {
        var report = await Doctor.RunAsync(Slow(TimeSpan.FromMilliseconds(100)), CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(_dir, "closed")));
        var check = report.Checks.First(check => check.Target == "slow");
        Assert.Equal(("engine", DoctorStatus.Fail, "ENGINE_FAILURE", "engine \"slow\" did not finish its checks within 100ms"), (check.Id, check.Status, check.Code, check.Message));
    }
}
