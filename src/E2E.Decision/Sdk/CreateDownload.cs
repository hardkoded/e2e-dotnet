// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>What <see cref="CreateDownload.Create"/> takes (the options of the AI SDK's <c>createDownload</c>).</summary>
public sealed class CreateDownloadOptions
{
    /// <summary>The most bytes a download may hold. Defaults to 2 GiB.</summary>
    public long? MaxBytes { get; init; }

    /// <summary>The transport (upstream's <c>fetch</c>), for example one that validates DNS at connection time. Defaults to a shared <see cref="HttpClient"/>.</summary>
    public FetchFunction? Fetch { get; init; }
}

/// <summary>The AI SDK's <c>createDownload</c> (<c>util/download/create-download.ts</c>).</summary>
public static class CreateDownload
{
    /// <summary>
    /// Creates a download function that downloads every URL the model does not support. Pass it as
    /// <see cref="DecideOptions.ExperimentalDownload"/>. Upstream's also answers a single request (for <c>transcribe</c> and
    /// <c>experimental_generateVideo</c>); the port has neither, so only the array form exists.
    /// </summary>
    public static DownloadFunction Create(CreateDownloadOptions? options = null) =>
        async (requests, cancellationToken) =>
        {
            // One failed download cancels its siblings, so a failed call leaves no download reading in the background.
            using var batch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = requests
                .Select(request =>
                    request.IsUrlSupportedByModel
                        ? Task.FromResult<DownloadedFile?>(null)
                        : DownloadAsync(request, options, batch.Token)
                )
                .ToList();
            try
            {
                return await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                await batch.CancelAsync().ConfigureAwait(false);
                foreach (var task in tasks)
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch
                    {
                        // The first error is the one thrown; the siblings' are cancellations.
                    }
                }

                throw;
            }
        };

    private static async Task<DownloadedFile?> DownloadAsync(
        DownloadRequest request,
        CreateDownloadOptions? options,
        CancellationToken cancellationToken
    ) =>
        await Downloads
            .DownloadAsync(request.Url, options?.MaxBytes, options?.Fetch, cancellationToken)
            .ConfigureAwait(false);
}
