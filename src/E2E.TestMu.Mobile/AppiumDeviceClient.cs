// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

/// <summary>
/// The device client for a TestMu device: <c>namespace.method</c> calls (<see cref="DeviceMethods"/>) turned into
/// Appium commands over a W3C WebDriver session. It answers what the agent-device client answers and refuses, with
/// the errors <see cref="DeviceErrors"/> reads, what Appium cannot do.
/// <para>
/// The Appium session belongs to the lease, not to a test attempt: <c>sessions.close</c> does nothing, since the
/// engine closes at the end of every attempt and the next attempt drives the same device. The provider ends the
/// session when it releases the lease.
/// </para>
/// <para>
/// <c>settle</c> options are not honored: the engine's own observe loop waits for the screen to settle.
/// </para>
/// </summary>
internal sealed partial class AppiumDeviceClient(WebDriverClient driver, MobilePlatform platform, IPageSourceConverter? converter = null) : ICancellableDeviceClient
{
    private readonly ScreenRefs _refs = new();

    public async Task<JsonNode?> CallAsync(string method, JsonObject? options, CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchAsync(method, options ?? [], cancellationToken).ConfigureAwait(false);
        }
        catch (WebDriverException failure)
        {
            throw AppiumErrors.Translate(failure, method);
        }
    }

    private Task<JsonNode?> DispatchAsync(string method, JsonObject options, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case DeviceMethods.DevicesBoot:
                return PingAsync(cancellationToken);
            case DeviceMethods.DevicesList or DeviceMethods.SessionsList:
                return Task.FromResult<JsonNode?>(new JsonArray());
            case DeviceMethods.CommandPrepare or DeviceMethods.SessionsClose:
                return Task.FromResult<JsonNode?>(null);
            case DeviceMethods.AppsOpen:
                return OpenAsync(options, cancellationToken);
            case DeviceMethods.AppsClose:
                return CloseAppAsync(options, cancellationToken);
            case DeviceMethods.CaptureSnapshot:
                return SnapshotAsync(cancellationToken);
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
            case DeviceMethods.InteractionsDrag:
                return DragAsync(options, cancellationToken);
            case DeviceMethods.CommandKeyboard:
                return KeyboardAsync(options, cancellationToken);
            case DeviceMethods.CommandAlert:
                return AlertAsync(options, cancellationToken);
            case DeviceMethods.CommandHome:
                return HomeAsync(cancellationToken);
            case DeviceMethods.CommandBack:
                return BackAsync(cancellationToken);
            case DeviceMethods.CommandOrientation:
                return OrientationAsync(options, cancellationToken);
            case DeviceMethods.CommandClipboard:
                return ClipboardAsync(options, cancellationToken);
            case DeviceMethods.AppsInstall or DeviceMethods.AppsReinstall:
                throw AppiumErrors.Unsupported("installing a build from a test is not supported on TestMu: the provider installs the app when it leases the device");
            case DeviceMethods.SettingsUpdate:
                throw AppiumErrors.Unsupported($"the {Text(options, "setting") ?? "settings"} setting is not supported on TestMu devices yet");
            case DeviceMethods.InteractionsHover:
                throw AppiumErrors.Unsupported("a touch screen has no hover");
            case DeviceMethods.CommandFold:
                throw AppiumErrors.Unsupported("folding a device is not supported on TestMu");
            case DeviceMethods.CommandAppState:
                throw AppiumErrors.Unsupported("reading the foreground app is not supported on TestMu devices yet");
            default:
                throw AppiumErrors.Unsupported($"the TestMu device client does not support {method}");
        }
    }

    /// <summary>A cheap read that proves the session is alive. The provider sends the same one to keep an idle session from expiring.</summary>
    private async Task<JsonNode?> PingAsync(CancellationToken cancellationToken)
    {
        await driver.GetAsync("window/rect", cancellationToken).ConfigureAwait(false);
        return null;
    }

    private Task<JsonNode?> ExecuteAsync(string script, CancellationToken cancellationToken, params (string Key, JsonNode? Value)[] arguments)
    {
        var args = new JsonObject();
        foreach (var (key, value) in arguments)
        {
            if (value is not null)
            {
                args[key] = value;
            }
        }

        return driver.ExecuteAsync(script, args, cancellationToken);
    }

    private async Task<JsonNode?> ActAsync(JsonObject actions, CancellationToken cancellationToken)
    {
        await driver.PostAsync("actions", actions, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>The window size, in the units pointers use.</summary>
    private async Task<(double Width, double Height)> WindowAsync(CancellationToken cancellationToken)
    {
        var rect = await driver.GetAsync("window/rect", cancellationToken).ConfigureAwait(false);
        return Number(rect?["width"]) is > 0 and var width && Number(rect?["height"]) is > 0 and var height
            ? (width, height)
            : throw new AgentDeviceError("COMMAND_FAILED", "the device reported no window size");
    }

    private static string? Text(JsonObject options, string name) =>
        options[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A JSON number, whatever type backs it: one parsed from text and one built in code differ.</summary>
    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;

    private static JsonNode? Json(string? text) => text is null ? null : JsonValue.Create(text);
}
