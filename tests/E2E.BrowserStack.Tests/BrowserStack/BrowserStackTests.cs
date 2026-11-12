// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;
using E2E.Mobile.Lease;

namespace E2E.BrowserStack.Tests.BrowserStack;

/// <summary>
/// Ports <c>describe('browserstack()')</c> of <c>packages/browserstack/tests/unit/browserstack.test.ts</c>: the options it refuses, the
/// selectors and credentials it leases with, and the app upload, against a stubbed lease helper and a fake App Automate API. The lease
/// lifecycle is <c>E2E.Mobile</c>'s, tested there. <c>Recording/</c> covers <c>RecordAsync</c>.
/// </summary>
public sealed class BrowserStackTests
{
    private const string Root = "/project";

    private static readonly Dictionary<string, string?> Env = new() { ["BROWSERSTACK_USERNAME"] = "ada", ["BROWSERSTACK_ACCESS_KEY"] = "bs-key", ["PATH"] = "/bin" };

    private static readonly BrowserStackDevicesOptions Options = new() { Device = "Google Pixel 9", OsVersion = "16.0", App = "bs://APP0" };

    private readonly FakeLeases _leases = new();
    private readonly FakeAppAutomate _api = new();
    private AgentDeviceLeaseOptions? _leaseOptions;

    private BrowserStackDevices Provider(BrowserStackDevicesOptions options) => new(options, leaseOptions =>
    {
        _leaseOptions = leaseOptions;
        return _leases;
    }, _api.Client());

    private static DeviceRequest Request(
        MobilePlatform platform = MobilePlatform.Android,
        string runId = "run-1",
        string targetName = "android",
        int slot = 0,
        int slots = 2,
        string? appPath = null,
        IReadOnlyDictionary<string, string?>? env = null,
        Action<string>? log = null) => new()
        {
            Platform = platform,
            RunId = runId,
            TargetName = targetName,
            Slot = slot,
            Slots = slots,
            App = "com.example.app",
            AppPath = appPath,
            AgentDeviceVersion = AgentDeviceVersion.Pinned,
            ProjectRoot = Root,
            Env = env ?? Env,
            Log = log ?? (_ => { }),
        };

    private static DeviceReleaseContext Context() => new() { RunId = "run-1", TargetName = "android", Env = Env, Log = _ => { } };

    private static ConfigurationException Invalid(Action create)
    {
        var error = Assert.Throws<ConfigurationException>(create);
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error;
    }

    [Fact]
    public void Is_a_device_provider_named_browserstack_that_leases_through_agent_devices_browserstack_provider_and_records_BrowserStacks_session_video()
    {
        var provider = Provider(Options);

        Assert.Equal("browserstack", provider.Name);
        Assert.Equal(new AgentDeviceLeaseOptions { Provider = "browserstack", Label = "BrowserStack" }, _leaseOptions);
        Assert.IsAssignableFrom<IDeviceProvider>(provider);
    }

    [Fact]
    public async Task Leases_with_the_device_selectors_dashboard_labels_the_runs_credentials_and_a_summary_of_the_session()
    {
        var provider = Provider(Options);
        var request = Request(slot: 1);

        var lease = await provider.AcquireAsync(request, CancellationToken.None);

        var (seen, leased) = Assert.Single(_leases.Acquired);
        Assert.Same(request, seen);
        Assert.Equal(new Dictionary<string, string> { ["BROWSERSTACK_USERNAME"] = "ada", ["BROWSERSTACK_ACCESS_KEY"] = "bs-key" }, leased.Env);
        Assert.Equal("Google Pixel 9, android 16.0; session e2e-run-1-android-2 reserved", leased.Summary);
        Assert.Equal(
            new LeaseSelectors
            {
                Device = "Google Pixel 9",
                ProviderOsVersion = "16.0",
                ProviderApp = "bs://APP0",
                ProviderProject = "e2e",
                ProviderBuild = "run-1",
                ProviderSessionName = "e2e-run-1-android-2",
            },
            leased.Selectors);
        await provider.ReleaseAsync(lease, Context(), CancellationToken.None);
        Assert.Same(lease, Assert.Single(_leases.Released));
    }

