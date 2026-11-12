// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Reports;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.AppPortAllocation;

/// <summary>Upstream: packages/e2e/tests/integration/app-port-allocation.test.ts, describe('app port allocation').</summary>
public sealed class AppPortAllocationTests
{
    /// <summary>
    /// Partial: the port has no <c>runExisting</c>, so the app command is started through <c>RunApps</c> and the test checks the app command's setup
    /// steps and that the server answers on the assigned port. It does not run a test against the base URL, read the report's <c>baseOrigin</c>, or
    /// start a second run (see COMPATIBILITY.md).
    /// </summary>
    [Fact]
    public async Task Partial_Serves_the_app_on_a_port_the_run_picked_and_hands_tests_the_allocated_base_URL_run_after_run()
    {
        var config = E2EConfig.Parse(
            $$"""
            { "targets": [{ "name": "web", "app": { "url": "http://127.0.0.1:0",
              "command": { "executable": {{JsonSerializer.Serialize(AppProgram.Executable)}}, "args": ["serve", "{port}"], "startupTimeout": 20000, "inheritEnv": ["DOTNET_ROOT"] } } }] }
            """,
            Directory.CreateTempSubdirectory("e2e-appports-").FullName,
            _ => null);
        var steps = new List<AppSetupStep>();

        // The process's events hear every app this process starts at once, so the steps are read by the URL they name below.
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", runEvent =>
        {
            if (runEvent.Fact is SetupEvent { Step: AppSetupStep step })
            {
                lock (steps)
                {
                    steps.Add(step);
                }
            }
        }));

        try
        {
            var started = await E2E.RunApps.StartAsync(config, config.Target, null, CancellationToken.None);
            Assert.True(await AppProgram.IsReachable(started.App.Url!));

            // The app command's setup step names the URL on the assigned port, not the declared `:0`.
            var origin = new Uri(started.App.Url!).GetLeftPart(UriPartial.Authority);
            Assert.NotEqual("http://127.0.0.1:0", origin);
            var step = new AppSetupStep("target \"web\" command", origin + "/");
            lock (steps)
            {
                Assert.Equal([step, step], steps.Where(each => each.Url == step.Url));
            }
        }
        finally
        {
            await E2E.RunApps.StopAllAsync();
        }
    }
}
