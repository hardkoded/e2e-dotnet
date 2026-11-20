// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace E2E.Decision.Sdk;

/// <summary>What <see cref="CreateDownload.Create"/> takes (the options of the AI SDK's <c>createDownload</c>).</summary>
public sealed class CreateDownloadOptions
{
    /// <summary>The most bytes a download may hold. Defaults to 2 GiB.</summary>
    public long? MaxBytes { get; init; }

    /// <summary>The transport (upstream's <c>fetch</c>), for example one that validates DNS at connection time. Defaults to a shared <see cref="HttpClient"/>.</summary>
    public FetchFunction? Fetch { get; init; }
}

/// <summary>
/// The AI SDK's <c>Download</c> type: the function <see cref="CreateDownload.Create"/> returns. Called with a single URL, it downloads that
/// URL. Called with an array of requests, it behaves like the default <c>experimental_download</c> of <c>decide</c>: URLs the model supports
/// are passed through (null), all others are downloaded. A <see cref="Download"/> converts to a <see cref="DownloadFunction"/>, so it can be
/// passed as <see cref="DecideOptions.ExperimentalDownload"/>.
/// </summary>
public sealed class Download
{
    private readonly CreateDownloadOptions? _options;

    internal Download(CreateDownloadOptions? options) => _options = options;

    /// <summary>Converts to the array form, for <see cref="DecideOptions.ExperimentalDownload"/>.</summary>
    public static implicit operator DownloadFunction(Download download) => download.DownloadAsync;

    /// <summary>Downloads a single URL. Throws <see cref="DownloadException"/> when the download fails.</summary>
    public Task<DownloadedFile> DownloadAsync(Uri url, CancellationToken cancellationToken = default) =>
        Downloads.DownloadAsync(url, _options?.MaxBytes, _options?.Fetch, cancellationToken);

    /// <summary>Downloads every URL the model does not support, and answers null for the others.</summary>
    public async Task<IReadOnlyList<DownloadedFile?>> DownloadAsync(
        IReadOnlyList<DownloadRequest> requests,
        CancellationToken cancellationToken = default
    )
    {
        // One failed download cancels its siblings, so a failed call leaves no download reading in the background.
        using var batch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The first failure, recorded before the siblings are cancelled, so nothing a sibling throws while it stops can replace it.
        var initiating = new StrongBox<Exception?>();
        var tasks = requests
            .Select(request =>
                request.IsUrlSupportedByModel
                    ? Task.FromResult<DownloadedFile?>(null)
                    : DownloadOneAsync(request, batch, initiating)
            )
            .ToList();
        try
        {
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            await batch.CancelAsync().ConfigureAwait(false);
            // Wait for the siblings and observe their errors.
            await Task.WhenAll(tasks).ContinueWith(static _ => { }, TaskScheduler.Default).ConfigureAwait(false);
            // A cancel by the caller surfaces as cancellation, whatever a download threw while it stopped.
            cancellationToken.ThrowIfCancellationRequested();
            if (initiating.Value is { } error)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            throw;
        }
    }

    // A failing download cancels the batch at once, so its siblings stop while they are still reading instead of after they finish.
    private async Task<DownloadedFile?> DownloadOneAsync(
        DownloadRequest request,
        CancellationTokenSource batch,
        StrongBox<Exception?> initiating
    )
    {
        try
        {
            return await DownloadAsync(request.Url, batch.Token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref initiating.Value, error, null);
            await batch.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>The AI SDK's <c>createDownload</c> (<c>util/download/create-download.ts</c>).</summary>
public static class CreateDownload
{
    /// <summary>
    /// Creates a download function with configurable options. Pass it as <see cref="DecideOptions.ExperimentalDownload"/>, or call it with a
    /// single URL.
    /// </summary>
    public static Download Create(CreateDownloadOptions? options = null) => new(options);
}
