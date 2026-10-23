// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;

namespace E2E.Cli.Tests.McpHostedDevice;

/// <summary>
/// .NET-only: upstream's MCP session opens whatever engine its config holds, a hosted device included, and tests the lease inside the
/// provider's own package. The port builds the device engine from <c>E2E_MOBILE_PROVIDER</c>, so these tests run the session host over a real
/// <see cref="E2E.Mobile.MobileEngine"/> and a fake provider that counts what it leased and what came back. A hosted device bills until it is released,
/// so each way a session can end must release it once. The host's own close and finish are tested in <c>OpenHostLifecycleTests</c> and
/// <c>EngineTargetsTests</c> of <c>E2E.Tests</c>; these test that the session host uses it. No test reaches Limrun or a DevFlow agent.
/// </summary>
public sealed class HostedDeviceSessionTests : IDisposable
{
    private const string Config = """{ "targets": [{ "name": "device", "platform": "ios", "app": { "bundleId": "com.example.greeter" } }] }""";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-mcp-hosted-").FullName;
    private readonly List<string> _logs = [];
    private readonly List<SessionHost> _hosts = [];

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Leases_when_the_session_opens_and_releases_when_it_closes()
    {
        var cloud = new FakeCloud();
        var host = Host(cloud);
        Assert.Equal(0, cloud.Acquired);
        await host.OpenAsync(new OpenSessionOptions());
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        await host.CloseAsync("done");
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_of_an_open_that_fails_after_the_lease_exists_and_leaves_the_host_ready()
    {
        var cloud = new FakeCloud { OnOpen = (count, _) => count == 2 ? throw new InvalidOperationException("the app would not launch") : Task.CompletedTask };
        var host = Host(cloud);
        var failure = await Assert.ThrowsAsync<EngineException>(() => host.OpenAsync(new OpenSessionOptions()));
        Assert.Contains("the app would not launch", failure.Message, StringComparison.Ordinal);
        Assert.False(host.IsOpen);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));

        cloud.OnOpen = null;
        await host.OpenAsync(new OpenSessionOptions());
        await host.CloseAsync("done");
        Assert.Equal((2, 2), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_of_an_open_the_request_cancels_and_reports_a_cancel()
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
        var host = Host(cloud);
        using var request = new CancellationTokenSource();
        var opening = host.OpenAsync(new OpenSessionOptions(), request.Token);
        await launching.Task.WaitAsync(Patience);
        await request.CancelAsync();
        var cancelled = await Assert.ThrowsAnyAsync<E2EException>(() => opening);
        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.False(host.IsOpen);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Releases_the_lease_of_an_open_the_request_cancels_while_the_provider_leases()
    {
        var leasing = new TaskCompletionSource();
        var grant = new TaskCompletionSource();
        var cloud = new FakeCloud
        {
            OnAcquire = async () =>
            {
                leasing.SetResult();
                await grant.Task;
            },
        };
        var host = Host(cloud);
        using var request = new CancellationTokenSource();
        var opening = host.OpenAsync(new OpenSessionOptions(), request.Token);
        await leasing.Task.WaitAsync(Patience);
        await request.CancelAsync();

        // The provider ignores its token and has not answered: the open ends with a cancel, and nothing was granted to give back yet.
        var cancelled = await Assert.ThrowsAnyAsync<E2EException>(() => opening.WaitAsync(Patience));
        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.False(host.IsOpen);
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));

