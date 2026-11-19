// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;

namespace E2E.Decision.Tests.ReadResponseWithSizeLimit;

/// <summary>The upstream test's <c>createMockResponse</c> and <c>createResponseWithChunk</c>: a response that streams its body in small chunks and reports when its body was cancelled.</summary>
internal sealed class MockResponse : IDisposable
{
    private MockResponse(HttpResponseMessage response, ChunkedStream stream)
    {
        Response = response;
        Stream = stream;
    }

    internal HttpResponseMessage Response { get; }

    internal ChunkedStream Stream { get; }

    /// <summary>Whether the body was cancelled (disposed before the end).</summary>
    internal bool Cancelled => Stream.Disposed;

    /// <summary>Builds a response; the body is sent in chunks of 4 bytes, to simulate streaming.</summary>
    internal static MockResponse Create(byte[] body, string? contentLength = null, bool failCancel = false)
    {
        var stream = new ChunkedStream(body, 4, failCancel);
        var content = new StreamingContent(stream);
        if (contentLength is not null)
        {
            content.Headers.ContentLength = long.Parse(
                contentLength,
                System.Globalization.CultureInfo.InvariantCulture
            );
        }

        return new MockResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }, stream);
    }

    /// <summary>A response with one chunk, whatever its size.</summary>
    internal static MockResponse CreateWithChunk(ReadOnlyMemory<byte> chunk)
    {
        var stream = new ChunkedStream(chunk.ToArray(), int.MaxValue, false);
        return new MockResponse(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamingContent(stream) },
            stream
        );
    }

    public void Dispose() => Response.Dispose();

    /// <summary>A read-only stream that hands out at most <c>chunkSize</c> bytes per read and records its disposal.</summary>
    internal sealed class ChunkedStream(byte[] body, int chunkSize, bool failCancel) : Stream
    {
        private int _position;

        internal bool Disposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var count = Math.Min(Math.Min(chunkSize, buffer.Length), body.Length - _position);
            body.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask DisposeAsync()
        {
            var first = !Disposed;
            Disposed = true;
            return failCancel && first ? throw new InvalidOperationException("cancel failed") : ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            var first = !Disposed;
            Disposed = true;
            if (failCancel && disposing && first)
            {
                throw new InvalidOperationException("cancel failed");
            }
        }
    }

    private sealed class StreamingContent(ChunkedStream stream) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(stream);

        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
            stream.CopyToAsync(target);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
