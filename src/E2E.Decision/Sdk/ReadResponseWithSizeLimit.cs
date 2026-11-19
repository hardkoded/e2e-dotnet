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
    /// chunks and stops with a <see cref="DownloadException"/> when the limit is passed.
    /// </summary>
    internal static async Task<byte[]> ReadAsync(
        HttpResponseMessage response,
        string url,
        long maxBytes,
        CancellationToken cancellationToken
    )
    {
        // A byte array cannot hold more than Array.MaxLength, whatever the caller allows.
        maxBytes = Math.Min(maxBytes, Array.MaxLength);
        if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
        {
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
        await using (stream.ConfigureAwait(false))
        {
            using var result = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (result.Length + read > maxBytes)
                {
                    throw new DownloadException(
                        url,
                        message: "Download of " + url + " exceeded maximum size of " + maxBytes + " bytes."
                    );
                }

                result.Write(buffer, 0, read);
            }

            return result.ToArray();
        }
    }
}
