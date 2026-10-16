// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

internal sealed partial class AppiumDeviceClient
{
    /// <summary>
    /// <c>capture.snapshot</c>: the page source, converted to the snapshot the engine reads. Until a converter is
    /// plugged in, there is no snapshot to give, and the engine gets <c>NOT_IMPLEMENTED</c> (an unsupported capability).
    /// </summary>
    private async Task<JsonNode?> SnapshotAsync(CancellationToken cancellationToken) =>
        await ReadScreenAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Reads the screen and remembers its refs as the newest.</summary>
    private async Task<JsonObject> ReadScreenAsync(CancellationToken cancellationToken)
    {
        if (converter is null)
        {
            throw AppiumErrors.NotImplemented("reading the screen of a TestMu device is not implemented yet: the page source has no snapshot conversion");
        }

        var source = await driver.GetAsync("source", cancellationToken).ConfigureAwait(false);
        var snapshot = converter.Convert(source is JsonValue value && value.TryGetValue<string>(out var text) ? text : "", platform);
        _refs.Replace(snapshot);
        return snapshot;
    }

    /// <summary>
    /// <c>capture.screenshot</c>: the PNG Appium takes, written to <c>path</c>. The logical size is the window's. The
    /// status bar is as the device shows it: Appium cannot normalize it, so <c>normalizeStatusBar</c> is ignored.
    /// </summary>
    private async Task<JsonNode?> ScreenshotAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var path = Text(options, "path") ?? throw AppiumErrors.InvalidArgument("a screenshot needs a path");
        var encoded = await driver.GetAsync("screenshot", cancellationToken).ConfigureAwait(false);
        byte[] png;
        try
        {
            png = Convert.FromBase64String(encoded is JsonValue value && value.TryGetValue<string>(out var text) ? text : "");
        }
        catch (FormatException cause)
        {
            throw new AgentDeviceError("COMMAND_FAILED", "the device returned a screenshot that is not base64", inner: cause);
        }

        await File.WriteAllBytesAsync(path, png, cancellationToken).ConfigureAwait(false);
        var (logicalWidth, logicalHeight) = await WindowAsync(cancellationToken).ConfigureAwait(false);
        var result = new JsonObject { ["path"] = path, ["logicalWidth"] = logicalWidth, ["logicalHeight"] = logicalHeight };
        if (DeviceSupport.ReadPngSize(png) is { } size)
        {
            result["width"] = size.Width;
            result["height"] = size.Height;
            result["pixelDensity"] = size.Width / logicalWidth;
        }

        return result;
    }
}
