// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.TestMu.Mobile.Tests;

internal static class Gesture
{
    /// <summary>
    /// A W3C actions body, one step per word: <c>move 125,220 (0ms)</c>, <c>down</c>, <c>pause 50</c>, <c>up</c>, and for a
    /// keyboard <c>key a</c>. The pointer must be a touch.
    /// </summary>
    public static string Describe(JsonNode? body)
    {
        var source = Assert.Single(body!["actions"]!.AsArray())!;
        if (source["type"]!.GetValue<string>() == "pointer")
        {
            Assert.Equal("touch", source["parameters"]!["pointerType"]!.GetValue<string>());
        }

        return string.Join(" | ", source["actions"]!.AsArray().Select(step => step!["type"]!.GetValue<string>() switch
        {
            "pointerMove" => $"move {step["x"]},{step["y"]} ({step["duration"]}ms)",
            "pointerDown" => "down",
            "pointerUp" => "up",
            "pause" => $"pause {step["duration"]}",
            "keyDown" => $"key {Escape(step["value"]!.GetValue<string>())}",
            "keyUp" => "release",
            var other => other,
        }));
    }

    private static string Escape(string key) => key.Length == 1 && key[0] < ' ' || key[0] >= '' ? $"U+{(int)key[0]:X4}" : key;
}