    [Fact]
    public async Task Leases_an_iOS_device_with_its_own_project_build_and_session_name()
    {
        await Provider(new BrowserStackDevicesOptions { Device = "iPhone 16", OsVersion = "18", App = "bs://APP123", Project = "shop", Build = "nightly", SessionName = "checkout" })
            .AcquireAsync(Request(MobilePlatform.Ios), CancellationToken.None);

        Assert.Equal(
            new LeaseSelectors
            {
                Device = "iPhone 16",
                ProviderOsVersion = "18",
                ProviderApp = "bs://APP123",
                ProviderProject = "shop",
                ProviderBuild = "nightly",
                ProviderSessionName = "checkout-1",
            },
            _leases.Selectors());
    }

    [Fact]
    public async Task Names_each_slots_session_after_the_run_the_target_and_the_slot_and_keeps_a_given_name_unique_per_slot()
    {
        var provider = Provider(Options);
        await provider.AcquireAsync(Request(runId: "run-7", targetName: "pixel", slot: 0, slots: 3), CancellationToken.None);
        await provider.AcquireAsync(Request(runId: "run-7", targetName: "pixel", slot: 2, slots: 3), CancellationToken.None);
        await Provider(Options with { SessionName = "checkout" }).AcquireAsync(Request(slot: 2, slots: 3), CancellationToken.None);
        await Provider(Options with { SessionName = "checkout" }).AcquireAsync(Request(slot: 0, slots: 1), CancellationToken.None);

        Assert.Equal(["e2e-run-7-pixel-1", "e2e-run-7-pixel-3", "checkout-3", "checkout"], _leases.Acquired.Select(call => call.Lease.Selectors!.ProviderSessionName!).ToArray());
    }

    [Fact]
    public async Task Passes_the_device_features_to_the_allocation_and_the_worker_under_agent_devices_keys()
    {
        var lease = await Provider(Options with
        {
            Orientation = BrowserStackOrientation.Landscape,
            GeoLocation = "US",
            Timezone = "New_York",
            Language = "fr",
            Locale = "fr_FR",
            AppiumVersion = "2.16.2",
        }).AcquireAsync(Request(), CancellationToken.None);

        var selectors = _leases.Selectors();
        Assert.Equal(
            ("landscape", "US", "New_York", "fr", "fr_FR", "2.16.2"),
            (selectors.ProviderDeviceOrientation, selectors.ProviderGeoLocation, selectors.ProviderTimezone, selectors.ProviderLanguage, selectors.ProviderLocale, selectors.ProviderAppiumVersion));
        Assert.Equal("landscape", lease.Client!["providerDeviceOrientation"]!.GetValue<string>());
        Assert.Equal("fr_FR", lease.Client["providerLocale"]!.GetValue<string>());
        Assert.Equal("2.16.2", lease.Client["providerAppiumVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task Resolves_a_local_build_against_the_project_root_never_the_working_directory()
    {
        await Provider(Options with { App = "build/app.apk" }).AcquireAsync(Request(), CancellationToken.None);
        Assert.Equal(Path.GetFullPath(Path.Combine("build", "app.apk"), Root), _leases.Selectors(0).ProviderApp);

        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "builds", "app.apk"));
        await Provider(Options with { App = absolute }).AcquireAsync(Request(), CancellationToken.None);
        Assert.Equal(absolute, _leases.Selectors(1).ProviderApp);
    }

    [Theory]
    [InlineData(null, null, "BROWSERSTACK_USERNAME and BROWSERSTACK_ACCESS_KEY are not set")]
    [InlineData("ada", null, "BROWSERSTACK_ACCESS_KEY is not set")]
    [InlineData("  ", "bs-key", "BROWSERSTACK_USERNAME is not set")]
    public async Task Leases_nothing_without_credentials_in_the_runs_environment(string? username, string? accessKey, string message)
    {
        var env = new Dictionary<string, string?> { ["BROWSERSTACK_USERNAME"] = username, ["BROWSERSTACK_ACCESS_KEY"] = accessKey };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(Options).AcquireAsync(Request(env: env), CancellationToken.None));

        Assert.Equal($"{message}; set BROWSERSTACK_USERNAME and BROWSERSTACK_ACCESS_KEY to your BrowserStack username and access key in the environment the tests start in", error.Message);
        Assert.Empty(_leases.Acquired);
    }

