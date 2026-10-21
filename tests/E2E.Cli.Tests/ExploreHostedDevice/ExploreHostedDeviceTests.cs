// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;

namespace E2E.Cli.Tests.ExploreHostedDevice;

/// <summary>
/// .NET-only: upstream's <c>explore</c> opens whatever engine its config holds, a hosted device included. The port builds the device engine
/// from <c>E2E_MOBILE_PROVIDER</c>, so these run <c>Explore.ExploreAsync</c> over a real <see cref="E2E.Mobile.MobileEngine"/> and a fake provider
/// that counts what it leased and what came back. No test reaches Limrun or a DevFlow agent.
/// </summary>
public sealed class ExploreHostedDeviceTests : IDisposable
{
    private const string Config = """{ "targets": [{ "name": "device", "platform": "ios", "app": { "bundleId": "com.example.greeter" } }] }""";

    private readonly string _root = Directory.CreateTempSubdirectory("e2e-explore-hosted-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A planner that has seen enough: it finishes at once.</summary>
    private static ScriptedModel Finishing() => new(_ => new ModelResponse
    {
        ToolCalls =
        [
            new ModelToolCall
            {
                Id = "call_extract",
                Name = "extract",
                Arguments = JsonSerializer.SerializeToElement(new { data = new { decision = "finish", title = "", instruction = "", summary = "Looked." } }),
            },
        ],
    });

    private ExploreOptions Options(FakeCloud cloud, ScriptedModel model, string? target = null)
    {
        var config = E2EConfig.Parse(Config, _root);
        return new ExploreOptions
        {
            Cwd = _root,
            RawConfig = config,
            Agents = new Dictionary<string, AgentOptions> { ["default"] = new AgentOptions { Model = model } },
            Engine = cloud.Engine(config.Targets[0]),
            Goal = "Explore the greeter and find bugs",
            Target = target,
            MaxSteps = 2,
            Timeout = TimeSpan.FromMinutes(3),
        };
    }

    [Fact]
    public async Task Leases_when_the_exploration_starts_and_releases_when_it_ends()
    {
        var cloud = new FakeCloud();
        var outcome = await Explore.ExploreAsync(Options(cloud, Finishing()));
        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Leases_nothing_when_the_exploration_fails_before_a_session_opens()
    {
        var cloud = new FakeCloud();
        var options = Options(cloud, Finishing(), target: "missing");
        await Assert.ThrowsAnyAsync<E2EException>(() => Explore.ExploreAsync(options));
        Assert.Equal((0, 0), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_when_the_app_will_not_launch()
    {
        var cloud = new FakeCloud { OnOpen = (count, _) => count == 2 ? throw new InvalidOperationException("the app would not launch") : Task.CompletedTask };
        var outcome = await Explore.ExploreAsync(Options(cloud, Finishing()));
        Assert.NotNull(outcome.Error);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_when_the_run_is_cancelled_and_reports_a_cancel()
    {
        var launching = new TaskCompletionSource();
        var cloud = new FakeCloud
        {
            OnOpen = async (count, token) =>
            {
                if (count == 2)
                {
                    launching.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                }
            },
        };
        using var cancel = new CancellationTokenSource();
        var running = Explore.ExploreAsync(Options(cloud, Finishing()), cancel.Token);
        await launching.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancel.CancelAsync();
        try
        {
            var outcome = await running;
            Assert.Equal("CANCELLED", outcome.Error?.Code);
        }
        catch (Exception cause) when (cause is OperationCanceledException or E2EException)
        {
            // A cancelled run may end by throwing: either way it is a cancel, and the lease below is what matters.
        }

        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Reports_a_release_that_fails_instead_of_losing_it()
    {
        var cloud = new FakeCloud { OnRelease = () => throw new InvalidOperationException("stop failed") };
        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => Explore.ExploreAsync(Options(cloud, Finishing())));
        Assert.Contains("could not release a device: stop failed", failure.Message, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }
}
