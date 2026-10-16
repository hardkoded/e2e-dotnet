// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using E2E.Engine;
using static E2E.Kernel.Tests.Kernel.KernelFixtures;

namespace E2E.Kernel.Tests.Kernel;

/// <summary>
/// <c>describe('kernel()')</c> of upstream <c>kernel.test.ts</c>, against a <see cref="FakeKernel"/> in place of the mocked
/// <c>@onkernel/sdk</c>: the create body and its tags, the live view line, delete on release and after a cancelled request, a
/// browser Kernel no longer knows, sweeping, and downloads.
/// </summary>
public sealed class KernelTests
{
    [Fact]
    public void Is_a_browser_provider_named_kernel_with_the_scope_it_was_given()
    {
        var fake = new FakeKernel();
        Assert.Equal("kernel", Provider(fake).Name);
        Assert.Equal(BrowserProviderScope.Worker, Provider(fake).Scope);
        Assert.Equal(BrowserProviderScope.Attempt, Provider(fake, new KernelOptions { Scope = BrowserProviderScope.Attempt }).Scope);
    }

    [Fact]
    public async Task Creates_a_browser_with_the_run_tags_and_the_idle_timeout_backstop_and_logs_its_live_view()
    {
        var fake = new FakeKernel();
        var lines = new List<string>();

        var lease = await Provider(fake, new KernelOptions { Stealth = true, Tags = new Dictionary<string, string> { ["team"] = "qa" } })
            .AcquireAsync(Request(lines, slot: 1), CancellationToken.None);

        Assert.Equal("b1", lease.Id);
        Assert.Equal("wss://kernel/b1", lease.CdpEndpoint);
        var create = Assert.Single(fake.Of("create"));
        AssertJson(
            new JsonObject
            {
                ["stealth"] = true,
                ["timeout_seconds"] = 600,
                ["tags"] = new JsonObject { ["team"] = "qa", ["e2e_run"] = "run-1", ["e2e_target"] = "web", ["e2e_slot"] = "1" },
            },
            create.Body);
        Assert.Equal(["browser b1, watch at https://view/b1"], lines);
    }

    [Fact]
    public async Task Keeps_an_explicit_idle_timeout_tags_a_per_attempt_lease_with_its_attempt_and_never_passes_scope_to_Kernel()
    {
        var fake = new FakeKernel();

        await Provider(fake, new KernelOptions { Scope = BrowserProviderScope.Attempt, TimeoutSeconds = 60 }).AcquireAsync(Request(attemptId: "a7"), CancellationToken.None);

        AssertJson(
            new JsonObject
            {
                ["timeout_seconds"] = 60,
                ["tags"] = new JsonObject { ["e2e_run"] = "run-1", ["e2e_target"] = "web", ["e2e_slot"] = "0", ["e2e_attempt"] = "a7" },
            },
            Assert.Single(fake.Of("create")).Body);
    }

    [Fact]
    public async Task Logs_the_session_alone_when_Kernel_has_no_live_view_for_it()
    {
        var fake = new FakeKernel { Create = () => FakeKernel.Json(new JsonObject { ["session_id"] = "headless", ["cdp_ws_url"] = "wss://kernel/headless" }, HttpStatusCode.Created) };
        var lines = new List<string>();

        await Provider(fake, new KernelOptions { Headless = true }).AcquireAsync(Request(lines), CancellationToken.None);

        Assert.Equal(["browser headless"], lines);
    }

