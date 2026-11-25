// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Engine.Protocol;

namespace E2E.Tests.EngineProtocol;

/// <summary>
/// The <c>it</c> of upstream's <c>describe('engine protocol against a server written from the messages alone')</c>
/// (<c>packages/e2e/tests/unit/engine-protocol.test.ts</c>) that need only <see cref="Connection"/>.
/// </summary>
public sealed class EngineProtocolAgainstAServerWrittenFromTheMessagesAloneTests
{
    private static byte[] Frame(JsonNode message)
    {
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        return [.. Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), .. body];
    }

    private static byte[] Note(JsonNode? parameters) =>
        Frame(
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "note",
                ["params"] = parameters,
            }
        );

    [Fact]
    public async Task Rejects_a_request_answered_with_a_malformed_error_instead_of_throwing_out_of_the_stream()
    {
        var input = new Pipe();
        var output = new Pipe();
        using var connection = new Connection(new MessageStreams(input.Reader.AsStream(), output.Writer.AsStream()));

        var pending = connection.RequestAsync("engine/observe", new JsonObject());
        await input
            .Writer.AsStream()
            .WriteAsync(
                Frame(
                    new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = 1,
                        ["error"] = null,
                    }
                )
            );

        var failure = await Assert.ThrowsAsync<RpcException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(-32603, failure.Code);
        Assert.Contains("malformed error", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_message_that_arrives_a_byte_at_a_time_and_two_that_arrive_in_one_chunk()
    {
        var input = new Pipe();
        var output = new Pipe();
        var seen = new ConcurrentQueue<JsonNode?>();
        using var connection = new Connection(
            new MessageStreams(input.Reader.AsStream(), output.Writer.AsStream()),
            new ConnectionOptions
            {
                Notifications = new Dictionary<string, Action<JsonNode?>>
                {
                    ["note"] = parameters => seen.Enqueue(parameters),
                },
            }
        );
        var writer = input.Writer.AsStream();

        foreach (var value in Note("één"))
        {
            await writer.WriteAsync(new[] { value });
            await writer.FlushAsync();
        }

        await writer.WriteAsync(Note(2).Concat(Note(3)).ToArray());
        await writer.WriteAsync(Note(4));
        await writer.FlushAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (seen.Count < 4 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(
            ["\"één\"", "2", "3", "4"],
            seen.Select(value =>
                value!.ToJsonString(
                    new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
                )
            )
        );
        Assert.Null(connection.Closed);
    }
}
