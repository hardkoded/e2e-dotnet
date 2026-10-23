// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.MessageShape;

/// <summary>
/// "read a Buffer by its bytes, like the plain array it holds" is not ported: .NET has one byte array type. The port has no
/// provider options on a message, so its provider material is the reasoning a message carries, and a file named by URL has no
/// counterpart: an image is always bytes.
/// </summary>
public sealed class MessageShapesTests
{
    private static byte[] Bytes() => [137, 80, 78, 71];

    private static ModelMessage Screenshot() => new()
    {
        Role = "user",
        Content = "Execute this test step: look.",
        Images = [new ModelImage(Bytes(), "image/png", 1, 1)],
    };

    private static ModelMessage Result(string value) => new() { Role = "tool", ToolCallId = "c1", Name = "tap", Content = value };

    [Fact]
    public void Match_for_a_clone_of_the_same_message_bytes_included()
    {
        var message = Screenshot();
        var clone = new ModelMessage { Role = message.Role, Content = message.Content, Images = [new ModelImage(Bytes(), "image/png", 1, 1)] };

        Assert.Equal(global::E2E.MessageShape.ShapeOf(message), global::E2E.MessageShape.ShapeOf(clone));
    }

    /// <summary>Partial port of "ignore provider options, where a cache breakpoint moves and a signature rides": the reasoning a message carries, signature and all, is the port's provider material.</summary>
    [Fact]
    public void Ignore_the_reasoning_a_message_carries_where_a_signature_rides()
    {
        var plain = new ModelMessage { Role = "assistant", Content = "ok" };
        var signed = new ModelMessage
        {
            Role = "assistant",
            Content = "ok",
            ReasoningItems = [JsonDocument.Parse("""{ "type": "thinking", "thinking": "", "signature": "sig" }""").RootElement.Clone()],
        };

        Assert.Equal(global::E2E.MessageShape.ShapeOf(plain), global::E2E.MessageShape.ShapeOf(signed));
    }

    [Fact]
    public void Differ_when_a_text_a_tool_result_below_its_part_or_the_role_changes()
    {
        var before = global::E2E.MessageShape.ShapeOf(Result("looked"));

        Assert.NotEqual(before, global::E2E.MessageShape.ShapeOf(Result("looked again")));
        Assert.NotEqual(before, global::E2E.MessageShape.ShapeOf(new ModelMessage { Role = "tool", ToolCallId = "c2", Name = "tap", Content = "looked" }));
        Assert.NotEqual(
            global::E2E.MessageShape.ShapeOf(new ModelMessage { Role = "user", Content = "x" }),
            global::E2E.MessageShape.ShapeOf(new ModelMessage { Role = "assistant", Content = "x" }));
    }

    [Fact]
    public void Are_taken_from_the_bytes_so_an_edit_to_the_live_array_after_the_snapshot_shows()
    {
        var message = Screenshot();
        var before = global::E2E.MessageShape.ShapeOf(message);

        message.Images![0].Data[0] = 0;

        Assert.NotEqual(before, global::E2E.MessageShape.ShapeOf(message));
        Assert.Equal(before, global::E2E.MessageShape.ShapeOf(Screenshot()));
    }

    /// <summary>Partial port of "differ for payloads of another length or content, and for a file named by URL against one sent as bytes": the port has no file named by URL.</summary>
    [Fact]
    public void Differ_for_payloads_of_another_length_or_content()
    {
        var shorter = new ModelMessage { Role = "user", Images = [new ModelImage([137, 80, 78], "image/png", 1, 1)] };
        var other = new ModelMessage { Role = "user", Images = [new ModelImage([137, 80, 78, 72], "image/png", 1, 1)] };
        var screenshot = new ModelMessage { Role = "user", Images = [new ModelImage(Bytes(), "image/png", 1, 1)] };

        Assert.NotEqual(global::E2E.MessageShape.ShapeOf(screenshot), global::E2E.MessageShape.ShapeOf(shorter));
        Assert.NotEqual(global::E2E.MessageShape.ShapeOf(screenshot), global::E2E.MessageShape.ShapeOf(other));
    }
}
