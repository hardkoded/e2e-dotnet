// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.MessageShape;

/// <summary>
/// Not ported: "read a Buffer by its bytes, like the plain array it holds" (a .NET image is a <c>byte[]</c> only) and "ignore
/// provider options" (a <see cref="ModelMessage"/> carries none). The last upstream test is partial: a file named by URL has no
/// counterpart, so only the payload of another length and content is checked.
/// </summary>
public sealed class MessageShapesTests
{
    private static byte[] Bytes() => [137, 80, 78, 71];

    private static ModelMessage Screenshot(byte[]? data = null) => new()
    {
        Role = "user",
        Content = "Execute this test step: look.",
        Images = [new ModelImage(data ?? Bytes(), "image/png", 1, 1)],
    };

    private static ModelMessage Result(string value) => new() { Role = "tool", ToolCallId = "c1", Name = "tap", Content = value };

    [Fact]
    public void Match_for_a_clone_of_the_same_message_bytes_included()
    {
        var message = Screenshot();
        var clone = new ModelMessage { Role = message.Role, Content = message.Content, Images = [new ModelImage([.. Bytes()], "image/png", 1, 1)] };

        Assert.Equal(E2E.MessageShape.ShapeOf(message), E2E.MessageShape.ShapeOf(clone));
    }

    [Fact]
    public void Differ_when_a_text_a_tool_result_or_the_role_changes()
    {
        var before = E2E.MessageShape.ShapeOf(Result("looked"));

        Assert.NotEqual(before, E2E.MessageShape.ShapeOf(Result("looked again")));
        Assert.NotEqual(
            E2E.MessageShape.ShapeOf(new ModelMessage { Role = "user", Content = "x" }),
            E2E.MessageShape.ShapeOf(new ModelMessage { Role = "assistant", Content = "x" }));
    }

    [Fact]
    public void Are_taken_from_the_bytes_so_an_edit_to_the_live_array_after_the_snapshot_shows()
    {
        var message = Screenshot();
        var before = E2E.MessageShape.ShapeOf(message);

        message.Images![0].Data[0] = 0;

        Assert.NotEqual(before, E2E.MessageShape.ShapeOf(message));
        Assert.Equal(before, E2E.MessageShape.ShapeOf(Screenshot()));
    }

    [Fact]
    public void Differ_for_payloads_of_another_length_or_content_PARTIAL()
    {
        var screenshot = E2E.MessageShape.ShapeOf(Screenshot());

        Assert.NotEqual(screenshot, E2E.MessageShape.ShapeOf(Screenshot([137, 80, 78])));
        Assert.NotEqual(screenshot, E2E.MessageShape.ShapeOf(Screenshot([137, 80, 78, 72])));
    }
}
