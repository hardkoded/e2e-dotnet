// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>download</c> (<c>util/download/download.ts</c>). Named <c>Downloads</c> because a C# class cannot share its namespace member's name with a method.</summary>
internal static class Downloads
{
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
        try
        {
            response = await FetchUntrustedUrl
                .FetchAsync(
                    urlText,
                    new Dictionary<string, string> { ["user-agent"] = "ai-sdk/e2e-dotnet" },
                    fetch,
                    cancellationToken
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
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new DownloadedFile(data, response.Content.Headers.ContentType is { } type ? MediaType(type) : null);
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

    /// <summary>The <c>content-type</c> header as the server sent it (parameters included), as <c>headers.get('content-type')</c>.</summary>
    private static string MediaType(MediaTypeHeaderValue type) => type.ToString();
}
