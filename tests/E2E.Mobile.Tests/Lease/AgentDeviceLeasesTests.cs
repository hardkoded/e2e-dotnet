// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text.Json.Nodes;
using E2E.Mobile.Lease;

namespace E2E.Mobile.Tests.Lease;

/// <summary>
/// Ports <c>describe('agentDeviceLeases()')</c> of <c>packages/mobile/tests/unit/lease.test.ts</c>: the lease it allocates and hands the
/// worker, the environment and home each daemon gets, and the release on every exit path, against a stubbed daemon.
/// <c>LeaseDaemon/</c> covers the stop report.
/// </summary>
public sealed class AgentDeviceLeasesTests : IDisposable
{
    private static readonly AgentDeviceLeaseOptions Options = new() { Provider = "cloud", Label = "Cloud" };

    private static readonly Dictionary<string, string?> RunEnvironment = new() { ["PATH"] = "/bin" };

    private readonly FakeLeaseHost _host = new();

    public void Dispose() => _host.Dispose();

    private AgentDeviceLeases Leases() => new(Options, _host);

    private static AgentDeviceLeaseRequest Allocation(Func<string, CancellationToken, Task>? prepareHome = null, IReadOnlyDictionary<string, string>? env = null) => new()
    {
        Env = env ?? new Dictionary<string, string> { ["CLOUD_USERNAME"] = "ada", ["CLOUD_ACCESS_KEY"] = "cloud-key" },
        Selectors = new LeaseSelectors { Device = "Google Pixel 9", ProviderOsVersion = "16.0", ProviderApp = "cloud://APP0" },
        Summary = "Google Pixel 9, android 16.0",
        PrepareHome = prepareHome,
    };

    private static DeviceRequest Request(MobilePlatform platform = MobilePlatform.Android, int slot = 0, IReadOnlyDictionary<string, string?>? env = null, Action<string>? log = null) => new()
    {
        Platform = platform,
        RunId = "run-1",
        TargetName = "android",
        Slot = slot,
        Slots = 2,
        AgentDeviceVersion = AgentDeviceVersion.Pinned,
        ProjectRoot = "/project",
        Env = env ?? RunEnvironment,
        Log = log ?? (_ => { }),
    };

    [Fact]
    public async Task Allocates_a_retained_lease_from_its_own_daemon_with_the_selectors_and_the_platform_backend()
    {
        var lease = await Leases().AcquireAsync(Request(), Allocation(), CancellationToken.None);

        JsonAssert.Args(
            """{"tenant":"cloud","runId":"run-1","leaseBackend":"android-instance","leaseProvider":"cloud","platform":"android","target":"mobile","device":"Google Pixel 9","providerOsVersion":"16.0","providerApp":"cloud://APP0","ttlMs":60000,"retainOnClose":true}""",
            _host.Starts[0].Request.Allocate);
        Assert.Equal("Cloud", _host.Starts[0].Label);
        Assert.True(Directory.Exists(_host.DirectoryOf()));
        await Leases().AcquireAsync(Request(MobilePlatform.Ios), Allocation(), CancellationToken.None);
        Assert.Equal("ios-instance", _host.Starts[1].Request.Allocate["leaseBackend"]!.GetValue<string>());
        Assert.Equal("ios", _host.Starts[1].Request.Allocate["platform"]!.GetValue<string>());
        Assert.Equal("lease-1", lease.Id);
    }

    [Fact]
    public async Task Hands_the_worker_the_lease_scope_and_the_selectors_as_JSON_client_configuration_without_credentials()
    {
        var request = Request();
        var lease = await Leases().AcquireAsync(request, Allocation(), CancellationToken.None);

        var stateDirectory = _host.Starts[0].Request.StateDirectory;
        Assert.Equal("lease-1", lease.Id);
        JsonAssert.Args(
            $$"""{"stateDir":{{JsonValue.Create(stateDirectory)!.ToJsonString()}},"tenant":"cloud","runId":"run-1","leaseId":"lease-1","leaseBackend":"android-instance","leaseProvider":"cloud","platform":"android","target":"mobile","device":"Google Pixel 9","providerOsVersion":"16.0","providerApp":"cloud://APP0"}""",
            lease.Client);
        Assert.DoesNotContain("cloud-key", lease.Client!.ToJsonString(), StringComparison.Ordinal);
        var (scope, log) = Assert.Single(_host.Heartbeats);
        JsonAssert.Args(
            $$"""{"stateDir":{{JsonValue.Create(stateDirectory)!.ToJsonString()}},"tenant":"cloud","runId":"run-1","leaseId":"lease-1","leaseBackend":"android-instance","leaseProvider":"cloud"}""",
            scope);
        Assert.Same(request.Log, log);
    }