        // The lease that lands after the cancel is released where it lands, not left billing.
        grant.SetResult();
        await UntilAsync(() => cloud.Released == 1);
    }

    [Fact]
    public async Task Releases_the_lease_of_a_session_that_reaches_its_time_limit()
    {
        var cloud = new FakeCloud();
        var host = Host(cloud, lifetime: TimeSpan.FromMilliseconds(500));
        await host.OpenAsync(new OpenSessionOptions());
        await UntilAsync(() => !host.IsOpen);
        await UntilAsync(() => cloud.Released == 1);
        Assert.Equal(1, cloud.Acquired);
        Assert.Contains(_logs, line => line.Contains("reached its time limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Releases_the_lease_of_a_session_that_sits_idle()
    {
        var cloud = new FakeCloud();
        var host = Host(cloud, idle: TimeSpan.FromMilliseconds(200));
        await host.OpenAsync(new OpenSessionOptions());
        await UntilAsync(() => cloud.Released == 1);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task Releases_the_lease_of_every_session_when_the_server_shuts_down()
    {
        var cloud = new FakeCloud();
        var host = Host(cloud);
        await host.OpenAsync(new OpenSessionOptions());
        var summary = await host.CloseAllAsync("server shutdown");
        Assert.Contains("closed (server shutdown)", summary, StringComparison.Ordinal);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Leases_nothing_for_a_second_session_on_the_target_that_is_refused_and_leaves_the_first_lease_alone()
    {
        var cloud = new FakeCloud();
        var host = Host(cloud, maxSessions: 2);
        var first = Regex.Match(await host.OpenAsync(new OpenSessionOptions()), @"^Session (\S+) open", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value;
        var other = await Assert.ThrowsAsync<ConfigurationException>(() => host.OpenAsync(new OpenSessionOptions()));
        Assert.Equal("ENGINE_IN_USE", other.Code);
        Assert.Equal((1, 0), (cloud.Acquired, cloud.Released));
        await host.CloseAsync("done", first);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));

        await host.OpenAsync(new OpenSessionOptions());
        await host.CloseAsync("done");
        Assert.Equal((2, 2), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Reports_a_release_that_fails_in_the_close_and_still_frees_the_session()
    {
        var cloud = new FakeCloud { OnRelease = () => throw new InvalidOperationException("stop failed") };
        var host = Host(cloud);
        await host.OpenAsync(new OpenSessionOptions());
        var closed = await host.CloseAsync("done");
        Assert.Contains("Cleanup:", closed, StringComparison.Ordinal);
        Assert.Contains("could not release a device: stop failed", closed, StringComparison.Ordinal);
        Assert.False(host.IsOpen);
        Assert.Equal((1, 1), (cloud.Acquired, cloud.Released));
    }

    [Fact]
    public async Task Redacts_the_services_key_from_an_open_failure_and_the_cleanup_line_and_shows_no_lease_id_or_token()
    {
        var cloud = new FakeCloud { OnAcquire = () => throw new InvalidOperationException("Limrun answered 401 to POST /v1/ios_instances: bad key limrun-key-12345") };
        var host = Host(cloud);
        var result = await host.ToolSpecs()[0].Call(Json("{}"), CancellationToken.None);
        var text = Text(result);
        Assert.True(result.IsError);
        Assert.Contains("<secret:LIMRUN_API_KEY>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("limrun-key-12345", text, StringComparison.Ordinal);
        Assert.All(_logs, line => Assert.DoesNotContain("limrun-key-12345", line, StringComparison.Ordinal));

        cloud.OnAcquire = null;
        cloud.OnRelease = () => throw new InvalidOperationException("stop failed for limrun-key-12345");
        var specs = host.ToolSpecs().ToDictionary(spec => spec.Name);
        var opened = Text(await specs["open_session"].Call(Json("{}"), CancellationToken.None));
        var closed = Text(await specs["close_session"].Call(Json("{}"), CancellationToken.None));
        Assert.DoesNotContain("limrun-key-12345", closed, StringComparison.Ordinal);
        Assert.Contains("<secret:LIMRUN_API_KEY>", closed, StringComparison.Ordinal);
        Assert.All(new[] { opened, closed }, shown =>
        {
            Assert.DoesNotContain("lease-secret-id", shown, StringComparison.Ordinal);
            Assert.DoesNotContain("daemon-secret-token", shown, StringComparison.Ordinal);
        });
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Text(ModelContextProtocol.Protocol.CallToolResult result) =>
        string.Join('\n', result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(part => part.Text));

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition did not hold in time");
            await Task.Delay(20);
        }
    }

    /// <summary>A host whose every session builds a device engine over <paramref name="cloud"/>, as the tool builds one from E2E_MOBILE_PROVIDER.</summary>
    private SessionHost Host(FakeCloud cloud, TimeSpan? idle = null, TimeSpan? lifetime = null, int maxSessions = 4)
    {
        var host = new SessionHost(new SessionHostOptions
        {
            LocateConfig = requested => Path.Combine(_dir, requested ?? "e2e.config.json"),
            LoadConfig = path => Task.FromResult(E2EConfig.Parse(Config, _dir)),
            CreateEngine = (target, _) => cloud.Engine(target),
            Credentials = () => [Secret.Create("LIMRUN_API_KEY", "limrun-key-12345")],
            MaxSessions = maxSessions,
            Idle = idle ?? SessionHost.SessionIdle,
            Ttl = lifetime ?? SessionHost.SessionTtl,
            Log = (level, message) =>
            {
                lock (_logs)
                {
                    _logs.Add(level + ": " + message);
                }
            },
        });
        _hosts.Add(host);
        return host;
    }
}
