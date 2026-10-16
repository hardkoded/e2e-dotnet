// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

internal sealed partial class AppiumDeviceClient
{
    /// <summary>The argument that names an app: a bundle id on iOS, a package on Android.</summary>
    private string AppKey => platform == MobilePlatform.Ios ? "bundleId" : "appId";

    /// <summary>
    /// <c>apps.open</c>. A link opens through the OS (<c>mobile: deepLink</c>), into the app when one is named. An app
    /// opens in the foreground, or fresh when <c>relaunch</c> is set: terminated first, then started with its launch
    /// arguments. The answer names the app as the engine named it, since Appium reports no app identity here.
    /// </summary>
    private async Task<JsonNode?> OpenAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var app = Text(options, "app");
        var url = Text(options, "url");
        if (url is null && app is not null && DeviceLinks.IsLink(app))
        {
            (url, app) = (app, null);
        }

        if (url is not null)
        {
            await ExecuteAsync(
                "mobile: deepLink",
                cancellationToken,
                ("url", Json(url)),
                (platform == MobilePlatform.Ios ? "bundleId" : "package", Json(app))).ConfigureAwait(false);
            return app is null ? null : AppIdentity(app);
        }

        if (app is null)
        {
            throw AppiumErrors.InvalidArgument("apps.open needs an app or a url");
        }

        var launchArguments = options["launchArgs"] is JsonArray array ? array.Select(argument => argument?.GetValue<string>()).OfType<string>().ToList() : [];
        if (options["relaunch"] is JsonValue relaunch && relaunch.TryGetValue<bool>(out var fresh) && fresh)
        {
            if (platform == MobilePlatform.Android && launchArguments.Count > 0)
            {
                throw AppiumErrors.Unsupported("launch arguments are not supported on TestMu Android devices");
            }

            await ExecuteAsync("mobile: terminateApp", cancellationToken, (AppKey, Json(app))).ConfigureAwait(false);
            if (platform == MobilePlatform.Ios)
            {
                await ExecuteAsync(
                    "mobile: launchApp",
                    cancellationToken,
                    ("bundleId", Json(app)),
                    ("arguments", launchArguments.Count == 0 ? null : new JsonArray([.. launchArguments.Select(argument => (JsonNode?)JsonValue.Create(argument))]))).ConfigureAwait(false);
                return AppIdentity(app);
            }
        }

        await ExecuteAsync("mobile: activateApp", cancellationToken, (AppKey, Json(app))).ConfigureAwait(false);
        return AppIdentity(app);
    }

    private async Task<JsonNode?> CloseAppAsync(JsonObject options, CancellationToken cancellationToken)
    {
        if (Text(options, "app") is { } app)
        {
            await ExecuteAsync("mobile: terminateApp", cancellationToken, (AppKey, Json(app))).ConfigureAwait(false);
        }

        return null;
    }

    private static JsonObject AppIdentity(string app) => new() { ["appBundleId"] = app, ["appName"] = app };

    /// <summary>
    /// <c>command.clipboard</c>. Appium moves the text as base64. A real iPhone has no pasteboard access from the driver:
    /// the hub's refusal reaches the test as an unsupported operation.
    /// </summary>
    private async Task<JsonNode?> ClipboardAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var ios = platform == MobilePlatform.Ios;
        if (Text(options, "action") == "write")
        {
            await ExecuteAsync(
                ios ? "mobile: setPasteboard" : "mobile: setClipboard",
                cancellationToken,
                ("content", Json(Convert.ToBase64String(Encoding.UTF8.GetBytes(Text(options, "text") ?? "")))),
                ios ? ("encoding", Json("utf8")) : ("contentType", Json("plaintext"))).ConfigureAwait(false);
            return null;
        }

        var content = await ExecuteAsync(
            ios ? "mobile: getPasteboard" : "mobile: getClipboard",
            cancellationToken,
            ios ? ("encoding", Json("utf8")) : ("contentType", Json("plaintext"))).ConfigureAwait(false);
        var encoded = content is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        try
        {
            return new JsonObject { ["action"] = "read", ["text"] = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)) };
        }
        catch (FormatException cause)
        {
            throw new AgentDeviceError("COMMAND_FAILED", "the device returned a clipboard that is not base64", inner: cause);
        }
    }
}
