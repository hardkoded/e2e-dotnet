// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>One URL a <see cref="DownloadFunction"/> is asked for (an element of the options array of upstream's <c>DownloadFunction</c>).</summary>
/// <param name="Url">The URL of the file.</param>
/// <param name="IsUrlSupportedByModel">Whether the model takes the URL as is. Decision models take no URLs, so <c>decide</c> always says false.</param>
public sealed record DownloadRequest(Uri Url, bool IsUrlSupportedByModel);

/// <summary>A file a <see cref="DownloadFunction"/> downloaded.</summary>
/// <param name="Data">The bytes of the file.</param>
/// <param name="MediaType">The media type of the file, when the server named one.</param>
public sealed record DownloadedFile(byte[] Data, string? MediaType);

/// <summary>
/// The AI SDK's <c>DownloadFunction</c> (<c>util/download/download-function.ts</c>), experimental. Called with the requested URLs, it answers
/// one entry for each, in the same order: the downloaded file, or null when the URL goes to the model as is. It throws
/// <see cref="DownloadException"/> when a download fails.
/// </summary>
public delegate Task<IReadOnlyList<DownloadedFile?>> DownloadFunction(
    IReadOnlyList<DownloadRequest> requests,
    CancellationToken cancellationToken
);

/// <summary>The type of the AI SDK's <c>download</c> (<c>typeof originalDownload</c>), which <see cref="CreateDefaultDownloadFunction.Create"/> takes. Receives the requested URL and the signal of the enclosing call.</summary>
public delegate Task<DownloadedFile> DownloadFile(DownloadRequest request, CancellationToken cancellationToken);

/// <summary>The AI SDK's <c>createDefaultDownloadFunction</c> (<c>util/download/download-function.ts</c>).</summary>
public static class CreateDefaultDownloadFunction
{
    /// <summary>
    /// Default download function. Downloads the file if it is not supported by the model. <paramref name="cancellationToken"/> (upstream's
    /// <c>abortSignal</c>) wins over the token of the call.
    /// </summary>
    /// <param name="download">The single-request download. Defaults to the AI SDK's <c>download</c>.</param>
    /// <param name="cancellationToken">A token passed to every download instead of the call's.</param>
    public static DownloadFunction Create(DownloadFile? download = null, CancellationToken? cancellationToken = null)
    {
        download ??= static (request, token) => Downloads.DownloadAsync(request.Url, null, null, token);
        return async (requestedDownloads, token) =>
            await Task.WhenAll(
                    requestedDownloads.Select(async requestedDownload =>
                        requestedDownload.IsUrlSupportedByModel
                            ? null
                            : await download(requestedDownload, cancellationToken ?? token).ConfigureAwait(false)
                    )
                )
                .ConfigureAwait(false);
    }
}
