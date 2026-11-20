// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>fetchWithValidatedRedirects</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class FetchWithValidatedRedirects
{
    internal const int DefaultMaxRedirects = 10;

    /// <summary>The request option that carries the <c>redirect</c> mode (<c>error</c>, <c>manual</c>, or <c>follow</c>) of <see cref="FetchEndpointAsync"/>.</summary>
    internal static readonly HttpRequestOptionsKey<string> RedirectOption = new("redirect");

    private static readonly HttpClient SharedClient = new(
        new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
    );

    /// <summary>
    /// Fetches a URL while enforcing the download guard on every hop. Redirects are followed by hand so each hop is validated before it is
    /// requested. A redirect from https to http is rejected, and a redirect across origins drops every caller header but <c>user-agent</c>.
    /// The returned response is the final, non-redirect one; the caller checks its status and reads the body. URL validation does not
    /// resolve DNS: inject a transport that does, or restrict network egress. A hop that is same-origin with <paramref name="trustedOrigin"/>
    /// (a developer-configured origin, never one taken from response data) skips URL validation.
    /// </summary>
    internal static async Task<HttpResponseMessage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? headers,
        FetchFunction? customFetch,
        CancellationToken cancellationToken,
        string? trustedOrigin = null,
        int maxRedirects = DefaultMaxRedirects
    )
    {
        var currentHeaders = headers is null ? null : SanitizeRequestHeaders.Sanitize(headers);
        var fetch = customFetch ?? DefaultSend;
        var currentUrl = url;
        // The bound also acts as a backstop against an unterminated redirect chain.
        for (var redirectCount = 0; redirectCount <= maxRedirects; redirectCount++)
        {
            // The developer-configured origin is trusted by definition; validating it would reject legitimate self-hosted deployments.
            if (trustedOrigin is null || !IsSameOrigin.Check(currentUrl, trustedOrigin))
            {
                ValidateDownloadUrl.Validate(currentUrl);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            request.Options.Set(RedirectOption, "manual");
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

        throw new DownloadException(url, message: "Too many redirects (max " + maxRedirects + ")");
    }

    /// <summary>
    /// Fetches one validated URL without following redirects (upstream's <c>fetchWithValidatedEndpoint</c>). Uses the supplied fetch or the
    /// default transport; <paramref name="redirect"/> reaches the fetch as the <see cref="RedirectOption"/> request option. With
    /// <paramref name="requireDnsValidation"/>, the default transport is rejected for an untrusted URL, since only an explicit custom fetch can
    /// provide connect-time DNS validation.
    /// </summary>
    internal static Task<HttpResponseMessage> FetchEndpointAsync(
        string url,
        HttpRequestMessage? init,
        FetchFunction? customFetch,
        string? trustedOrigin,
        string redirect,
        bool requireDnsValidation,
        CancellationToken cancellationToken
    )
    {
        if (trustedOrigin is null || !IsSameOrigin.Check(url, trustedOrigin))
        {
            ValidateDownloadUrl.Validate(url);
            if (requireDnsValidation && customFetch is null)
            {
                throw new DownloadException(
                    url,
                    message: "This server runtime requires a custom fetch with connect-time DNS validation for untrusted URLs"
                );
            }
        }

        var request = init ?? new HttpRequestMessage(HttpMethod.Get, url);
        request.Options.Set(RedirectOption, redirect);
        var fetch = customFetch ?? DefaultSend;
        return fetch(request, cancellationToken);
    }

    // The default transport. Like a runtime's native fetch, it answers a data: URL itself, because HttpClient has no data scheme.
    private static Task<HttpResponseMessage> DefaultSend(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.RequestUri is { Scheme: "data" } uri)
        {
            return Task.FromResult(DataUrlResponse(uri.OriginalString));
        }

        return SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static HttpResponseMessage DataUrlResponse(string url)
    {
        var comma = url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            throw new FormatException("Invalid data URL.");
        }

        var meta = url[5..comma];
        var payload = url[(comma + 1)..];
        var isBase64 = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        var mediaType = (isBase64 ? meta[..^7] : meta).Trim();
        var bytes = isBase64
            ? Convert.FromBase64String(Uri.UnescapeDataString(payload))
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
            mediaType.Length == 0 ? "text/plain;charset=US-ASCII" : mediaType
        );
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
    }
}
