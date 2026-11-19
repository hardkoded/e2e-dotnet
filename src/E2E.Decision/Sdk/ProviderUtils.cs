// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text.Json;
using E2E.Internal;

namespace E2E.Decision.Sdk;

/// <summary>A successful provider answer: the parsed body, its raw text, and the response headers (lowercase names).</summary>
/// <param name="Value">The body, parsed.</param>
/// <param name="RawValue">The body, as the provider sent it.</param>
/// <param name="ResponseHeaders">The response headers, lowercase names.</param>
internal sealed record ApiResponse(
    JsonElement Value,
    string RawValue,
    IReadOnlyDictionary<string, string> ResponseHeaders
);

/// <summary>
/// The AI SDK's <c>provider-utils</c> request helpers the decision models share (<c>postJsonToApi</c>, <c>postFormDataToApi</c>,
/// <c>combineHeaders</c>): one POST, no retry (the decide call owns the retries), and the failure as an <see cref="ApiCallException"/>.
/// </summary>
internal static class ProviderUtils
{
    /// <summary>Upstream's <c>combineHeaders</c>: later headers replace earlier ones of the same name, and a null value drops the header.</summary>
    public static Dictionary<string, string> CombineHeaders(params IReadOnlyDictionary<string, string?>?[] headers)
    {
        var combined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in headers)
        {
            foreach (var (name, value) in set ?? new Dictionary<string, string?>())
            {
                if (value is null)
                {
                    combined.Remove(name);
                }
                else
                {
                    combined[name] = value;
                }
            }
        }

        return combined;
    }

    /// <summary>Upstream's <c>postJsonToApi</c>.</summary>
    public static Task<ApiResponse> PostJsonToApiAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string body,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    ) =>
        PostAsync(
            http,
            url,
            headers,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            errorToMessage,
            cancellationToken
        );

    /// <summary>Upstream's <c>postFormDataToApi</c>.</summary>
    public static Task<ApiResponse> PostFormDataToApiAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        MultipartFormDataContent form,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    ) => PostAsync(http, url, headers, form, errorToMessage, cancellationToken);

    private static async Task<ApiResponse> PostAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        HttpContent content,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    )
    {
        using var _ = content;
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        foreach (var (name, value) in headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                // A content header, such as Content-Type.
                content.Headers.Remove(name);
                content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiCallException(
                "Cannot connect to API: the request got no response within "
                    + ((int)ModelHttp.Timeout.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " seconds.",
                isRetryable: false,
                innerException: new TimeoutException()
            );
        }
        catch (HttpRequestException error)
        {
            // A request that got no answer, such as a refused or reset connection, may be sent again.
            throw new ApiCallException(
                "Cannot connect to API: " + error.Message,
                isRetryable: true,
                innerException: error
            );
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var responseHeaders = ReadHeaders(response);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiCallException(errorToMessage(status, text), status, responseHeaders)
                {
                    ResponseBody = text,
                };
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                return new ApiResponse(document.RootElement.Clone(), text, responseHeaders);
            }
            catch (JsonException error)
            {
                throw new ApiCallException("Invalid JSON response", status, responseHeaders, innerException: error)
                {
                    ResponseBody = text,
                };
            }
        }
    }

    /// <summary>
    /// Upstream's <c>postToApi</c> wrapper around a successful answer a handler could not process: anything but an <see cref="ApiCallException"/>
    /// the handler throws becomes one, with the status and headers of the answer.
    /// </summary>
    public static T ProcessSuccessfulResponse<T>(ApiResponse response, Func<JsonElement, T> read)
    {
        try
        {
            return read(response.Value);
        }
        catch (Exception error)
            when (error is InvalidResponseDataException or JsonException or InvalidOperationException)
        {
            throw new ApiCallException(
                "Failed to process successful response",
                200,
                response.ResponseHeaders,
                isRetryable: false,
                innerException: error
            )
            {
                ResponseBody = response.RawValue,
            };
        }
    }

    private static Dictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (
            var (name, values) in response.Headers.Concat<KeyValuePair<string, IEnumerable<string>>>(
                response.Content.Headers
            )
        )
        {
            headers[name.ToLowerInvariant()] = string.Join(", ", values);
        }

        return headers;
    }
}
