// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.RunApps;

/// <summary>
/// .NET-only: <c>RunApps</c> holds the app command for the life of the test process, where upstream's runner starts it per run. No upstream
/// <c>it</c> covers it (<c>target-processes.test.ts</c> drives the runner, which the port lacks).
/// </summary>
public sealed class RunAppsTests
{
    [Fact]
    public async Task Starts_the_command_on_the_free_port_the_url_asked_for_and_keeps_it_for_the_next_caller()
    {
        var config = E2EConfig.Parse(
            $$"""
            { "targets": [{ "app": { "url": "http://127.0.0.1:0",
              "command": { "executable": {{JsonSerializer.Serialize(AppProgram.Executable)}}, "args": ["serve", "{port}"], "startupTimeout": 20000, "inheritEnv": ["DOTNET_ROOT"] } } }] }
            """,
            Directory.CreateTempSubdirectory("e2e-runapps-").FullName,
            _ => null);
        var notices = new List<string>();
        var first = await E2E.RunApps.StartAsync(config, config.Target, notices.Add, CancellationToken.None);
        Assert.NotEqual("http://127.0.0.1:0/", first.App.Url);
        Assert.True(await AppProgram.IsReachable(first.App.Url!));

        // A later test, explore, or MCP session finds it running on the same port.
        var second = await E2E.RunApps.StartAsync(config, config.Target, notices.Add, CancellationToken.None);
        Assert.Equal(first.App.Url, second.App.Url);
        Assert.Empty(notices);
    }

    [Fact]
    public async Task Starts_the_command_again_when_the_app_has_ended()
    {
        var config = E2EConfig.Parse(
            $$"""
            { "targets": [{ "app": { "url": "http://127.0.0.1:0",
              "command": { "executable": {{JsonSerializer.Serialize(AppProgram.Executable)}}, "args": ["serve-for", "{port}", "6000"], "startupTimeout": 20000, "inheritEnv": ["DOTNET_ROOT"] } } }] }
            """,
            Directory.CreateTempSubdirectory("e2e-runapps-").FullName,
            _ => null);
        var first = await E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None);
        Assert.True(await AppProgram.IsReachable(first.App.Url!));
        while (await AppProgram.IsReachable(first.App.Url!))
        {
            await Task.Delay(100);
        }

        // The listener closes a moment before the process is reaped, so a call in that gap still finds the ended app running and returns the
        // target it already holds. The next call starts a new one, and only that one counts.
        var deadline = DateTime.UtcNow.AddSeconds(20);
        var second = await E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None);
        while (ReferenceEquals(second, first) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            second = await E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None);
        }

        Assert.NotSame(first, second);
        Assert.True(await AppProgram.IsReachable(second.App.Url!));
    }

    [Fact]
    public async Task Leaves_a_target_without_a_command_as_it_is()
    {
        var config = E2EConfig.Parse("""{ "targets": [{ "app": { "url": "http://127.0.0.1:3000" } }] }""", Path.GetTempPath(), _ => null);
        Assert.Same(config.Target, await E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None));
    }

    [Fact]
    public async Task Names_APP_UNREACHABLE_when_the_command_exits_before_the_app_answers()
    {
        var config = E2EConfig.Parse(
            $$"""
            { "targets": [{ "app": { "url": "http://127.0.0.1:0",
              "command": { "executable": {{JsonSerializer.Serialize(AppProgram.Executable)}}, "args": ["exit", "3"], "startupTimeout": 20000, "inheritEnv": ["DOTNET_ROOT"] } } }] }
            """,
            Directory.CreateTempSubdirectory("e2e-runapps-").FullName,
            _ => null);
        var failure = await Assert.ThrowsAsync<EngineException>(() => E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None));
        Assert.Equal("APP_UNREACHABLE", failure.Code);

        // The failed start is not remembered: the next caller starts afresh and fails the same way.
        await Assert.ThrowsAsync<EngineException>(() => E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None));
    }
}
