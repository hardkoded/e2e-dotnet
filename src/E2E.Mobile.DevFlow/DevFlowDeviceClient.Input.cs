// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

internal sealed partial class DevFlowDeviceClient
{
    /// <summary>How far a screen scroll travels when the call says nothing, as a share of the viewport.</summary>
    private const double DefaultScrollAmount = 0.5;

    /// <summary>How long a swipe or pan takes when the call says nothing: a flick, not a drag.</summary>
    private const int SwipeMilliseconds = 200;

    /// <summary>The element a <c>ref</c> option names in the newest tree. Null when the call names a point instead.</summary>
    private ScreenElement? ElementOf(JsonObject options) => Text(options, "ref") is { } reference ? _screen.ElementOf(reference) : null;

    /// <summary>The body every action on an element carries: its id and the epoch of the capture it came from, so the agent refuses a stale target.</summary>
    private JsonObject Target(ScreenElement element, params (string Key, JsonNode? Value)[] more)
    {
        var body = new JsonObject { ["elementId"] = element.Id };
        if (_screen.CaptureEpoch is { } epoch)
        {
            body["captureEpoch"] = epoch;
        }

        foreach (var (key, value) in more)
        {
            if (value is not null)
            {
                body[key] = value;
            }
        }

        return body;
    }

    private Task<JsonNode?> ActionAsync(string action, JsonObject body, CancellationToken cancellationToken) =>
        agent.PostAsync(Api + "ui/actions/" + action, body, cancellationToken);

    /// <summary>A gesture on an element, or on the current page when <paramref name="element"/> is null.</summary>
    private Task<JsonNode?> GestureAsync(string type, ScreenElement? element, CancellationToken cancellationToken, params (string Key, JsonNode? Value)[] more)
    {
        var body = element is null ? new JsonObject() : Target(element);
        body["type"] = type;
        foreach (var (key, value) in more)
        {
            if (value is not null)
            {
                body[key] = value;
            }
        }

        return ActionAsync("gesture", body, cancellationToken);
    }

    /// <summary>
    /// The key the agent's key action takes beside typed text. The agent reads the key, else the text, as the key name,
    /// so a typed word that is also a key name (<c>delete</c>, <c>enter</c>) would press the key; a key that is no key
    /// name makes the agent append the text instead, whatever it says.
    /// </summary>
    private const string TextKey = "text";

    /// <summary>
    /// A press on an element is a tap; two presses are the agent's double tap. A press at a point taps the element the
    /// agent finds there, since its tap takes an element and no coordinates.
    /// </summary>
    private async Task<JsonNode?> PressAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var count = Number(options["count"]) ?? 1;
        if (count is not (1 or 2))
        {
            throw DevFlowErrors.InvalidArgument("a press takes a count of 1 or 2");
        }

