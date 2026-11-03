// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TranscriptCompaction;

/// <summary>
/// The messages upstream's <c>transcript-compaction.test.ts</c> builds, in the port's shape: a message holds one text and its images,
/// a screen is printed whole (<c>Screen (/):</c>) where upstream prints <c>Current screen (revision ...)</c>, and a screenshot rides a user
/// turn after the tool results (<c>ToolLoopResult.Pixels</c>) where upstream's rides the result.
/// </summary>
internal static class TranscriptMessages
{
    public static readonly ModelImage Png = new([1, 2, 3], "image/png", 1, 1);

    public static ModelMessage Opening(string text) => new() { Role = "user", Content = text };

    public static ModelMessage OpeningWithScreen { get; } = Opening("Goal: do the thing\n\nScreen (/):\n- document \"Home\" [ref=e1]\n  - heading \"Home\" [ref=e2]");

    public static ModelMessage FullScreenResult(string id, int rows = 1) => new()
    {
        Role = "tool",
        ToolCallId = id,
        Name = "tap",
        Content = "Tapped #e3.\nScreen (/):\n- document [ref=e1]\n" + string.Join('\n', Enumerable.Range(0, rows).Select(index => $"  - heading \"X{index}\" [ref=e{index + 2}]")),
    };

    public static ModelMessage ChangeResult(string id) => new() { Role = "tool", ToolCallId = id, Name = "type", Content = "Typed into #e6. The field shows a." };

    public static ModelMessage Screenshot(string id) => new() { Role = "user", Content = "Screenshot attached to the result of " + id + ".", Images = [Png] };

    public static int ImageCount(IEnumerable<ModelMessage> messages) => messages.Sum(message => message.Images?.Count ?? 0);

    public static int TextChars(IEnumerable<ModelMessage> messages) => messages.Sum(message => message.Content?.Length ?? 0);
}
