// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>readResponseWithSizeLimit</c> and <c>DEFAULT_MAX_DOWNLOAD_SIZE</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class ReadResponseWithSizeLimit
{
    /// <summary>The default maximum download size: 2 GiB.</summary>
    internal const long DefaultMaxDownloadSize = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Reads a response body with a size limit, to prevent memory exhaustion. Rejects early on <c>Content-Length</c>, then reads the body in
    /// chunks and stops with a <see cref="DownloadException"/> when the limit is passed. With <paramref name="onChunk"/>, the chunks go to it instead
    /// of being collected: the limit is enforced before a chunk is passed on, the result is empty, and an error of the callback cancels the body and
    /// surfaces as is.
    /// </summary>
    internal static async Task<byte[]> ReadAsync(
        HttpResponseMessage response,
        string url,
        long maxBytes = DefaultMaxDownloadSize,
        Action<ReadOnlyMemory<byte>>? onChunk = null,
        CancellationToken cancellationToken = default
    )
    {
        // A byte array cannot hold more than Array.MaxLength, whatever the caller allows.
        maxBytes = Math.Min(maxBytes, Array.MaxLength);
        if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
        {
            // Cancel the body, so the socket is not left open.
            response.Content.Dispose();
            throw new DownloadException(
                url,
                message: "Download of "
                    + url
                    + " exceeded maximum size of "
                    + maxBytes
                    + " bytes (Content-Length: "
                    + length
                    + ")."
            );
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var result = new MemoryStream();
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (total + read > maxBytes)
                {
                    throw new DownloadException(
                        url,
                        message: "Download of " + url + " exceeded maximum size of " + maxBytes + " bytes."
                    );
                }

                total += read;
                if (onChunk is null)
                {
                    result.Write(buffer, 0, read);
                }
                else
                {
                    onChunk(buffer.AsMemory(0, read));
                }
            }

            await stream.DisposeAsync().ConfigureAwait(false);
            return result.ToArray();
        }
        catch
        {
            // Cancel the body without letting a failing cancel replace the error that stopped the read.
            _ = await CancelBodyAsync(stream).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<Exception?> CancelBodyAsync(Stream stream)
    {
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }
}
