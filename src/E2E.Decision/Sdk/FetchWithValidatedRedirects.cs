// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>fetchWithValidatedRedirects</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class FetchWithValidatedRedirects
{
    private const int MaxDownloadRedirects = 10;

    private static readonly HttpClient SharedClient = new(
        new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
    );

    /// <summary>
    /// Fetches a URL while enforcing the download guard on every hop. Redirects are followed by hand so each hop is validated before it is
    /// requested. A redirect from https to http is rejected, and a redirect across origins drops every caller header but <c>user-agent</c>.
    /// The returned response is the final, non-redirect one; the caller checks its status and reads the body. URL validation does not
    /// resolve DNS: inject a transport that does, or restrict network egress.
    /// </summary>
    internal static async Task<HttpResponseMessage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? headers,
        FetchFunction? customFetch,
        CancellationToken cancellationToken
    )
    {
        var currentHeaders = headers is null ? null : SanitizeRequestHeaders.Sanitize(headers);
        var fetch = customFetch ?? ((request, token) => SharedClient.SendAsync(request, token));
        var currentUrl = url;
        // The bound also acts as a backstop against an unterminated redirect chain.
        for (var redirectCount = 0; redirectCount <= MaxDownloadRedirects; redirectCount++)
        {
            ValidateDownloadUrl.Validate(currentUrl);
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            if (currentHeaders is not null)
            {
                foreach (var (name, value) in currentHeaders)
                {
                    request.Headers.TryAddWithoutValidation(name, value);
                }
            }

            var response = await fetch(request, cancellationToken).ConfigureAwait(false);
            // Redirect status codes per the fetch spec: 300 and 304 are not redirects, even with a Location header.
            if (
                (int)response.StatusCode is 301 or 302 or 303 or 307 or 308
                && response.Headers.Location is { } location
            )
            {
                response.Dispose();
                var nextUrl = new Uri(new Uri(currentUrl), location).ToString();
                if (new Uri(currentUrl).Scheme == "https" && new Uri(nextUrl).Scheme == "http")
                {
                    throw new DownloadException(url, message: "Redirect from HTTPS to HTTP was blocked");
                }

                if (currentHeaders is not null && !IsSameOrigin.Check(nextUrl, currentUrl))
                {
                    currentHeaders = currentHeaders.TryGetValue("user-agent", out var userAgent)
                        ? new Dictionary<string, string> { ["user-agent"] = userAgent }
                        : [];
                }

                currentUrl = nextUrl;
                continue;
            }

            return response;
        }

        throw new DownloadException(url, message: "Too many redirects (max " + MaxDownloadRedirects + ")");
    }
}