        if (ElementOf(options) is { } element)
        {
            if (count == 2)
            {
                await GestureAsync("doubletap", element, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ActionAsync("tap", Target(element), cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        var (x, y) = PointOf(options);
        if (count == 2)
        {
            throw DevFlowErrors.Unsupported("a double tap at a bare point is not supported by the DevFlow agent; double tap a node");
        }

        var hit = await ReadScreenPartAsync(Api + $"ui/hit-test?x={x.ToString(CultureInfo.InvariantCulture)}&y={y.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
        var topmost = (hit?["elements"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
        var id = Text(topmost?["id"]) ?? throw DevFlowErrors.Failed($"nothing on the screen takes a tap at ({x}, {y})");
        var body = new JsonObject { ["elementId"] = id };
        if (Number(hit?["captureEpoch"]) is { } epoch)
        {
            body["captureEpoch"] = (long)epoch;
        }

        await ActionAsync("tap", body, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<JsonNode?> LongPressAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var element = ElementOf(options) ?? throw DevFlowErrors.Unsupported("a long press at a bare point is not supported by the DevFlow agent; long press a node");
        await GestureAsync("longpress", element, cancellationToken, ("durationMs", JsonValue.Create((int)(Number(options["durationMs"]) ?? 1000)))).ConfigureAwait(false);
        return null;
    }

    /// <summary>Fills a field by element id. An empty text clears it, which is the agent's own clear.</summary>
    private async Task<JsonNode?> FillAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var element = ElementOf(options) ?? throw DevFlowErrors.InvalidArgument("fill needs the ref of a field");
        var text = Text(options, "text") ?? "";
        if (text.Length == 0)
        {
            await ActionAsync("clear", Target(element), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ActionAsync("fill", Target(element, ("text", JsonValue.Create(text))), cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Types into the focused field. The agent has no key bus: its key action appends text to the field it is given, so
    /// the field with the focus is read off a fresh tree first. With none, the keys would land nowhere.
    /// </summary>
    private async Task<JsonNode?> TypeAsync(string text, CancellationToken cancellationToken)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var focused = await FocusedInputAsync(cancellationToken).ConfigureAwait(false);
        await ActionAsync("key", Target(focused, ("key", JsonValue.Create(TextKey)), ("text", JsonValue.Create(text))), cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<ScreenElement> FocusedInputAsync(CancellationToken cancellationToken)
    {
        var screen = await ReadScreenAsync(interactiveOnly: false, cancellationToken).ConfigureAwait(false);
        return screen.FocusedInput() ?? throw DevFlowErrors.Unsupported("no field has the focus, so the DevFlow agent has nowhere to type; fill the field by id or tap it first");
    }

    /// <summary>A swipe from one point to another: the agent takes a direction and a distance, on the current page.</summary>
    private async Task<JsonNode?> SwipeAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var (fromX, fromY) = PointAt(options["from"]);
        var (toX, toY) = PointAt(options["to"]);
        var (dx, dy) = (toX - fromX, toY - fromY);
        var direction = Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? "right" : "left") : (dy >= 0 ? "down" : "up");
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance == 0)
        {
            throw DevFlowErrors.InvalidArgument("a swipe needs two different points");
        }

        await GestureAsync("swipe", null, cancellationToken, ("direction", JsonValue.Create(direction)), ("distance", JsonValue.Create(distance)), ("durationMs", JsonValue.Create(SwipeMilliseconds))).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Scrolls the content one way: the finger travels the other way, as it does on a phone, about <c>amount</c> of the
    /// viewport's extent along the axis. On a node when the call names one, else on the current page.
    /// </summary>
    private async Task<JsonNode?> ScrollAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var element = ElementOf(options);
        var finger = Text(options, "direction") switch
        {
            "down" => "up",
            "up" => "down",
            "right" => "left",
            "left" => "right",
            var other => throw DevFlowErrors.InvalidArgument($"scroll needs a direction of up, down, left, or right, not {other ?? "none"}"),
        };
        var amount = Math.Clamp(Number(options["amount"]) ?? DefaultScrollAmount, 0.05, 0.95);
        if (_screen.Viewport is null)
        {
            await ReadScreenAsync(interactiveOnly: false, cancellationToken).ConfigureAwait(false);
        }

        var extent = element?.Rect is { } box
            ? (finger is "up" or "down" ? box.Height : box.Width)
            : _screen.Viewport is { } viewport ? (finger is "up" or "down" ? viewport.Height : viewport.Width) : 0;
        var distance = extent > 0 ? extent * amount : (double?)null;
        await GestureAsync("swipe", element, cancellationToken, ("direction", JsonValue.Create(finger)), ("distance", distance is null ? null : JsonValue.Create(distance.Value)), ("durationMs", JsonValue.Create(SwipeMilliseconds))).ConfigureAwait(false);
        return null;
    }

    /// <summary>A pan from a node by a delta. The agent pans an element, not a bare point.</summary>
    private async Task<JsonNode?> PanAsync(JsonObject options, CancellationToken cancellationToken)
    {
        var element = ElementOf(options) ?? throw DevFlowErrors.Unsupported("a pan from a bare point is not supported by the DevFlow agent; pan from a node");
        await GestureAsync(
            "pan",
            element,
            cancellationToken,
            ("deltaX", JsonValue.Create(Number(options["dx"]) ?? 0)),
            ("deltaY", JsonValue.Create(Number(options["dy"]) ?? 0)),
            ("durationMs", JsonValue.Create((int)(Number(options["durationMs"]) ?? SwipeMilliseconds)))).ConfigureAwait(false);
        return null;
    }

    /// <summary>Enter completes the focused field, as the soft keyboard's return key does. The agent has no way to dismiss the keyboard.</summary>
    private async Task<JsonNode?> KeyboardAsync(JsonObject options, CancellationToken cancellationToken)
    {
        switch (Text(options, "action"))
        {
            case "enter":
                var focused = await FocusedInputAsync(cancellationToken).ConfigureAwait(false);
                await ActionAsync("key", Target(focused, ("key", JsonValue.Create("Enter"))), cancellationToken).ConfigureAwait(false);
                return null;
            case "dismiss":
                throw DevFlowErrors.Unsupported("dismissing the keyboard is not supported by the DevFlow agent");
            case var other:
                throw DevFlowErrors.InvalidArgument($"keyboard needs an action of enter or dismiss, not {other ?? "none"}");
        }
    }

    /// <summary>Back: the agent pops the navigation stack, a modal, or a Shell route.</summary>
    private async Task<JsonNode?> BackAsync(CancellationToken cancellationToken)
    {
        await ActionAsync("back", new JsonObject(), cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static (double X, double Y) PointOf(JsonObject options) =>
        Number(options["x"]) is { } x && Number(options["y"]) is { } y ? (x, y) : throw DevFlowErrors.InvalidArgument("an interaction needs a ref or an x and y");

    private static (double X, double Y) PointAt(JsonNode? node) =>
        Number(node?["x"]) is { } x && Number(node?["y"]) is { } y ? (x, y) : throw DevFlowErrors.InvalidArgument("a gesture needs points with an x and y");
}
