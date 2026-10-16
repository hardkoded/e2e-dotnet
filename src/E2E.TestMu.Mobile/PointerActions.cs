// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json.Nodes;

namespace E2E.TestMu.Mobile;

/// <summary>The W3C Actions payloads (<c>POST /actions</c>) the client sends: one touch pointer, or one keyboard.</summary>
internal static class PointerActions
{
    /// <summary>WebDriver's code for the Enter key.</summary>
    public const string Enter = "";

    /// <summary>A tap, or <paramref name="count"/> taps in one sequence, 100 ms apart.</summary>
    public static JsonObject Tap(double x, double y, int count = 1)
    {
        var steps = new JsonArray(Move(x, y, 0));
        for (var tap = 0; tap < count; tap++)
        {
            if (tap > 0)
            {
                steps.Add(Pause(100));
            }

            steps.Add(Down());
            steps.Add(Pause(50));
            steps.Add(Up());
        }

        return Touch(steps);
    }

    /// <summary>A hold: down, wait, up.</summary>
    public static JsonObject LongPress(double x, double y, double durationMilliseconds) =>
        Touch(new JsonArray(Move(x, y, 0), Down(), Pause(durationMilliseconds), Up()));

    /// <summary>A drag from one point to another over <paramref name="durationMilliseconds"/>. <paramref name="holdMilliseconds"/> waits on the first point before it moves.</summary>
    public static JsonObject Drag(double fromX, double fromY, double toX, double toY, double durationMilliseconds, double holdMilliseconds = 0)
    {
        var steps = new JsonArray(Move(fromX, fromY, 0), Down());
        if (holdMilliseconds > 0)
        {
            steps.Add(Pause(holdMilliseconds));
        }

        steps.Add(Move(toX, toY, durationMilliseconds));
        steps.Add(Up());
        return Touch(steps);
    }

    /// <summary>The keys of <paramref name="text"/>, one down and one up each. A surrogate pair is one key.</summary>
    public static JsonObject Keys(string text)
    {
        var steps = new JsonArray();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var key = (string)elements.Current;
            steps.Add(new JsonObject { ["type"] = "keyDown", ["value"] = key });
            steps.Add(new JsonObject { ["type"] = "keyUp", ["value"] = key });
        }

        return new JsonObject { ["actions"] = new JsonArray(new JsonObject { ["type"] = "key", ["id"] = "keyboard", ["actions"] = steps }) };
    }

    private static JsonObject Touch(JsonArray steps) => new()
    {
        ["actions"] = new JsonArray(new JsonObject
        {
            ["type"] = "pointer",
            ["id"] = "finger",
            ["parameters"] = new JsonObject { ["pointerType"] = "touch" },
            ["actions"] = steps,
        }),
    };

    // The spec takes whole pixels.
    private static int Whole(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static JsonObject Move(double x, double y, double durationMilliseconds) => new()
    {
        ["type"] = "pointerMove",
        ["duration"] = Whole(durationMilliseconds),
        ["origin"] = "viewport",
        ["x"] = Whole(x),
        ["y"] = Whole(y),
    };

    private static JsonObject Down() => new() { ["type"] = "pointerDown", ["button"] = 0 };

    private static JsonObject Up() => new() { ["type"] = "pointerUp", ["button"] = 0 };

    private static JsonObject Pause(double durationMilliseconds) => new() { ["type"] = "pause", ["duration"] = Whole(durationMilliseconds) };
}
