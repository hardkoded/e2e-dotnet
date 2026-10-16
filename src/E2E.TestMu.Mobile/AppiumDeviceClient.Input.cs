// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

internal sealed partial class AppiumDeviceClient
{
    /// <summary>How long a swipe takes, and how far a scroll goes when the call says nothing.</summary>
    private const double SwipeMilliseconds = 300;

    private const double DefaultScrollAmount = 0.5;

    /// <summary>The key of an element reference in the answer of a W3C call (the legacy protocol's is <c>ELEMENT</c>).</summary>
    private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

    /// <summary>A point, by <c>ref</c> (the centre of the node the newest snapshot has under it) or by <c>x</c> and <c>y</c>.</summary>
    private (double X, double Y) PointOf(JsonObject options)
    {
        if (Text(options, "ref") is { } reference)
        {
            return _refs.CentreOf(reference);
        }

        return Number(options["x"]) is { } x && Number(options["y"]) is { } y ? (x, y) : throw AppiumErrors.InvalidArgument("an interaction needs a ref or an x and y");
    }

    private static (double X, double Y) PointAt(JsonNode? node) =>
        Number(node?["x"]) is { } x && Number(node?["y"]) is { } y ? (x, y) : throw AppiumErrors.InvalidArgument("a gesture needs points with an x and y");

