// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>
/// The device client for a MAUI app with the DevFlow agent: <c>namespace.method</c> calls (<see cref="DeviceMethods"/>)
/// turned into the agent's HTTP API. It answers what the agent-device client answers and refuses, with the errors
/// <see cref="DeviceErrors"/> reads, what the agent cannot do.
/// <para>
/// The agent runs inside the app. With an <see cref="AppControl"/> the client launches, relaunches, installs, and
/// terminates the app through the device's tools and waits for the agent of the new process; without one it attaches
/// to an app you launched. <c>sessions.close</c> does nothing, since the next attempt drives the same app. The provider
/// releases the mutation lease when the run ends.
/// </para>
/// <para>
/// <c>settle</c> options are not honored: the engine's own observe loop waits for the screen to settle.
/// </para>
/// </summary>
internal sealed partial class DevFlowDeviceClient(DevFlowAgentClient agent, MobilePlatform? platform = null, AppControl? control = null) : IDeviceClient
{
    private const string Api = "api/v1/";

    /// <summary>The platform the target declares: given up front, else read off the <c>devices.boot</c> the engine sends first.</summary>
    private MobilePlatform? _platform = platform;

    private DevFlowScreen _screen = DevFlowScreen.Empty;
    private (string? Name, string? PackageId) _app;

    /// <summary>The agent the client talks to.</summary>
    internal DevFlowAgentClient Agent => agent;