    [Fact]
    public async Task Logs_the_granted_lease_with_its_summary()
    {
        var lines = new List<string>();

        await Leases().AcquireAsync(Request(log: lines.Add), Allocation(), CancellationToken.None);

        Assert.Equal(["lease lease-1: Google Pixel 9, android 16.0"], lines);
    }

    [Fact]
    public async Task Releases_a_granted_lease_when_the_runs_log_throws_instead_of_losing_it()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Leases().AcquireAsync(Request(log: _ => throw new InvalidOperationException("log closed")), Allocation(), CancellationToken.None));

        Assert.Equal("log closed", error.Message);
        var stop = Assert.Single(_host.Stops);
        Assert.Equal((_host.Starts[0].Request.StateDirectory, "lease-1", "Cloud"), (stop.StateDirectory, stop.LeaseId, stop.Label));
        Assert.Same(_host.Starts[0].Env, stop.Env);
        Assert.Equal(1, _host.HeartbeatsStopped);
    }

    [Fact]
    public async Task Gives_each_leases_daemon_its_own_state_agent_device_home_and_the_services_env_and_never_mutates_the_process_environment()
    {
        var before = Snapshot();
        var provider = Leases();
        var runEnvironment = new Dictionary<string, string?>
        {
            ["PATH"] = "/bin",
            ["CLOUD_USERNAME"] = "run-user",
            ["AGENT_DEVICE_STATE_DIR"] = "/foreign",
            ["AGENT_DEVICE_HOME"] = "/foreign-home",
            ["AGENT_DEVICE_DAEMON_POLICY"] = "/policy.json",
        };

        await Task.WhenAll(
            provider.AcquireAsync(Request(env: runEnvironment), Allocation(), CancellationToken.None),
            provider.AcquireAsync(Request(slot: 1, env: runEnvironment), Allocation(), CancellationToken.None));

        Assert.NotEqual(_host.Starts[0].Request.StateDirectory, _host.Starts[1].Request.StateDirectory);
        Assert.NotEqual(_host.Starts[0].Env["AGENT_DEVICE_HOME"], _host.Starts[1].Env["AGENT_DEVICE_HOME"]);
        var env = _host.Starts[0].Env;
        Assert.Equal("ada", env["CLOUD_USERNAME"]);
        Assert.Equal("cloud-key", env["CLOUD_ACCESS_KEY"]);
        Assert.Equal("/bin", env["PATH"]);
        Assert.Equal("/policy.json", env["AGENT_DEVICE_DAEMON_POLICY"]);
        Assert.False(env.ContainsKey("AGENT_DEVICE_STATE_DIR"));
        Assert.Equal(Path.Combine(_host.DirectoryOf(), "home"), env["AGENT_DEVICE_HOME"]);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Drops_agent_device_daemon_settings_from_the_services_env_too_so_it_cannot_pick_another_daemon_or_home()
    {
        var env = new Dictionary<string, string>
        {
            ["CLOUD_USERNAME"] = "ada",
            ["AGENT_DEVICE_HOME"] = "/foreign-home",
            ["AGENT_DEVICE_DAEMON_BASE_URL"] = "http://foreign",
        };

        await Leases().AcquireAsync(Request(), Allocation(env: env), CancellationToken.None);

        var daemon = _host.Starts[0].Env;
        Assert.Equal(Path.Combine(_host.DirectoryOf(), "home"), daemon["AGENT_DEVICE_HOME"]);
        Assert.False(daemon.ContainsKey("AGENT_DEVICE_DAEMON_BASE_URL"));
        Assert.Equal("ada", daemon["CLOUD_USERNAME"]);
    }

    [Fact]
    public async Task Prepares_the_daemons_home_before_it_starts()
    {
        string? prepared = null;
        var startedBefore = -1;

        await Leases().AcquireAsync(
            Request(),
            Allocation((home, _) =>
            {
                prepared = home;
                startedBefore = _host.Starts.Count;
                return File.WriteAllTextAsync(Path.Combine(home, "..", "prepared"), home);
            }),
            CancellationToken.None);

        Assert.Equal(_host.Starts[0].Env["AGENT_DEVICE_HOME"], prepared);
        Assert.Equal(prepared, await File.ReadAllTextAsync(Path.Combine(_host.DirectoryOf(), "prepared")));
        Assert.Equal(0, startedBefore);
    }

    [Fact]
    public async Task Allocates_nothing_once_the_run_is_interrupted()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leases().AcquireAsync(Request(), Allocation(), cancelled.Token));

        Assert.Empty(_host.Starts);
    }

    [Fact]
    public async Task Stops_the_daemon_of_a_lease_granted_after_an_interrupt_instead_of_handing_it_over()
    {
        using var cancelled = new CancellationTokenSource();
        _host.NextStarts.Enqueue(async _ =>
        {
            await cancelled.CancelAsync();
            return new DriverGrant("lease-1", "cloud", "run-1", true);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Leases().AcquireAsync(Request(), Allocation(), cancelled.Token));

        var stop = Assert.Single(_host.Stops);
        Assert.Equal((_host.Starts[0].Request.StateDirectory, "lease-1", "Cloud"), (stop.StateDirectory, stop.LeaseId, stop.Label));
        Assert.Empty(_host.Heartbeats);
        Assert.False(Directory.Exists(_host.DirectoryOf()));
    }

    [Fact]
    public async Task Stops_the_daemon_when_agent_device_does_not_acknowledge_retainOnClose()
    {
        _host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("lease-1", "cloud", "run-1", false)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Leases().AcquireAsync(Request(), Allocation(), CancellationToken.None));

        Assert.Equal("agent-device did not acknowledge retainOnClose. Closing the engine session could end the Cloud lease before run-end release", error.Message);
        Assert.Single(_host.Stops);
    }

    [Fact]
    public async Task Stops_the_daemon_after_a_failed_allocation_or_home_setup_and_reports_a_failed_cleanup_with_the_cause()
    {
        _host.NextStarts.Enqueue(_ => throw new InvalidOperationException("no capacity"));
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => Leases().AcquireAsync(Request(), Allocation(), CancellationToken.None));
        Assert.Equal("no capacity", failed.Message);
        var stop = Assert.Single(_host.Stops);

        // Nothing was granted, so a stop that finds no daemon has nothing to confirm.
        Assert.Equal((_host.Starts[0].Request.StateDirectory, (string?)null, "Cloud"), (stop.StateDirectory, stop.LeaseId, stop.Label));
        Assert.Empty(_host.Heartbeats);

        _host.NextStarts.Enqueue(_ => throw new InvalidOperationException("no capacity"));
        _host.NextStops.Enqueue((_, _) => throw new InvalidOperationException("daemon did not stop"));
        var both = await Assert.ThrowsAsync<AggregateException>(() => Leases().AcquireAsync(Request(), Allocation(), CancellationToken.None));
        Assert.StartsWith("Cloud lease failed: no capacity; cleanup also failed: daemon did not stop", both.Message, StringComparison.Ordinal);
        Assert.Equal(["no capacity", "daemon did not stop"], both.InnerExceptions.Select(inner => inner.Message));

        var home = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Leases().AcquireAsync(Request(), Allocation((_, _) => throw new InvalidOperationException("plugin missing")), CancellationToken.None));
        Assert.Equal("plugin missing", home.Message);
        Assert.Equal(2, _host.Starts.Count);
        Assert.Equal(3, _host.Stops.Count);
    }

    [Fact]
    public async Task Stops_the_heartbeat_and_the_daemon_and_removes_its_directory_once_however_often_it_is_asked()
    {
        var provider = Leases();
        var lease = await provider.AcquireAsync(Request(), Allocation(), CancellationToken.None);
        using var token = new CancellationTokenSource();

        await Task.WhenAll(provider.ReleaseAsync(lease, token.Token), provider.ReleaseAsync(lease, token.Token));
        await provider.ReleaseAsync(lease, token.Token);

        Assert.Equal(1, _host.HeartbeatsStopped);
        var stop = Assert.Single(_host.Stops);
        Assert.Equal((_host.Starts[0].Request.StateDirectory, "lease-1", "Cloud"), (stop.StateDirectory, stop.LeaseId, stop.Label));
        Assert.False(Directory.Exists(_host.DirectoryOf()));
    }

    [Fact]
    public async Task Tries_a_release_again_after_one_failed()
    {
        var provider = Leases();
        var lease = await provider.AcquireAsync(Request(), Allocation(), CancellationToken.None);
        _host.NextStops.Enqueue((_, _) => throw new InvalidOperationException("daemon busy"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReleaseAsync(lease, CancellationToken.None));
        await provider.ReleaseAsync(lease, CancellationToken.None);

        Assert.Equal("daemon busy", error.Message);
        Assert.Equal(2, _host.Stops.Count);
    }

    [Fact]
    public async Task Keeps_failing_to_release_a_lease_whose_session_end_is_unknown_once_its_daemon_is_gone()
    {
        static string Stop(object data) => System.Text.Json.JsonSerializer.Serialize(new { success = true, data });
        var forced = Stop(new { stopped = true, mode = "forced", providerReleases = new { status = "unknown", released = Array.Empty<object>(), pending = (object?)null }, warnings = Array.Empty<string>() });
        var notRunning = Stop(new { stopped = false, mode = "not-running", providerReleases = new { status = "completed", released = Array.Empty<object>(), pending = Array.Empty<object>() }, warnings = Array.Empty<string>() });
        _host.Stop = (leaseId, count) =>
        {
            Mobile.Lease.LeaseDaemon.AssertSessionsEnded(count == 1 ? forced : notRunning, leaseId, "Cloud");
            return Task.CompletedTask;
        };
        var provider = Leases();
        var lease = await provider.AcquireAsync(Request(), Allocation(), CancellationToken.None);
        var directory = _host.DirectoryOf();

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReleaseAsync(lease, CancellationToken.None));
        Assert.Contains("agent-device could not confirm its Cloud sessions ended", first.Message, StringComparison.Ordinal);

        // The forced stop killed the daemon, so every later stop finds none: the release never resolves without evidence.
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReleaseAsync(lease, CancellationToken.None));
        Assert.Equal("agent-device found no daemon running for lease lease-1, so nothing confirms its Cloud session ended", second.Message);
        var third = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReleaseAsync(lease, CancellationToken.None));
        Assert.Contains("agent-device found no daemon running for lease lease-1", third.Message, StringComparison.Ordinal);
        Assert.Equal(3, _host.Stops.Count);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public async Task Cleans_up_without_stopping_again_when_the_stop_confirmed_the_session_ended_but_the_cleanup_failed()
    {
        var provider = Leases();
        var lease = await provider.AcquireAsync(Request(), Allocation(), CancellationToken.None);
        _host.NextDeleteFailures.Enqueue(new IOException("EBUSY: resource busy or locked"));

        var error = await Assert.ThrowsAsync<IOException>(() => provider.ReleaseAsync(lease, CancellationToken.None));
        await provider.ReleaseAsync(lease, CancellationToken.None);

        Assert.Contains("EBUSY", error.Message, StringComparison.Ordinal);
        Assert.Single(_host.Stops);
        Assert.Equal(2, _host.Deletes);
        Assert.False(Directory.Exists(_host.DirectoryOf()));
    }

    [Fact]
    public async Task Ignores_a_lease_it_does_not_hold()
    {
        await Leases().ReleaseAsync(new DeviceLease { Id = "other", Client = new JsonObject { ["stateDir"] = "/tmp/x" } }, CancellationToken.None);

        Assert.Empty(_host.Stops);
    }

    private static Dictionary<string, string> Snapshot() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? "");
}
