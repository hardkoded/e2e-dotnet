// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>A successful answer read as a stream: its body, status, and headers (lowercase names). Disposing it releases the HTTP response.</summary>
internal sealed class ApiStreamResponse(
    HttpResponseMessage response,
    Stream body,
    int statusCode,
    IReadOnlyDictionary<string, string> responseHeaders
) : IDisposable
{
    public Stream Body { get; } = body;

    public int StatusCode { get; } = statusCode;

    public IReadOnlyDictionary<string, string> ResponseHeaders { get; } = responseHeaders;

    public void Dispose() => response.Dispose();
}

/// <summary>Upstream's <c>ParseResult</c> (<c>parse-json.ts</c>): the parsed value of one event, or the error that stopped it parsing.</summary>
/// <param name="Value">The parsed JSON, when it parsed.</param>
/// <param name="Error">The error, when it did not.</param>
internal sealed record ParseResult(JsonElement? Value, Exception? Error);

/// <summary>
/// The AI SDK's <c>provider-utils</c> request helpers the decision models share (<c>postJsonToApi</c>, <c>postFormDataToApi</c>,
/// <c>combineHeaders</c>): one POST, no retry (the decide call owns the retries), and the failure as an <see cref="ApiCallException"/>.
/// </summary>
internal static class ProviderUtils
{
    // DEFAULT_MAX_JSON_LINE_BYTES: 64 MiB.
    private const int DefaultMaxJsonLineBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Upstream's <c>withUserAgentSuffix</c> (<c>provider-utils/src/with-user-agent-suffix.ts</c>): the headers with the suffix appended to
    /// the <c>user-agent</c> the caller set, or as the <c>user-agent</c> when there is none or it is empty.
    /// </summary>
    public static Dictionary<string, string> WithUserAgentSuffix(
        IReadOnlyDictionary<string, string>? headers,
        string suffix
    )
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            result[name] = value;
        }

        result["user-agent"] =
            result.TryGetValue("user-agent", out var existing) && !string.IsNullOrEmpty(existing)
                ? existing + " " + suffix
                : suffix;
        return result;
    }

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

    /// <summary>Upstream's <c>getFromApi</c>: one GET, no retry, and the failure as an <see cref="ApiCallException"/>.</summary>
    public static async Task<ApiResponse> GetFromApiAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string>? headers,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    )
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            message.Headers.TryAddWithoutValidation(name, value);
        }

        return await SendAsync(http, message, errorToMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Upstream's <c>postFormDataToApi</c>.</summary>
    public static Task<ApiResponse> PostFormDataToApiAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        MultipartFormDataContent form,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    ) => PostAsync(http, url, headers, form, errorToMessage, cancellationToken);

    /// <summary>
    /// Upstream's <c>postJsonToApi</c> with <c>createJsonLinesResponseHandler</c> (<c>response-handler.ts</c>): one POST, and the answer's JSON Lines
    /// as a lazy stream of parsed lines. A line of more than <paramref name="maxLineBytes"/> bytes (the line feed excluded) fails the stream with a
    /// <see cref="DownloadException"/>; an empty line is skipped; <paramref name="validate"/> throws for a line that breaks the schema. The stream
    /// owns the response and releases it when its enumeration ends, fails, or is stopped early, so the caller must enumerate it (upstream's stream
    /// likewise holds the connection until it is consumed or cancelled). A read that gets no data within the model timeout fails with a
    /// <see cref="TimeoutException"/>.
    /// </summary>
    public static async Task<IAsyncEnumerable<JsonNode>> PostJsonToApiForJsonLinesAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string body,
        Func<int, string, string> errorToMessage,
        Action<JsonNode> validate,
        int? maxLineBytes,
        CancellationToken cancellationToken
    )
    {
        // createJsonLinesResponseHandler checks its option when the handler is made, before any request.
        var limit = maxLineBytes ?? DefaultMaxJsonLineBytes;
        if (limit <= 0)
        {
            throw new InvalidArgumentException("maxLineBytes", "maxLineBytes must be a positive safe integer.");
        }

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        foreach (var (name, value) in headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                content.Headers.Remove(name);
                content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
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
            throw new ApiCallException(
                "Cannot connect to API: " + error.Message,
                isRetryable: true,
                innerException: error
            );
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                string text;
                using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    readTimeout.CancelAfter(ModelHttp.Timeout);
                    try
                    {
                        text = await response.Content.ReadAsStringAsync(readTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException("The error response body stalled.");
                    }
                }

                var status = (int)response.StatusCode;
                throw new ApiCallException(errorToMessage(status, text), status, ReadHeaders(response))
                {
                    ResponseBody = text,
                };
            }
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return ParseJsonLinesAsync(response, url, validate, limit, cancellationToken);
    }

    // upstream's parseJsonLines: the line is bounded before it is decoded, and the stream is released when the consumer stops.
    private static async IAsyncEnumerable<JsonNode> ParseJsonLinesAsync(
        HttpResponseMessage response,
        string url,
        Action<JsonNode> validate,
        int maxLineBytes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var line = new MemoryStream();
            var chunk = new byte[16 * 1024];
            while (true)
            {
                int read;
                using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    readTimeout.CancelAfter(ModelHttp.Timeout);
                    try
                    {
                        read = await stream.ReadAsync(chunk, readTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException("The JSON Lines response stalled.");
                    }
                }

                if (read == 0)
                {
                    break;
                }

                var offset = 0;
                while (offset < read)
                {
                    var lineEnd = Array.IndexOf(chunk, (byte)10, offset, read - offset);
                    var segmentEnd = lineEnd == -1 ? read : lineEnd;
                    if (line.Length + (segmentEnd - offset) > maxLineBytes)
                    {
                        throw new DownloadException(
                            url,
                            message: "JSON Lines response exceeded maximum line size of "
                                + maxLineBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + " bytes."
                        );
                    }

                    line.Write(chunk, offset, segmentEnd - offset);
                    if (lineEnd == -1)
                    {
                        break;
                    }

                    offset = lineEnd + 1;
                    if (ParseLine(line, validate) is { } parsed)
                    {
                        yield return parsed;
                    }
                }
            }

            if (ParseLine(line, validate) is { } last)
            {
                yield return last;
            }
        }
    }

    private static JsonNode? ParseLine(MemoryStream line, Action<JsonNode> validate)
    {
        var text = System.Text.Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length);
        line.SetLength(0);
        text = text.EndsWith('\r') ? text[..^1] : text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException error)
        {
            throw new InvalidResponseDataException("JSON parsing failed: " + error.Message);
        }

        if (node is null)
        {
            throw new InvalidResponseDataException("Type validation failed: a JSON Lines line must not be null.");
        }

        validate(node);
        return node;
    }

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

        return await SendAsync(http, message, errorToMessage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ApiResponse> SendAsync(
        HttpClient http,
        HttpRequestMessage message,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    )
    {
        var response = await SendRequestAsync(
                http,
                message,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken
            )
            .ConfigureAwait(false);
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

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient http,
        HttpRequestMessage message,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await http.SendAsync(message, completionOption, cancellationToken).ConfigureAwait(false);
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
    }

    /// <summary>
    /// Upstream's <c>postJsonToApi</c> with <c>createEventSourceResponseHandler</c>: one POST whose successful answer is read as a stream.
    /// .NET-only split: C# has no response handler argument, so the streaming answer has its own method. The caller owns the answer and
    /// disposes it, or hands it to <see cref="ParseJsonEventStreamAsync"/>, which does.
    /// </summary>
    public static async Task<ApiStreamResponse> PostJsonToApiStreamAsync(
        HttpClient http,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string body,
        Func<int, string, string> errorToMessage,
        CancellationToken cancellationToken
    )
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
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

        var response = await SendRequestAsync(
                http,
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            )
            .ConfigureAwait(false);
        try
        {
            var responseHeaders = ReadHeaders(response);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new ApiCallException(errorToMessage(status, text), status, responseHeaders)
                {
                    ResponseBody = text,
                };
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new ApiStreamResponse(response, stream, status, responseHeaders);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Upstream's <c>parseJsonEventStream</c> (<c>parse-json-event-stream.ts</c>): the data of each server-sent event as parsed JSON. The
    /// <c>[DONE]</c> event some providers send is ignored; an event whose data is not JSON is a failed <see cref="ParseResult"/>. A failure
    /// to read the body is an <see cref="ApiCallException"/> ("Failed to process successful response"), as upstream's
    /// <c>wrapResponseBodyStream</c> makes it. Cancellation is not wrapped. The enumeration owns <paramref name="response"/> and releases it
    /// when the stream ends, fails, or is disposed early.
    /// </summary>
    public static async IAsyncEnumerable<ParseResult> ParseJsonEventStreamAsync(
        ApiStreamResponse response,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var owned = response;
        using var reader = new StreamReader(response.Body, System.Text.Encoding.UTF8);
        var data = new System.Text.StringBuilder();
        var hasData = false;
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or HttpRequestException)
            {
                throw new ApiCallException(
                    "Failed to process successful response",
                    response.StatusCode,
                    response.ResponseHeaders,
                    innerException: error
                );
            }

            // A blank line, or the end of the stream, dispatches the event.
            if (line is null || line.Length == 0)
            {
                if (hasData)
                {
                    var text = data.ToString();
                    data.Clear();
                    hasData = false;
                    if (text != "[DONE]")
                    {
                        yield return ParseEventData(text);
                    }
                }

                if (line is null)
                {
                    yield break;
                }

                continue;
            }

            // A comment, or a field other than data, carries no data.
            if (line[0] == ':')
            {
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if ((colon < 0 ? line : line[..colon]) != "data")
            {
                continue;
            }

            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            if (hasData)
            {
                data.Append('\n');
            }

            data.Append(value);
            hasData = true;
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

    private static ParseResult ParseEventData(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return new ParseResult(document.RootElement.Clone(), null);
        }
        catch (JsonException error)
        {
            return new ParseResult(null, new JsonParseException(text, error));
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
