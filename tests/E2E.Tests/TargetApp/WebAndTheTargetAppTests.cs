// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.TargetApp;

/// <summary>The app under test is the target's: <c>web()</c> checks the target's <c>app</c> for what a browser needs.</summary>
public sealed class WebAndTheTargetAppTests
{
    [Fact]
    public void Needs_app_url_on_the_target_and_refuses_the_fields_of_an_installed_device_app()
    {
        var web = new WebEngine();
        const string name = "chromium";
        ValidateWeb(web, new AppConfig { Url = "http://localhost:3000" }, name);
        var missing = Assert.Throws<ConfigurationException>(() => ValidateWeb(web, new AppConfig(), name));
        Assert.Equal("INVALID_CONFIG", missing.Code);
        Assert.Contains("target \"chromium\" needs app.url", missing.Message, StringComparison.Ordinal);
        foreach (var key in new[] { "bundleId", "appPath", "launchArguments" })
        {
            var app = key switch
            {
                "bundleId" => new AppConfig { Url = "http://localhost:3000", BundleId = "x" },
                "appPath" => new AppConfig { Url = "http://localhost:3000", AppPath = "x" },
                _ => new AppConfig { Url = "http://localhost:3000", LaunchArguments = ["-x"] },
            };
            var error = Assert.Throws<ConfigurationException>(() => ValidateWeb(web, app, name));
            Assert.Equal(
                $"target \"chromium\" declares app.{key}, which describes an installed device app; a web() target opens app.url",
                error.Message
            );
        }
    }

    [Fact]
    public void Takes_the_permissions_its_browser_grants_and_grants_only()
    {
        const string url = "http://localhost:3000";
        static AppConfig Of(string name, AppPermissionState state) =>
            new()
            {
                Url = url,
                Permissions = new Dictionary<string, AppPermissionState> { [name] = state },
            };
        static ConfigurationException Refused(WebEngine engine, AppConfig app)
        {
            var error = Assert.Throws<ConfigurationException>(() => ValidateWeb(engine, app, "web"));
            Assert.Equal("INVALID_CONFIG", error.Code);
            return error;
        }

        ValidateWeb(
            new WebEngine(),
            new AppConfig
            {
                Url = url,
                Permissions = new Dictionary<string, AppPermissionState>
                {
                    ["clipboard-read"] = AppPermissionState.Grant,
                    ["clipboard-write"] = AppPermissionState.Grant,
                },
            },
            "web"
        );
        ValidateWeb(
            new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Webkit }),
            Of("clipboard-read", AppPermissionState.Grant),
            "web"
        );
        Assert.Contains(
            "target \"web\" app.permissions on firefox has unknown key \"clipboard-read\"",
            Refused(
                new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Firefox }),
                Of("clipboard-read", AppPermissionState.Grant)
            ).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "did you mean \"clipboard-write\"",
            Refused(new WebEngine(), Of("clipboard-wirte", AppPermissionState.Grant)).Message,
            StringComparison.Ordinal
        );
        foreach (var state in new[] { AppPermissionState.Deny, AppPermissionState.Reset })
        {
            Assert.Equal(
                $"target \"web\" app.permissions.camera is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead",
                Refused(new WebEngine(), Of("camera", state)).Message
            );
        }
    }

    [Fact]
    public void Takes_all_on_every_browser_granting_each_permission_that_browser_grants()
    {
        foreach (var browser in new[] { BrowserName.Chromium, BrowserName.Firefox, BrowserName.Webkit })
        {
            ValidateWeb(
                new WebEngine(new WebEngineOptions { BrowserName = browser }),
                new AppConfig { Url = "http://localhost:3000", AllPermissions = true },
                "web"
            );
        }

        Assert.Equal(
            ["geolocation", "local-network-access", "notifications", "persistent-storage", "push", "screen-wake-lock"],
            WebPermissions.GrantedPermissions(BrowserName.Firefox, null, allPermissions: true)
        );
        Assert.Contains("clipboard-read", WebPermissions.GrantedPermissions(BrowserName.Chromium, null, true));
        Assert.Equal(
            ["camera"],
            WebPermissions.GrantedPermissions(
                BrowserName.Webkit,
                new Dictionary<string, AppPermissionState> { ["camera"] = AppPermissionState.Grant },
                false
            )
        );
        Assert.Empty(
            WebPermissions.GrantedPermissions(BrowserName.Webkit, new Dictionary<string, AppPermissionState>(), false)
        );
        Assert.True(((IEngine)new WebEngine()).GrantsAllPermissions);
    }

    [Fact]
    public async Task Leaves_out_for_all_only_a_permission_an_older_browser_refuses_as_unknown_and_grants_the_rest()
    {
        // An older Chrome over CDP does not know localNetworkAccess, and refuses the whole call over it.
        var held = new List<string>();
        var context = OlderChrome(held);
        var all = WebPermissions.GrantedPermissions(BrowserName.Chromium, null, allPermissions: true);
        await WebPermissions.GrantPermissionsAsync(context, all, skipUnknown: true);
        Assert.Contains("clipboard-read", held);
        Assert.DoesNotContain("local-network-access", held);
        Assert.Equal(all.Count - 1, held.Count);

        // A named grant keeps the refusal, since the user can drop the name.
        var error = await Assert.ThrowsAsync<PlaywrightException>(() =>
            WebPermissions.GrantPermissionsAsync(
                OlderChrome([]),
                ["geolocation", "local-network-access"],
                skipUnknown: false
            )
        );
        Assert.Contains("Unknown permission type: localNetworkAccess", error.Message, StringComparison.Ordinal);

        // Any other failure is not an unknown name: it propagates, whatever the list.
        var broken = BrowserContextDouble.Create(_ =>
            throw new PlaywrightException("Target page, context or browser has been closed")
        );
        var closed = await Assert.ThrowsAsync<PlaywrightException>(() =>
            WebPermissions.GrantPermissionsAsync(broken, all, skipUnknown: true)
        );
        Assert.Contains("has been closed", closed.Message, StringComparison.Ordinal);
    }

    private static IBrowserContext OlderChrome(List<string> held) =>
        BrowserContextDouble.Create(
            names =>
            {
                held.AddRange(names);
                if (held.Contains("local-network-access"))
                {
                    throw new PlaywrightException(
                        "browserContext.grantPermissions: Unknown permission type: localNetworkAccess"
                    );
                }
            },
            held.Clear
        );

    private static void ValidateWeb(WebEngine engine, AppConfig app, string targetName) =>
        ((IEngine)engine).ValidateApp(
            TargetApps.EngineAppDeclarationOf(app),
            new EngineAppCheckInfo { TargetName = targetName }
        );

    /// <summary>A browser context holding only <c>grantPermissions</c> and <c>clearPermissions</c>, the two calls upstream's fake context has.</summary>
    private class BrowserContextDouble : DispatchProxy
    {
        private Action<IReadOnlyList<string>> _grant = _ => { };
        private Action _clear = () => { };

        public static IBrowserContext Create(Action<IReadOnlyList<string>> grant, Action? clear = null)
        {
            var context = Create<IBrowserContext, BrowserContextDouble>();
            var double_ = (BrowserContextDouble)(object)context;
            double_._grant = grant;
            double_._clear = clear ?? (() => { });
            return context;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IBrowserContext.GrantPermissionsAsync):
                    _grant([.. (IEnumerable<string>)args![0]!]);
                    return Task.CompletedTask;
                case nameof(IBrowserContext.ClearPermissionsAsync):
                    _clear();
                    return Task.CompletedTask;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
