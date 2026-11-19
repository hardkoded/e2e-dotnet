// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>download</c> (<c>util/download/download.ts</c>). Named <c>Downloads</c> because a C# class cannot share its namespace member's name with a method.</summary>
internal static class Downloads
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Downloads a file from a URL. Throws <see cref="DownloadException"/> when the download fails or passes <paramref name="maxBytes"/>.</summary>
    internal static async Task<DownloadedFile> DownloadAsync(
        Uri url,
        long? maxBytes,
        FetchFunction? fetch,
        CancellationToken cancellationToken
    )
    {
        var urlText = url.ToString();
        HttpResponseMessage? response = null;
        // .NET-only: the default client's timeout ends with the headers (ResponseHeadersRead), so the body read needs its own bound.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        var linked = timeout.Token;
        try
        {
            response = await FetchUntrustedUrl
                .FetchAsync(
                    urlText,
                    new Dictionary<string, string> { ["user-agent"] = "ai-sdk/e2e-dotnet" },
                    fetch,
                    linked
                )
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Release the connection before rejecting, so an error status cannot leak open sockets.
                throw new DownloadException(urlText, (int)response.StatusCode, response.ReasonPhrase);
            }

            var data = await ReadResponseWithSizeLimit
                .ReadAsync(
                    response,
                    urlText,
                    maxBytes ?? ReadResponseWithSizeLimit.DefaultMaxDownloadSize,
                    cancellationToken: linked
                )
                .ConfigureAwait(false);
            return new DownloadedFile(data, response.Content.Headers.ContentType?.MediaType);
        }
        catch (DownloadException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new DownloadException(urlText, innerException: error);
        }
        finally
        {
            response?.Dispose();
        }
    }
}
