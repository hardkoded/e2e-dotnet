// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

internal sealed partial class DevFlowDeviceClient
{
    /// <summary><c>capture.snapshot</c>: the visual tree, converted to the snapshot the engine reads.</summary>
    private async Task<JsonNode?> SnapshotAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var interactiveOnly = options["interactiveOnly"] is JsonValue flag && flag.TryGetValue<bool>(out var wanted) && wanted;
        var screen = await ReadScreenAsync(interactiveOnly, cancellationToken).ConfigureAwait(false);
        return screen.Snapshot;
    }

    /// <summary>Reads the tree and remembers its elements as the newest. The app's identity comes from the last status read.</summary>
    private async Task<DevFlowScreen> ReadScreenAsync(bool interactiveOnly, CancellationToken cancellationToken)
    {
        if (_app.PackageId is null && _app.Name is null)
        {
            await StatusAsync(cancellationToken).ConfigureAwait(false);
        }

        var tree = await ReadTreeAsync(cancellationToken).ConfigureAwait(false);
        _screen = ElementTree.Read(tree, interactiveOnly, _app.Name, _app.PackageId);
        return _screen;
    }

    private Task<JsonNode?> ReadTreeAsync(CancellationToken cancellationToken) =>
        ReadScreenPartAsync(Api + "ui/tree?envelope=true", cancellationToken);

    /// <summary>
    /// Reads a part of the screen (the tree or a hit-test). The agent answers 409 <c>capture-changed-during-read</c> ("the
    /// UI changed while it was being captured, retry") when the screen moves during the read, as it does while an animation
    /// runs. A read changes nothing, so it is tried again after a short wait; the last attempt's failure is the caller's.
    /// </summary>
    private async Task<JsonNode?> ReadScreenPartAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt < ReadAttempts; attempt++)
        {
            try
            {
                return await agent.GetAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (DevFlowAgentException failure) when (failure.Status == System.Net.HttpStatusCode.Conflict && failure.Code == "capture-changed-during-read")
            {
                await Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return await agent.GetAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>capture.screenshot</c>: the PNG the agent takes at the device's pixel density, written to <c>path</c>. The
    /// logical size is the window's, from the tree. The status bar is as the device shows it: the agent cannot normalize
    /// it, so <c>normalizeStatusBar</c> is ignored.
    /// </summary>
    private async Task<JsonNode?> ScreenshotAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var path = Text(options, "path") ?? throw DevFlowErrors.InvalidArgument("a screenshot needs a path");
        if (_screen.Viewport is null)
        {
            await ReadScreenAsync(interactiveOnly: false, cancellationToken).ConfigureAwait(false);
        }

        var (png, _) = await agent.GetPngAsync(Api + "ui/screenshot?scale=native", cancellationToken).ConfigureAwait(false);
        if (DeviceSupport.ReadPngSize(png) is not { } size)
        {
            throw DevFlowErrors.Failed("the agent returned a screenshot that is not a PNG");
        }

        await WriteAsync(path, png, cancellationToken).ConfigureAwait(false);
        var result = new JsonObject { ["path"] = path, ["width"] = size.Width, ["height"] = size.Height };
        if (_screen.Viewport is { } viewport && viewport.Width > 0)
        {
            result["logicalWidth"] = viewport.Width;
            result["logicalHeight"] = viewport.Height;
            result["pixelDensity"] = size.Width / viewport.Width;
        }

        return result;
    }

    /// <summary>Writes through a unique temporary name, then moves, so a reader never sees a half-written file.</summary>
    private static async Task WriteAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = Path.Combine(directory ?? ".", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
