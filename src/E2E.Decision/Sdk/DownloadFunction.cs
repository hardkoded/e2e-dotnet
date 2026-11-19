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