    private Task<JsonNode?> PressAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (x, y) = PointOf(options);
        return ActAsync(PointerActions.Tap(x, y, Number(options["count"]) is 2 ? 2 : 1), cancellationToken);
    }

    private Task<JsonNode?> LongPressAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (x, y) = PointOf(options);
        return ActAsync(PointerActions.LongPress(x, y, Number(options["durationMs"]) ?? 1000), cancellationToken);
    }

    private Task<JsonNode?> SwipeAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (fromX, fromY) = PointAt(options["from"]);
        var (toX, toY) = PointAt(options["to"]);
        return ActAsync(PointerActions.Drag(fromX, fromY, toX, toY, SwipeMilliseconds), cancellationToken);
    }

    private Task<JsonNode?> PanAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (x, y) = PointOf(options);
        return ActAsync(PointerActions.Drag(x, y, x + (Number(options["dx"]) ?? 0), y + (Number(options["dy"]) ?? 0), Number(options["durationMs"]) ?? SwipeMilliseconds), cancellationToken);
    }

    /// <summary>Press on the source, hold, and move to the destination: a long press first, so a list takes it as a drag.</summary>
    private Task<JsonNode?> DragAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (fromX, fromY) = _refs.CentreOf(Text(options, "source") ?? throw AppiumErrors.InvalidArgument("a drag needs a source"));
        var (toX, toY) = _refs.CentreOf(Text(options, "destination") ?? throw AppiumErrors.InvalidArgument("a drag needs a destination"));
        return ActAsync(PointerActions.Drag(fromX, fromY, toX, toY, 500, holdMilliseconds: 500), cancellationToken);
    }

    /// <summary>
    /// Scrolls the screen's content one way: the finger travels the other way, as it does on a phone, about
    /// <c>amount</c> of the screen's extent along the axis, around the centre.
    /// </summary>
    private async Task<JsonNode?> ScrollAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var amount = Math.Clamp(Number(options["amount"]) ?? DefaultScrollAmount, 0.05, 0.95);
        var (width, height) = await WindowAsync(cancellationToken).ConfigureAwait(false);
        var (centreX, centreY) = (width / 2, height / 2);
        var (dx, dy) = (width * amount / 2, height * amount / 2);
        var (from, to) = Text(options, "direction") switch
        {
            "down" => ((centreX, centreY + dy), (centreX, centreY - dy)),
            "up" => ((centreX, centreY - dy), (centreX, centreY + dy)),
            "right" => ((centreX + dx, centreY), (centreX - dx, centreY)),
            "left" => ((centreX - dx, centreY), (centreX + dx, centreY)),
            var other => throw AppiumErrors.InvalidArgument($"scroll needs a direction of up, down, left, or right, not {other ?? "none"}"),
        };
        return await ActAsync(PointerActions.Drag(from.Item1, from.Item2, to.Item1, to.Item2, SwipeMilliseconds), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fills a field: taps it to focus it, then clears the focused element and types into it. Appium addresses a text
    /// entry by element, and the snapshot's refs are not element ids, so the focused element is the way in.
    /// </summary>
    private async Task<JsonNode?> FillAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (x, y) = PointOf(options);
        await ActAsync(PointerActions.Tap(x, y), cancellationToken).ConfigureAwait(false);
        var element = await ActiveElementAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new AgentDeviceError("COMMAND_FAILED", "no field has the focus after the tap, so there is nothing to fill");
        await driver.PostAsync($"element/{Uri.EscapeDataString(element)}/clear", null, cancellationToken).ConfigureAwait(false);
        if (Text(options, "text") is { Length: > 0 } text)
        {
            await SendValueAsync(element, text, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Types into the focused element. With none, the keys go to the device as key actions.</summary>
    private async Task<JsonNode?> TypeAsync(string text, CancellationToken cancellationToken)
    {
        if (text.Length == 0)
        {
            return null;
        }

        if (await ActiveElementAsync(cancellationToken).ConfigureAwait(false) is { } element)
        {
            await SendValueAsync(element, text, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return await ActAsync(PointerActions.Keys(text), cancellationToken).ConfigureAwait(false);
    }

    private Task SendValueAsync(string element, string text, CancellationToken cancellationToken) =>
        driver.PostAsync(
            $"element/{Uri.EscapeDataString(element)}/value",
            new JsonObject { ["text"] = text, ["value"] = new JsonArray([.. text.Select(character => (JsonNode?)JsonValue.Create(character.ToString()))]) },
            cancellationToken);

    /// <summary>The focused element's id, or null when nothing has the focus.</summary>
    private async Task<string?> ActiveElementAsync(CancellationToken cancellationToken)
    {
        try
        {
            var element = await driver.GetAsync("element/active", cancellationToken).ConfigureAwait(false) as JsonObject;
            return (element?[ElementKey] ?? element?["ELEMENT"]) is JsonValue id && id.TryGetValue<string>(out var text) ? text : null;
        }
        catch (WebDriverException failure) when (failure.Error == "no such element")
        {
            return null;
        }
    }

    private async Task<JsonNode?> KeyboardAsync(JsonObject options, CancellationToken cancellationToken)
    {
        switch (Text(options, "action"))
        {
            case "enter":
                return await ActAsync(PointerActions.Keys(PointerActions.Enter), cancellationToken).ConfigureAwait(false);
            case "dismiss":
                await ExecuteAsync("mobile: hideKeyboard", cancellationToken).ConfigureAwait(false);
                return null;
            case var other:
                throw AppiumErrors.InvalidArgument($"keyboard needs an action of enter or dismiss, not {other ?? "none"}");
        }
    }

    /// <summary>
    /// A native alert, accepted or dismissed. A system dialog that is not a WebDriver alert (Android's permission
    /// prompt) is <c>no such alert</c>: it fails, and the test taps the dialog's button instead.
    /// </summary>
    private async Task<JsonNode?> AlertAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var action = Text(options, "action");
        if (action is not ("accept" or "dismiss"))
        {
            throw AppiumErrors.InvalidArgument($"alert needs an action of accept or dismiss, not {action ?? "none"}");
        }

        await driver.PostAsync($"alert/{action}", null, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<JsonNode?> HomeAsync(CancellationToken cancellationToken)
    {
        if (platform == MobilePlatform.Ios)
        {
            await ExecuteAsync("mobile: pressButton", cancellationToken, ("name", Json("home"))).ConfigureAwait(false);
        }
        else
        {
            // KEYCODE_HOME.
            await ExecuteAsync("mobile: pressKey", cancellationToken, ("keycode", JsonValue.Create(3))).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Back. Android has a system back. iOS has none, so, as agent-device does, it taps the back button of the
    /// navigation bar on the screen now.
    /// </summary>
    private async Task<JsonNode?> BackAsync(CancellationToken cancellationToken)
    {
        if (platform == MobilePlatform.Android)
        {
            await driver.PostAsync("back", null, cancellationToken).ConfigureAwait(false);
            return null;
        }

        await ReadScreenAsync(cancellationToken).ConfigureAwait(false);
        var (x, y) = _refs.NavigationBarBackButton() ?? throw AppiumErrors.Unsupported("there is no back button in the navigation bar of this screen");
        return await ActAsync(PointerActions.Tap(x, y), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appium turns the screen to portrait or landscape. The upside-down and the two landscapes are not told apart.</summary>
    private async Task<JsonNode?> OrientationAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var orientation = Text(options, "orientation") switch
        {
            "portrait" => "PORTRAIT",
            "landscape-left" or "landscape-right" => "LANDSCAPE",
            var other => throw AppiumErrors.Unsupported($"the {other ?? "none"} orientation is not supported on TestMu devices"),
        };
        await driver.PostAsync("orientation", new JsonObject { ["orientation"] = orientation }, cancellationToken).ConfigureAwait(false);
        return null;
    }
}
