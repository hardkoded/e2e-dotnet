// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Mobile.Lease;

namespace E2E.Mobile.Tests.LeaseDaemon;

/// <summary>
/// Ports <c>packages/mobile/tests/unit/lease-daemon.test.ts</c>: the daemon stop a lease's release relies on, and the report it checks.
/// The first two <c>it</c>s run <c>agent-device daemon stop</c> for real, so they need Node and the pinned agent-device (through npx).
/// </summary>
public sealed class LeaseDaemonTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lease-daemon-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string Report(object data) => JsonSerializer.Serialize(new { success = true, data });

    private static string Graceful(object[] released, object[]? pending = null) =>
        Report(new { stopped = true, mode = "graceful", providerReleases = new { status = "completed", released, pending = pending ?? [] }, warnings = Array.Empty<string>() });

    private static readonly string NotRunning =
        Report(new { stopped = false, mode = "not-running", providerReleases = new { status = "completed", released = Array.Empty<object>(), pending = Array.Empty<object>() }, warnings = Array.Empty<string>() });

    private static Dictionary<string, string?> StopEnvironment() => new() { ["PATH"] = System.Environment.GetEnvironmentVariable("PATH"), ["HOME"] = System.Environment.GetEnvironmentVariable("HOME") };

    [Fact]
    public async Task Stops_a_daemon_that_is_not_running_without_error_before_a_lease_was_granted()
    {
        await new Mobile.Lease.LeaseDaemon().StopDriverAsync(Path.Combine(_directory, "state"), StopEnvironment(), null, "BrowserStack", CancellationToken.None);
    }

    [Fact]
    public async Task Refuses_to_confirm_a_granted_lease_ended_when_no_daemon_is_running_to_release_it()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Mobile.Lease.LeaseDaemon().StopDriverAsync(Path.Combine(_directory, "state"), StopEnvironment(), "lease-1", "BrowserStack", CancellationToken.None));

        Assert.Equal("agent-device found no daemon running for lease lease-1, so nothing confirms its BrowserStack session ended", error.Message);
    }

    [Fact]
    public void Accepts_a_graceful_stop_that_released_the_lease_and_any_graceful_stop_before_a_grant()
    {
        Mobile.Lease.LeaseDaemon.AssertSessionsEnded(Graceful([new { leaseId = "l1", provider = "browserstack" }]), "l1", "BrowserStack");
        Mobile.Lease.LeaseDaemon.AssertSessionsEnded(Graceful([]), null, "BrowserStack");
    }

    [Fact]
    public void Accepts_no_running_daemon_only_before_a_lease_was_granted()
    {
        Mobile.Lease.LeaseDaemon.AssertSessionsEnded(NotRunning, null, "BrowserStack");
        var error = Assert.Throws<InvalidOperationException>(() => Mobile.Lease.LeaseDaemon.AssertSessionsEnded(NotRunning, "l1", "BrowserStack"));
        Assert.Equal("agent-device found no daemon running for lease l1, so nothing confirms its BrowserStack session ended", error.Message);
    }

    [Fact]
    public void Refuses_a_graceful_stop_that_did_not_release_the_granted_lease_as_from_a_daemon_started_after_the_granting_one_died()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            Mobile.Lease.LeaseDaemon.AssertSessionsEnded(Graceful([new { leaseId = "l2", provider = "browserstack" }]), "l1", "BrowserStack"));
        Assert.Equal("agent-device stopped without listing lease l1 as released, so nothing confirms its BrowserStack session ended", error.Message);
    }

    [Fact]
    public void Refuses_a_report_with_sessions_still_pending()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            Mobile.Lease.LeaseDaemon.AssertSessionsEnded(Graceful([], [new { leaseId = "l1" }, new { leaseId = "l2" }]), null, "BrowserStack"));
        Assert.Equal("agent-device stopped without ending 2 BrowserStack session(s)", error.Message);
    }

    [Fact]
    public void Refuses_a_forced_stop_whose_cleanup_is_unknown_with_the_daemons_warning()
    {
        var warning = "The daemon was force-killed before provider lease state could be finalized. Provider allocations may remain active.";
        var forced = Report(new { stopped = true, mode = "forced", providerReleases = new { status = "unknown", released = Array.Empty<object>(), pending = (object?)null }, warnings = new[] { warning } });

        var error = Assert.Throws<InvalidOperationException>(() => Mobile.Lease.LeaseDaemon.AssertSessionsEnded(forced, "l1", "BrowserStack"));

        Assert.Equal($"agent-device could not confirm its BrowserStack sessions ended: {warning}", error.Message);
    }

    [Fact]
    public void Refuses_a_report_without_provider_releases_and_one_that_is_not_json()
    {
        var missing = Assert.Throws<InvalidOperationException>(() => Mobile.Lease.LeaseDaemon.AssertSessionsEnded(Report(new { stopped = true }), null, "BrowserStack"));
        Assert.Equal("agent-device could not confirm its BrowserStack sessions ended", missing.Message);
        var unreadable = Assert.Throws<InvalidOperationException>(() => Mobile.Lease.LeaseDaemon.AssertSessionsEnded("killed", null, "BrowserStack"));
        Assert.Equal("agent-device stopped without a readable shutdown report; BrowserStack sessions may still be running", unreadable.Message);
    }
}
