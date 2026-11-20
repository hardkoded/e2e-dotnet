// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.ReadResponseWithSizeLimit;

/// <summary>The AI SDK's <c>read-response-with-size-limit.test.ts</c>, <c>describe('readResponseWithSizeLimit')</c>.</summary>
public sealed class ReadResponseWithSizeLimitTests
{
    private static async Task<byte[]> ReadAsync(
        MockResponse mock,
        string url,
        long maxBytes = Sdk.ReadResponseWithSizeLimit.DefaultMaxDownloadSize
    ) => await Sdk.ReadResponseWithSizeLimit.ReadAsync(mock.Response, url, maxBytes);

    // The C# stream hands out bytes into the caller's buffer, so what upstream proves for a view into a larger backing buffer
    // (an offset, a Node Buffer) is proved here for a slice into a larger array.
    [Theory]
    [InlineData("Uint8Array with a nonzero byte offset")]
    [InlineData("Node Buffer with a larger backing buffer")]
    public async Task Should_read_a_view_with_a_larger_backing_buffer_correctly(string name)
    {
        var backing = new byte[64 * 1024];
        byte[] slice = [1, 2, 3];
        slice.CopyTo(backing, 17);
        ReadOnlyMemory<byte> chunk = name.StartsWith("Uint8Array", StringComparison.Ordinal)
            ? backing.AsMemory(17, 3)
            : new ArraySegment<byte>(backing, 17, 3);

        using var mock = MockResponse.CreateWithChunk(chunk);
        var result = await ReadAsync(mock, "http://example.com/file");

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    // Upstream spies on Uint8Array.prototype.set to see the chunk passed to concatenation. A .NET stream copies into the reader's own
    // buffer and the result is built by MemoryStream.ToArray(), so the same property is asserted on the result: it holds exactly the
    // bytes of the chunk, and it is not the oversized backing buffer.
    [Fact]
    public async Task Should_concatenate_compact_chunks_instead_of_oversized_backing_buffers()
    {
        var backing = new byte[64 * 1024];
        var chunk = new ArraySegment<byte>(backing, 17, 3);
        byte[] expected = [1, 2, 3];
        expected.CopyTo(backing, 17);
        using var mock = MockResponse.CreateWithChunk(chunk);

        var result = await ReadAsync(mock, "http://example.com/file");

        Assert.Equal(expected, result);
        Assert.Equal(chunk.Count, result.Length);
        Assert.NotSame(backing, result);
    }

    [Fact]
    public async Task Should_read_response_within_limit_successfully()
    {
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8];
        using var mock = MockResponse.Create(data, contentLength: "8");

        var result = await ReadAsync(mock, "http://example.com/file", 100);

        Assert.Equal(data, result);
    }

    [Fact]
    public async Task Should_reject_when_Content_Length_exceeds_limit_early_check()
    {
        using var mock = MockResponse.Create(new byte[10], contentLength: "1000");

        var error = await Assert.ThrowsAsync<DownloadException>(() => ReadAsync(mock, "http://example.com/large", 100));

        Assert.Contains("Content-Length: 1000", error.Message);
    }

    [Fact]
    public async Task Should_cancel_the_body_when_Content_Length_exceeds_limit_prevents_socket_leak()
    {
        using var mock = MockResponse.Create(new byte[10], contentLength: "1000");

        await Assert.ThrowsAnyAsync<Exception>(() => ReadAsync(mock, "http://example.com/large", 100));

        Assert.True(mock.Cancelled);
    }

    [Fact]
    public async Task Should_abort_when_streamed_bytes_exceed_limit()
    {
        // Body is larger than maxBytes, but Content-Length is not set
        var largeBody = new byte[200];
        Array.Fill(largeBody, (byte)42);
        using var mock = MockResponse.Create(largeBody);

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            ReadAsync(mock, "http://example.com/streaming", 50)
        );

        Assert.Contains("exceeded maximum size of 50 bytes", error.Message);
    }

    [Fact]
    public async Task Should_preserve_streamed_size_limit_errors_when_cancellation_fails()
    {
        using var mock = MockResponse.Create([1, 2], failCancel: true);

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            ReadAsync(mock, "http://example.com/streaming", 1)
        );

        Assert.Equal("Download of http://example.com/streaming exceeded maximum size of 1 bytes.", error.Message);
        Assert.True(mock.Cancelled);
    }

    [Fact]
    public async Task Should_handle_lying_Content_Length_says_small_sends_large()
    {
        var largeBody = new byte[200];
        Array.Fill(largeBody, (byte)42);
        using var mock = MockResponse.Create(largeBody, contentLength: "10"); // Claims to be small

        var error = await Assert.ThrowsAsync<DownloadException>(() => ReadAsync(mock, "http://example.com/liar", 50));

        Assert.Contains("exceeded maximum size of 50 bytes", error.Message);
    }

    [Fact]
    public async Task Should_handle_empty_body_null()
    {
        // A .NET response always has a content: an absent body is the empty one.
        using var mock = MockResponse.Create([]);

        var result = await ReadAsync(mock, "http://example.com/empty", 100);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_handle_empty_body_zero_length()
    {
        using var mock = MockResponse.Create([]);

        var result = await ReadAsync(mock, "http://example.com/empty", 100);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Should_respect_custom_maxBytes()
    {
        var data = new byte[10];
        Array.Fill(data, (byte)1);
        using var mock = MockResponse.Create(data, contentLength: "10");

        var result = await ReadAsync(mock, "http://example.com/custom", 10);

        Assert.Equal(data, result);
    }

    [Fact]
    public async Task Should_reject_at_exact_boundary_maxBytes_plus_1()
    {
        var data = new byte[11];
        Array.Fill(data, (byte)1);
        using var mock = MockResponse.Create(data);

        await Assert.ThrowsAsync<DownloadException>(() => ReadAsync(mock, "http://example.com/boundary", 10));
    }
}