    [Fact]
    public async Task Deletes_the_browser_on_release_and_a_browser_Kernel_no_longer_knows_counts_as_deleted()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake);
        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);

        await provider.ReleaseAsync(lease, ReleaseContext(), CancellationToken.None);
        Assert.Equal(["b1"], fake.Deleted);

        fake.Delete = _ => FakeKernel.Text(HttpStatusCode.NotFound, "gone");
        await provider.ReleaseAsync(lease, ReleaseContext(), CancellationToken.None);
    }

    [Fact]
    public async Task Surfaces_any_other_delete_failure()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake);
        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);
        fake.Delete = _ => FakeKernel.Text(HttpStatusCode.InternalServerError, "internal");

        var failure = await Assert.ThrowsAsync<KernelApiException>(() => provider.ReleaseAsync(lease, ReleaseContext(), CancellationToken.None));

        Assert.Contains("500 internal", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deletes_a_browser_that_arrived_after_the_request_was_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        var fake = new FakeKernel();
        fake.Create = () =>
        {
            cancelled.Cancel();
            return fake.NextBrowser();
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(fake).AcquireAsync(Request(), cancelled.Token));

        Assert.Contains("browser b1 arrived after the request was cancelled; deleted", failure.Message, StringComparison.Ordinal);
        Assert.Equal(["b1"], fake.Deleted);
    }

    [Fact]
    public async Task Says_so_when_a_browser_that_arrived_after_cancellation_could_not_be_deleted()
    {
        using var cancelled = new CancellationTokenSource();
        var fake = new FakeKernel { Delete = _ => FakeKernel.Text(HttpStatusCode.ServiceUnavailable, "unavailable") };
        fake.Create = () =>
        {
            cancelled.Cancel();
            return fake.NextBrowser();
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(fake).AcquireAsync(Request(), cancelled.Token));

        Assert.Equal("browser b1 arrived after the request was cancelled; deleting it failed, so Kernel ends it after timeout_seconds: 503 unavailable", failure.Message);
    }

    [Fact]
    public async Task Fails_without_an_API_key_in_the_run_environment_and_reuses_one_SDK_client_per_key_PARTIAL_there_is_no_SDK_client_to_reuse_so_each_request_carries_the_key_of_its_run()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AcquireAsync(Request(env: new Dictionary<string, string?> { ["KERNEL_API_KEY"] = "  " }), CancellationToken.None));
        Assert.Equal("KERNEL_API_KEY is not set", failure.Message);
        Assert.Empty(fake.Calls);

        await provider.AcquireAsync(Request(), CancellationToken.None);
        await provider.AcquireAsync(Request(slot: 1), CancellationToken.None);
        await provider.AcquireAsync(Request(env: new Dictionary<string, string?> { ["KERNEL_API_KEY"] = "k-other" }), CancellationToken.None);

        Assert.Equal(["Bearer k-test", "Bearer k-test", "Bearer k-other"], fake.Of("create").Select(call => call.Authorization));
    }

    [Fact]
    public async Task Surfaces_a_Kernel_failure_as_is()
    {
        var fake = new FakeKernel { Create = () => FakeKernel.Text(HttpStatusCode.Unauthorized, "invalid api key") };

        var failure = await Assert.ThrowsAsync<KernelApiException>(() => Provider(fake).AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("401 invalid api key", failure.Message);
    }

    [Fact]
    public async Task Trims_the_newline_Kernel_ends_an_error_body_with_keeping_the_error()
    {
        var fake = new FakeKernel { Create = () => FakeKernel.Text(HttpStatusCode.Unauthorized, "Invalid or disabled API key\n") };

        var failure = await Assert.ThrowsAsync<KernelApiException>(() => Provider(fake).AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, failure.Status);
        Assert.Equal("401 Invalid or disabled API key", failure.Message);
    }

    [Fact]
    public async Task Has_each_browser_save_downloads_to_its_own_disk_and_reads_a_finished_one_back_through_the_browser_filesystem()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake);
        Assert.Equal("/tmp/e2e-downloads", provider.Downloads?.Dir);
        var context = new BrowserDownloadContext { RunId = "run-1", TargetName = "web", Env = Env };

        var bytes = await provider.Downloads!.ReadAsync(Lease(), "/tmp/e2e-downloads/guid-1", context, CancellationToken.None);

        Assert.Equal("file bytes", Encoding.UTF8.GetString(bytes));
        var read = Assert.Single(fake.Of("readFile"));
        Assert.Equal("b1", read.Id);
        Assert.Equal("/tmp/e2e-downloads/guid-1", HttpUtility.ParseQueryString(read.Uri.Query)["path"]);
    }

    [Fact]
    public async Task Sweeps_the_runs_active_browsers_for_the_target_deletes_each_and_names_only_those_Kernel_still_knew()
    {
        var fake = new FakeKernel { Delete = id => id == "b8" ? FakeKernel.Text(HttpStatusCode.NotFound, "gone") : new HttpResponseMessage(HttpStatusCode.NoContent) };
        fake.Active.AddRange(["b7", "b8"]);

        var swept = await Provider(fake).SweepAsync(ReleaseContext(), CancellationToken.None);

        Assert.Equal(["b7"], swept);
        var query = HttpUtility.ParseQueryString(Assert.Single(fake.Of("list")).Uri.Query);
        Assert.Equal(["status", "tags[e2e_run]", "tags[e2e_target]"], query.AllKeys.OfType<string>());
        Assert.Equal(["active", "run-1", "web"], query.AllKeys.OfType<string>().Select(key => query[key]));
        Assert.Equal(["b7", "b8"], fake.Deleted.Order());
    }

    [Fact]
    public async Task Deletes_every_browser_the_sweep_found_even_when_one_delete_fails_and_names_both()
    {
        var fake = new FakeKernel { Delete = id => id == "b8" ? FakeKernel.Text(HttpStatusCode.InternalServerError, "internal") : new HttpResponseMessage(HttpStatusCode.NoContent) };
        fake.Active.AddRange(["b7", "b8", "b9"]);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(fake).SweepAsync(ReleaseContext(), CancellationToken.None));

        Assert.Equal("deleted b7, b9; could not delete b8 (500 internal), so Kernel ends it after timeout_seconds", failure.Message);
        Assert.Equal(["b7", "b8", "b9"], fake.Deleted.Order());
    }
}