    [Fact]
    public async Task Refuses_the_targets_app_appPath_since_BrowserStack_installs_App()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Provider(Options).AcquireAsync(Request(appPath: Path.Combine(Root, "build", "app.apk")), CancellationToken.None));

        Assert.Equal("BrowserStack installs the app from `App`; leave the target's `app.appPath` out", error.Message);
        Assert.Empty(_leases.Acquired);
    }

    [Fact]
    public async Task Leases_nothing_once_the_run_is_interrupted()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(Options).AcquireAsync(Request(), cancelled.Token));

        Assert.Empty(_leases.Acquired);
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("OsVersion")]
    [InlineData("App")]
    public void Requires_the_option_with_INVALID_CONFIG(string key)
    {
        BrowserStackDevicesOptions With(string? value) => key switch
        {
            "Device" => Options with { Device = value! },
            "OsVersion" => Options with { OsVersion = value! },
            _ => Options with { App = value! },
        };

        Assert.Equal($"BrowserStackDevices: `{key}` is required, as a non-empty string", Invalid(() => Provider(With(" "))).Message);
        Invalid(() => Provider(With(null)));
    }

    [Fact]
    public void Refuses_an_orientation_other_than_portrait_or_landscape()
    {
        var error = Invalid(() => Provider(Options with { Orientation = (BrowserStackOrientation)7 }));

        Assert.Equal("BrowserStackDevices: `Orientation` must be Portrait or Landscape, not 7", error.Message);
    }

    [Theory]
    [InlineData("GeoLocation")]
    [InlineData("Timezone")]
    [InlineData("Language")]
    [InlineData("Locale")]
    [InlineData("AppiumVersion")]
    [InlineData("NetworkProfile")]
    [InlineData("CustomNetwork")]
    public void Partial_Refuses_an_empty_or_non_string_option_with_INVALID_CONFIG(string key)
    {
        var options = key switch
        {
            "GeoLocation" => Options with { GeoLocation = " " },
            "Timezone" => Options with { Timezone = " " },
            "Language" => Options with { Language = " " },
            "Locale" => Options with { Locale = " " },
            "AppiumVersion" => Options with { AppiumVersion = " " },
            "NetworkProfile" => Options with { NetworkProfile = " " },
            _ => Options with { CustomNetwork = " " },
        };

        Assert.Equal($"BrowserStackDevices: `{key}` must be a non-empty string", Invalid(() => Provider(options)).Message);
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("Build")]
    [InlineData("SessionName")]
    public void Partial_Refuses_an_empty_or_non_string_project_build_and_sessionName_with_INVALID_CONFIG(string key)
    {
        var options = key switch
        {
            "Project" => Options with { Project = " " },
            "Build" => Options with { Build = " " },
            _ => Options with { SessionName = " " },
        };

        Assert.Equal($"BrowserStackDevices: `{key}` must be a non-empty string", Invalid(() => Provider(options)).Message);
    }

    [Fact]
    public async Task Passes_a_network_profile_a_custom_network_and_noResignApp_on_iOS_under_agent_devices_keys()
    {
        await Provider(Options with { NetworkProfile = "4g-lte-advanced-good" }).AcquireAsync(Request(), CancellationToken.None);
        Assert.Equal("4g-lte-advanced-good", _leases.Selectors(0).ProviderNetworkProfile);
        await Provider(Options with { CustomNetwork = "1000" }).AcquireAsync(Request(), CancellationToken.None);
        Assert.Equal("1000", _leases.Selectors(1).ProviderCustomNetwork);
        await Provider(Options with { NoResignApp = true }).AcquireAsync(Request(MobilePlatform.Ios), CancellationToken.None);
        Assert.True(_leases.Selectors(2).ProviderNoResignApp);
        await Provider(Options with { NoResignApp = false }).AcquireAsync(Request(MobilePlatform.Ios), CancellationToken.None);
        Assert.Null(_leases.Selectors(3).ProviderNoResignApp);
        Assert.False(_leases.Selectors(3).ToJson().ContainsKey("providerNoResignApp"));
    }

    [Fact]
    public async Task Refuses_noResignApp_on_Android_before_any_daemon_starts()
    {
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Provider(Options with { NoResignApp = true }).AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("BrowserStackDevices: `NoResignApp` applies to iOS only", error.Message);
        Assert.Empty(_leases.Acquired);
    }

    [Fact]
    public void Partial_Refuses_a_network_profile_together_with_a_custom_network_and_a_non_boolean_noResignApp()
    {
        var error = Invalid(() => Provider(Options with { NetworkProfile = "3g", CustomNetwork = "1000" }));

        Assert.Equal("BrowserStackDevices: set `NetworkProfile` or `CustomNetwork`, not both", error.Message);
    }

    [Fact]
    public async Task Has_BrowserStack_fetch_an_https_build_once_per_run_and_leases_every_slot_with_its_bs_id()
    {
        var provider = Provider(Options with { App = "https://example.com/app.apk" });
        var lines = new List<string>();
        var request = Request(log: lines.Add);

        await Task.WhenAll(provider.AcquireAsync(request, CancellationToken.None), provider.AcquireAsync(Request(slot: 1), CancellationToken.None));

        var upload = Assert.Single(_api.Uploads);
        Assert.Equal("https://api-cloud.browserstack.com/app-automate/upload", upload.Url.AbsoluteUri);
        Assert.Equal($"Basic {Convert.ToBase64String("ada:bs-key"u8.ToArray())}", upload.Authorization);
        Assert.Contains("https://example.com/app.apk", upload.Body, StringComparison.Ordinal);
        Assert.Equal(["bs://UPLOADED", "bs://UPLOADED"], [_leases.Selectors(0).ProviderApp!, _leases.Selectors(1).ProviderApp!]);
        Assert.Contains("uploaded the app URL as bs://UPLOADED", lines);
        Assert.DoesNotContain("example.com", string.Join('\n', lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_leasing_when_the_runs_log_throws_on_the_upload_line()
    {
        var provider = Provider(Options with { App = "https://example.com/app.apk" });

        await provider.AcquireAsync(Request(log: _ => throw new InvalidOperationException("log closed")), CancellationToken.None);

        Assert.Equal("bs://UPLOADED", _leases.Selectors().ProviderApp);
    }

    [Fact]
    public async Task Uploads_an_https_build_again_for_the_next_run_and_for_another_account_or_API_endpoint()
    {
        var provider = Provider(Options with { App = "https://example.com/latest.apk" });
        foreach (var id in new[] { "bs://RUN1", "bs://RUN2", "bs://EU", "bs://BOB" })
        {
            _api.UploadIds.Enqueue(id);
        }

        var first = await provider.AcquireAsync(Request(runId: "run-1"), CancellationToken.None);
        await provider.ReleaseAsync(first, Context(), CancellationToken.None);
        await Task.WhenAll(provider.AcquireAsync(Request(runId: "run-2"), CancellationToken.None), provider.AcquireAsync(Request(runId: "run-2", slot: 1), CancellationToken.None));
        await provider.AcquireAsync(Request(runId: "run-2", env: new Dictionary<string, string?>(Env) { ["BROWSERSTACK_API_ENDPOINT"] = "https://eu.example.com/app-automate" }), CancellationToken.None);
        await provider.AcquireAsync(Request(runId: "run-2", env: new Dictionary<string, string?>(Env) { ["BROWSERSTACK_USERNAME"] = "bob" }), CancellationToken.None);

        Assert.Equal(4, _api.Uploads.Count());
        Assert.Equal(["bs://RUN1", "bs://RUN2", "bs://RUN2", "bs://EU", "bs://BOB"], _leases.Acquired.Select(call => call.Lease.Selectors!.ProviderApp!).ToArray());
    }

    [Fact]
    public async Task Leases_nothing_when_the_upload_fails_and_uploads_again_on_the_next_lease()
    {
        var provider = Provider(Options with { App = "https://example.com/app.apk" });
        _api.Overrides.Enqueue(() => FakeAppAutomate.Json(new System.Text.Json.Nodes.JsonObject { ["message"] = "Invalid file" }, System.Net.HttpStatusCode.UnprocessableEntity));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.AcquireAsync(Request(), CancellationToken.None));
        Assert.Contains("HTTP 422 (Invalid file)", error.Message, StringComparison.Ordinal);
        Assert.Empty(_leases.Acquired);

        await provider.AcquireAsync(Request(), CancellationToken.None);

        Assert.Equal(2, _api.Uploads.Count());
        Assert.Equal("bs://UPLOADED", _leases.Selectors().ProviderApp);
    }

    [Fact]
    public async Task Leases_nothing_when_the_run_is_interrupted_during_the_upload()
    {
        using var cancelled = new CancellationTokenSource();
        _api.OnUpload = async () => await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Provider(Options with { App = "https://example.com/app.apk" }).AcquireAsync(Request(), cancelled.Token));

        Assert.Empty(_leases.Acquired);
    }

    [Fact]
    public async Task Passes_a_bs_id_or_a_local_path_through_without_uploading()
    {
        await Provider(Options).AcquireAsync(Request(), CancellationToken.None);
        await Provider(Options with { App = "build/app.apk" }).AcquireAsync(Request(), CancellationToken.None);

        Assert.Empty(_api.Uploads);
    }
}
