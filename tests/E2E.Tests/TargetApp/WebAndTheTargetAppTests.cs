// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Config;

namespace E2E.Tests.TargetApp;

/// <summary>The app under test is the target's: <c>web()</c> checks the target's <c>app</c> for what a browser needs.</summary>
public sealed class WebAndTheTargetAppTests
{
    /// <summary>
    /// Partial: the port has no <c>validateApp</c> on <see cref="WebEngine"/> to call (issue filed to extract it), so this checks what the
    /// config load refuses for a web target: the fields of an installed device app. The <c>app.url</c> requirement is not checked: the
    /// port's fixture supplies the URL, and a web target without one loads.
    /// </summary>
    [Fact]
    public void Partial_Needs_app_url_on_the_target_and_refuses_the_fields_of_an_installed_device_app()
    {
        _ = ResolveConfigTests.App("""{ "url": "http://localhost:3000" }""");
        foreach (var field in new[] { "\"bundleId\": \"x\"", "\"appPath\": \"x\"", "\"launchArguments\": [\"-x\"]" })
        {
            var error = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App("{ \"url\": \"http://localhost:3000\", " + field + " }"));
            Assert.Equal("INVALID_CONFIG", error.Code);
        }
    }

    /// <summary>
    /// Partial: calls <c>WebPermissions.ValidatePermissions</c>, which upstream's <c>validateApp</c> runs, for the browser the engine would
    /// pass; the engine's choice of browser (<c>web({ browser: 'webkit' })</c>, a provider or a CDP attach taking Chromium's names) is not
    /// reached. The config load takes the key on a web target.
    /// </summary>
    [Fact]
    public void Partial_Takes_the_permissions_its_browser_grants_and_grants_only()
    {
        static IReadOnlyDictionary<string, AppPermissionState> Of(string name, AppPermissionState state) => new Dictionary<string, AppPermissionState> { [name] = state };
        static ConfigurationException Refused(BrowserName browser, IReadOnlyDictionary<string, AppPermissionState> permissions)
        {
            var error = Assert.Throws<ConfigurationException>(() => WebPermissions.ValidatePermissions("web", browser, permissions));
            Assert.Equal("INVALID_CONFIG", error.Code);
            return error;
        }

        var config = ResolveConfigTests.App("""{ "url": "http://localhost:3000", "permissions": { "clipboard-read": "grant", "clipboard-write": "grant" } }""");
        WebPermissions.ValidatePermissions("web", BrowserName.Chromium, config.Target.App.Permissions!);
        WebPermissions.ValidatePermissions("web", BrowserName.Webkit, Of("clipboard-read", AppPermissionState.Grant));
        Assert.Contains(
            "target \"web\" app.permissions on firefox has unknown key \"clipboard-read\"",
            Refused(BrowserName.Firefox, Of("clipboard-read", AppPermissionState.Grant)).Message,
            StringComparison.Ordinal);
        Assert.Contains("did you mean \"clipboard-write\"", Refused(BrowserName.Chromium, Of("clipboard-wirte", AppPermissionState.Grant)).Message, StringComparison.Ordinal);
        foreach (var state in new[] { AppPermissionState.Deny, AppPermissionState.Reset })
        {
            Assert.Equal(
                $"target \"web\" app.permissions.camera is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead",
                Refused(BrowserName.Chromium, Of("camera", state)).Message);
        }
    }
}
