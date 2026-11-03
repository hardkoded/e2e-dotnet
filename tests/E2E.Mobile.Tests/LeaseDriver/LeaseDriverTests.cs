// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile.Lease;

namespace E2E.Mobile.Tests.LeaseDriver;

/// <summary>Ports <c>packages/mobile/tests/unit/lease-driver.test.ts</c>, which runs the driver module in a child process; the port runs it in the runner.</summary>
public sealed class LeaseDriverTests
{
    private static readonly JsonObject Allocate = new()
    {
        ["tenant"] = "browserstack",
        ["runId"] = "run",
        ["leaseProvider"] = "browserstack",
        ["retainOnClose"] = true,
    };

    [Fact]
    public async Task Allocates_from_the_daemon_in_its_state_directory_and_reports_the_grant()
    {
        var client = new FakeDeviceClient();
        client.Respond(DeviceMethods.LeasesAllocate, _ => new JsonObject { ["leaseId"] = "lease", ["tenantId"] = "browserstack", ["runId"] = "run", ["backend"] = "ios-instance", ["retainOnClose"] = true });
        DeviceConnection? connection = null;
        string? session = null;

        var grant = await Mobile.Lease.LeaseDriver.AllocateAsync(
            new DriverRequest("/daemon", Allocate),
            (named, config) =>
            {
                session = named;
                connection = config;
                return client;
            },
            CancellationToken.None);

        Assert.Equal(new DriverGrant("lease", "browserstack", "run", true), grant);
        JsonAssert.Args("""{"stateDir":"/daemon"}""", connection!.Client);
        Assert.Matches("^e2e-lease-[0-9a-f]{8}$", session);
        JsonAssert.Args("""{"tenant":"browserstack","runId":"run","leaseProvider":"browserstack","retainOnClose":true}""", client.LastArgs(DeviceMethods.LeasesAllocate));
    }

    [Fact]
    public async Task Reports_a_daemon_that_did_not_keep_the_lease()
    {
        var client = new FakeDeviceClient();
        client.Respond(DeviceMethods.LeasesAllocate, _ => new JsonObject { ["leaseId"] = "lease", ["tenantId"] = "browserstack", ["runId"] = "run", ["backend"] = "ios-instance" });

        var grant = await Mobile.Lease.LeaseDriver.AllocateAsync(new DriverRequest("/daemon", Allocate), (_, _) => client, CancellationToken.None);

        Assert.False(grant.RetainOnClose);
    }

    [Fact]
    public async Task Reports_an_allocation_error()
    {
        var client = new FakeDeviceClient();
        client.RespondAsync(DeviceMethods.LeasesAllocate, (_, _) => throw new InvalidOperationException("No device named Pixel 99"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Mobile.Lease.LeaseDriver.AllocateAsync(new DriverRequest("/daemon", Allocate), (_, _) => client, CancellationToken.None));

        Assert.Equal("No device named Pixel 99", error.Message);
    }
}