    public async Task<JsonNode?> CallAsync(string method, JsonObject? options, CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchAsync(method, options ?? [], cancellationToken).ConfigureAwait(false);
        }
        catch (DevFlowAgentException failure)
        {
            throw DevFlowErrors.Translate(failure, method);
        }
    }

    private Task<JsonNode?> DispatchAsync(string method, JsonObject options, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case DeviceMethods.DevicesBoot:
                return BootAsync(options, cancellationToken);
            case DeviceMethods.DevicesList or DeviceMethods.SessionsList:
                return Task.FromResult<JsonNode?>(new JsonArray());
            case DeviceMethods.CommandPrepare:
                return Task.FromResult<JsonNode?>(null);
            case DeviceMethods.SessionsClose:
                return CloseSessionAsync(cancellationToken);
            case DeviceMethods.AppsOpen:
                return OpenAsync(options, cancellationToken);
            case DeviceMethods.AppsClose:
                return CloseAppAsync(options, cancellationToken);
            case DeviceMethods.AppsInstall:
                return InstallAsync(options, reinstall: false, cancellationToken);
            case DeviceMethods.AppsReinstall:
                return InstallAsync(options, reinstall: true, cancellationToken);
            case DeviceMethods.CommandHome:
                return HomeAsync(cancellationToken);
            case DeviceMethods.CaptureSnapshot:
                return SnapshotAsync(options, cancellationToken);
            case DeviceMethods.CaptureScreenshot:
                return ScreenshotAsync(options, cancellationToken);
            case DeviceMethods.InteractionsPress:
                return PressAsync(options, cancellationToken);
            case DeviceMethods.InteractionsLongPress:
                return LongPressAsync(options, cancellationToken);
            case DeviceMethods.InteractionsFill:
                return FillAsync(options, cancellationToken);
            case DeviceMethods.InteractionsType:
                return TypeAsync(Text(options, "text") ?? "", cancellationToken);
            case DeviceMethods.InteractionsSwipe:
                return SwipeAsync(options, cancellationToken);
            case DeviceMethods.InteractionsScroll:
                return ScrollAsync(options, cancellationToken);
            case DeviceMethods.InteractionsPan:
                return PanAsync(options, cancellationToken);
            case DeviceMethods.CommandKeyboard:
                return KeyboardAsync(options, cancellationToken);
            case DeviceMethods.CommandBack:
                return BackAsync(cancellationToken);
            case DeviceMethods.CommandAppState:
                return AppStateAsync(cancellationToken);
            case DeviceMethods.SettingsUpdate:
                return SettingAsync(options, cancellationToken);
            case DeviceMethods.InteractionsDrag:
                throw DevFlowErrors.Unsupported("a drag between two nodes is not supported by the DevFlow agent");
            case DeviceMethods.InteractionsHover:
                throw DevFlowErrors.Unsupported("a touch screen has no hover");
            case DeviceMethods.CommandAlert:
                throw DevFlowErrors.Unsupported("system alerts are not supported with DevFlow yet; tap the alert's button instead");
            case DeviceMethods.CommandOrientation:
                throw DevFlowErrors.Unsupported("rotating the device is not supported with DevFlow");
            case DeviceMethods.CommandFold:
                throw DevFlowErrors.Unsupported("folding a device is not supported with DevFlow");
            case DeviceMethods.CommandClipboard:
                throw DevFlowErrors.Unsupported("the clipboard is not supported with DevFlow");
            default:
                throw DevFlowErrors.Unsupported($"the DevFlow device client does not support {method}");
        }
    }

    /// <summary>
    /// <c>devices.boot</c>: the agent's status, which proves the app is up and names it. The platform the agent reports
    /// must be the one the target declares. A boot also claims the mutation lease, so a lease another host holds fails
    /// the session here, before the first action, and the Inspector and the MCP server see who drives the app. With a
    /// launcher, the agent is looked for again when the port the binding names no longer answers (a relaunch moved it),
    /// and an app that is not running is not a failure: the open that follows launches it.
    /// </summary>
    private async Task<JsonNode?> BootAsync(JsonObject options, CancellationToken cancellationToken)
    {
        _platform = Text(options, "platform") switch
        {
            "ios" => MobilePlatform.Ios,
            "android" => MobilePlatform.Android,
            _ => _platform,
        };
        agent.PlatformHint = _platform;
        JsonNode? status;
        if (control is not null)
        {
            var device = await control.LauncherAsync(_platform ?? MobilePlatform.Ios, cancellationToken).ConfigureAwait(false);
            status = await control.ProbeAsync(agent, device, cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                return null;
            }

            ReadStatus(status);
        }
        else
        {
            status = await StatusAsync(cancellationToken).ConfigureAwait(false);
        }

        await agent.ClaimLeaseAsync(cancellationToken).ConfigureAwait(false);
        return status;
    }

    /// <summary>Reads <c>GET /agent/status</c>, remembers the app's identity, and checks the platform.</summary>
    private async Task<JsonNode?> StatusAsync(CancellationToken cancellationToken)
    {
        var status = await agent.GetAsync(Api + "agent/status", cancellationToken).ConfigureAwait(false);
        ReadStatus(status);
        return status;
    }

    /// <summary>What a status says about the app, checked against the platform the target declares.</summary>
    private void ReadStatus(JsonNode? status)
    {
        var reported = PlatformOf(status);
        if (reported is not null && _platform is { } expected && !string.Equals(reported, expected.WireName(), StringComparison.OrdinalIgnoreCase))
        {
            throw DevFlowErrors.Failed($"the DevFlow agent runs a {reported} app, but the target's platform is {expected.WireName()}; point the provider at the right app or fix MobileOptions.Platform");
        }

        _app = (Text(status?["app"]?["name"]), Text(status?["app"]?["packageId"]));
    }

    /// <summary><c>sessions.close</c>: the agent outlives the attempt, so only the session's Android forward is let go; the next session's boot forwards again.</summary>
    private async Task<JsonNode?> CloseSessionAsync(CancellationToken cancellationToken)
    {
        if (control is not null)
        {
            await control.ReleaseForwardAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary><c>command.appState</c>: the app the agent runs in, which is the one on screen as far as the agent can tell.</summary>
    private async Task<JsonNode?> AppStateAsync(CancellationToken cancellationToken)
    {
        await StatusAsync(cancellationToken).ConfigureAwait(false);
        return AppIdentity(_app.PackageId ?? _app.Name ?? "", _app.Name);
    }

    /// <summary><c>settings.update</c>: the app's theme is the one setting the agent can change.</summary>
    private async Task<JsonNode?> SettingAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var setting = Text(options, "setting");
        if (setting != "appearance")
        {
            throw DevFlowErrors.Unsupported($"the {setting ?? "settings"} setting is not supported with DevFlow: the agent runs inside the app and cannot change the device");
        }

        var theme = Text(options, "state") switch
        {
            "dark" => "dark",
            "light" => "light",
            var other => throw DevFlowErrors.InvalidArgument($"appearance takes dark or light, not {other ?? "none"}"),
        };
        await agent.PutAsync(Api + "device/app/theme", new JsonObject { ["theme"] = theme }, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static JsonObject AppIdentity(string bundleId, string? name) => new() { ["appBundleId"] = bundleId, ["appName"] = name ?? bundleId };

    /// <summary>The platform a status names: under <c>device</c>, where the agent writes it, else at the top, where its spec draws it.</summary>
    internal static string? PlatformOf(JsonNode? status) => Text(status?["device"]?["platform"]) ?? Text(status?["platform"]);

    private static string? Text(JsonObject options, string name) =>
        options[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A JSON number, whatever type backs it: one parsed from text and one built in code differ.</summary>
    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;
}
