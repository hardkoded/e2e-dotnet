// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

internal sealed partial class DevFlowDeviceClient
{
    private const string AttachOnly = "the client attaches to an app you launched: set DevFlowAgentsOptions.Launch to let it drive the device";

    /// <summary>
    /// <c>apps.open</c>. The agent lives inside the running app, so without a launcher an open is a check that the app
    /// the target names is the one the agent runs in, answered with its identity. With a launcher, a running app is
    /// brought forward (the launch of a running app does that on both platforms), one that is not running is started
    /// and its agent waited for, and a relaunch terminates it first and starts it fresh with its launch arguments. A link
    /// needs the device to route it, which neither the agent nor the launcher does yet.
    /// </summary>
    private async Task<JsonNode?> OpenAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var app = Text(options, "app");
        var url = Text(options, "url");
        if (url is not null || (app is not null && DeviceLinks.IsLink(app)))
        {
            throw DevFlowErrors.Unsupported("opening a link is not supported with DevFlow yet: the agent runs inside the app and cannot ask the device to open one");
        }

        if (app is null)
        {
            throw DevFlowErrors.InvalidArgument("apps.open needs an app");
        }

        var relaunch = options["relaunch"] is JsonValue flag && flag.TryGetValue<bool>(out var fresh) && fresh;
        var arguments = options["launchArgs"] is JsonArray array
            ? array.Select(argument => Text(argument) ?? throw DevFlowErrors.InvalidArgument("launch arguments are strings")).ToList()
            : [];
        if (control is null)
        {
            if (relaunch)
            {
                throw DevFlowErrors.Unsupported($"relaunching the app is not supported here: {AttachOnly}, or open it without Relaunch");
            }

            if (arguments.Count > 0)
            {
                throw DevFlowErrors.Unsupported($"launch arguments are not supported here: {AttachOnly}");
            }

            await StatusAsync(cancellationToken).ConfigureAwait(false);
            return Identity(app);
        }

        var device = await control.LauncherAsync(_platform ?? MobilePlatform.Ios, cancellationToken).ConfigureAwait(false);
        if (relaunch)
        {
            await device.TerminateAsync(app, cancellationToken).ConfigureAwait(false);
        }
        else if (await control.ProbeAsync(agent, device, cancellationToken).ConfigureAwait(false) is { } running)
        {
            if (arguments.Count > 0)
            {
                throw DevFlowErrors.Unsupported("launch arguments apply to a fresh launch: the app is running, so open it with Relaunch to start it with them");
            }

            ReadStatus(running);
            await device.LaunchAsync(app, [], cancellationToken).ConfigureAwait(false);
            return Identity(app);
        }

        // A new process: nothing the last screen named is on it.
        _screen = DevFlowScreen.Empty;
        await device.LaunchAsync(app, arguments, cancellationToken).ConfigureAwait(false);
        var status = await control.WaitForAgentAsync(agent, device, cancellationToken).ConfigureAwait(false);
        ReadStatus(status);
        return Identity(app);
    }

    /// <summary><c>apps.close</c>: ends the app's process. The agent goes with it, so the next open starts the app again.</summary>
    private async Task<JsonNode?> CloseAppAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var device = await LauncherAsync("terminating the app", cancellationToken).ConfigureAwait(false);
        var app = Text(options, "app") ?? _app.PackageId ?? throw DevFlowErrors.InvalidArgument("apps.close needs an app");
        await device.TerminateAsync(app, cancellationToken).ConfigureAwait(false);
        _screen = DevFlowScreen.Empty;
        return null;
    }

    /// <summary><c>apps.install</c> and <c>apps.reinstall</c>: the build goes on the device through the launcher. The answer names the app as the engine named it.</summary>
    private async Task<JsonNode?> InstallAsync(JsonObject options, bool reinstall, CancellationToken cancellationToken)
    {
        var device = await LauncherAsync("installing a build", cancellationToken).ConfigureAwait(false);
        var appPath = Text(options, "appPath") ?? throw DevFlowErrors.InvalidArgument("install needs an appPath");
        var app = Text(options, "app");
        await device.InstallAsync(appPath, app, reinstall, cancellationToken).ConfigureAwait(false);
        var result = new JsonObject();
        if (app is not null)
        {
            result[device.Platform == MobilePlatform.Ios ? "bundleId" : "package"] = app;
        }

        return result;
    }

    /// <summary><c>command.home</c>: the device's home button, where the launcher has one.</summary>
    private async Task<JsonNode?> HomeAsync(CancellationToken cancellationToken)
    {
        var device = await LauncherAsync("the home screen", cancellationToken).ConfigureAwait(false);
        await device.HomeAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    private Task<IAppLauncher> LauncherAsync(string what, CancellationToken cancellationToken) =>
        control is null
            ? Task.FromException<IAppLauncher>(DevFlowErrors.Unsupported($"{what} is not supported here: {AttachOnly}"))
            : control.LauncherAsync(_platform ?? MobilePlatform.Ios, cancellationToken);

    /// <summary>The identity of the app the agent runs in, checked against the app the engine named.</summary>
    private JsonObject Identity(string app)
    {
        if (_app.PackageId is { } running && !Matches(running, app) && !Matches(_app.Name, app))
        {
            throw DevFlowErrors.Failed($"the DevFlow agent runs {running}, not {app}; point the provider at the right app or fix the target's bundle id");
        }

        return AppIdentity(_app.PackageId ?? app, _app.Name ?? app);
    }

    private static bool Matches(string? reported, string wanted) => string.Equals(reported, wanted, StringComparison.OrdinalIgnoreCase);
}
