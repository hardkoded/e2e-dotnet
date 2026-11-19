// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.ReadResponseWithSizeLimit;

/// <summary>The AI SDK's <c>read-response-with-size-limit.test.ts</c>, <c>describe('onChunk')</c> inside <c>describe('readResponseWithSizeLimit')</c>.</summary>
public sealed class ReadResponseWithSizeLimitOnChunkTests
{
    private static Task<byte[]> ReadAsync(
        MockResponse mock,
        long maxBytes = Sdk.ReadResponseWithSizeLimit.DefaultMaxDownloadSize,
        Action<ReadOnlyMemory<byte>>? onChunk = null
    ) => Sdk.ReadResponseWithSizeLimit.ReadAsync(mock.Response, "http://example.com/file", maxBytes, onChunk);

    [Fact]
    public async Task Should_pass_the_chunks_to_the_callback_instead_of_collecting_them()
    {
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        using var mock = MockResponse.Create(data);
        var received = new List<byte>();

        var result = await ReadAsync(mock, onChunk: chunk => received.AddRange(chunk.ToArray()));

        Assert.Equal(data, received);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_enforce_the_limit_before_passing_a_chunk_on()
    {
        using var mock = MockResponse.Create(new byte[15]);
        var calls = 0;

        await Assert.ThrowsAsync<DownloadException>(() => ReadAsync(mock, 10, _ => calls++));

        // Chunks of 4 bytes: the third one would bring the total to 12, and a
        // fourth one is still waiting to be read.
        Assert.Equal(2, calls);
        Assert.True(mock.Cancelled);
    }

    [Fact]
    public async Task Should_reject_on_Content_Length_above_the_limit_before_reading()
    {
        using var mock = MockResponse.Create(new byte[10], contentLength: "1000");
        var calls = 0;

        var error = await Assert.ThrowsAsync<DownloadException>(() => ReadAsync(mock, 100, _ => calls++));

        Assert.Contains("Content-Length: 1000", error.Message);
        Assert.Equal(0, calls);
        Assert.True(mock.Cancelled);
    }

    [Fact]
    public async Task Should_cancel_the_body_and_reject_with_the_error_of_the_callback()
    {
        using var mock = MockResponse.Create(new byte[10]);
        var failure = new InvalidOperationException("callback failed");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReadAsync(mock, onChunk: _ => throw failure)
        );

        Assert.Same(failure, error);
        Assert.True(mock.Cancelled);
    }
}
