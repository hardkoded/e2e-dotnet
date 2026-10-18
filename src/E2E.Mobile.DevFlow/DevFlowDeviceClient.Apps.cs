// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

internal sealed partial class DevFlowDeviceClient
{
    /// <summary>
    /// <c>apps.open</c>. The agent lives inside the running app, so an open is a check that the app the target names is
    /// the one the agent runs in, answered with its identity. A relaunch, launch arguments, and a link need the device,
    /// which the agent cannot reach yet: they are refused, and the test launches the app itself.
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

        if (options["relaunch"] is JsonValue relaunch && relaunch.TryGetValue<bool>(out var fresh) && fresh)
        {
            throw DevFlowErrors.Unsupported("relaunching the app is not supported with DevFlow yet: the agent runs inside the app, so a test attaches to the app you launched; open it without Relaunch");
        }

        if (options["launchArgs"] is JsonArray { Count: > 0 })
        {
            throw DevFlowErrors.Unsupported("launch arguments are not supported with DevFlow: the app is already running when a test attaches to it");
        }

        await StatusAsync(cancellationToken).ConfigureAwait(false);
        if (_app.PackageId is { } running && !Matches(running, app) && !Matches(_app.Name, app))
        {
            throw DevFlowErrors.Failed($"the DevFlow agent runs {running}, not {app}; point the provider at the right app or fix the target's bundle id");
        }

        return AppIdentity(_app.PackageId ?? app, _app.Name ?? app);
    }

    private static bool Matches(string? reported, string wanted) => string.Equals(reported, wanted, StringComparison.OrdinalIgnoreCase);
}
