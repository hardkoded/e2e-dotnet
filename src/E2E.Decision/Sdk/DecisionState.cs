// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>decision-state.ts</c>.</summary>
internal static class DecisionState
{
    /// <summary>
    /// The AI SDK's <c>prepareDecisionState</c>: the state a model is called with. A URL file part is downloaded through
    /// <paramref name="download"/> and becomes a <see cref="DecisionFilePart"/> with the bytes; every other part passes through.
    /// </summary>
    /// <exception cref="DownloadException">A URL file part has no <paramref name="download"/> function, or a download failed.</exception>
    internal static async Task<IReadOnlyList<DecisionStatePart>> PrepareAsync(
        IReadOnlyList<DecisionStatePart> state,
        DownloadFunction? download,
        CancellationToken cancellationToken
    )
    {
        var snapshot = state.ToList();
        var files = snapshot.OfType<DecisionFileUrlPart>().ToList();
        if (files.Count == 0)
        {
            return state;
        }

        // Do not silently fetch URL state through the runtime's default HTTP client: URL and literal-IP validation alone
        // cannot protect against private DNS answers.
        download ??= (requests, _) =>
            requests.Count > 0
                ? throw new DownloadException(
                    requests[0].Url.ToString(),
                    message: "URL file state requires experimental_download. For untrusted URLs, use CreateDownload.Create(new CreateDownloadOptions { Fetch = ... }) with a transport that validates DNS at connection time."
                )
                : Task.FromResult<IReadOnlyList<DownloadedFile?>>([]);
        var downloaded = await download(
                [.. files.Select(file => new DownloadRequest(file.Url, IsUrlSupportedByModel: false))],
                cancellationToken
            )
            .ConfigureAwait(false);
        var fileIndex = 0;
        var prepared = new List<DecisionStatePart>(snapshot.Count);
        foreach (var part in snapshot)
        {
            if (part is not DecisionFileUrlPart file)
            {
                prepared.Add(part);
                continue;
            }

            var result = downloaded[fileIndex++]
                ?? throw new DownloadException(
                    file.Url.ToString(),
                    message: "The download function returned no data for " + file.Url
                );
            // The downloaded media type replaces a partial one (such as `image`), never a full one.
            var mediaType =
                result.MediaType is not null && !IsFullMediaType(file.MediaType) ? result.MediaType : file.MediaType;
            prepared.Add(new DecisionFilePart(mediaType, result.Data));
        }

        return prepared;
    }

    /// <summary>The AI SDK's <c>isFullMediaType</c>: a type and a subtype that is not a wildcard.</summary>
    private static bool IsFullMediaType(string mediaType)
    {
        var slash = mediaType.IndexOf('/');
        return slash != -1 && mediaType.Length > slash + 1 && mediaType[(slash + 1)..] != "*";
    }
}
