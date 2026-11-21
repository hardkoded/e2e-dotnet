// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Tests;

/// <summary>One file or field of a multipart request body (what <c>requestBodyMultipart</c> gives upstream's tests).</summary>
internal sealed record MultipartPart(string Name, string? FileName, string? ContentType, byte[] Data)
{
    public string Text => Encoding.UTF8.GetString(Data);
}

/// <summary>One request a <see cref="TestServer"/> got.</summary>
internal sealed class TestServerCall(
    string url,
    IReadOnlyDictionary<string, string> headers,
    byte[] body,
    string? contentType,
    string method = "POST"
)
{
    public string RequestUrl { get; } = url;

    public string RequestMethod { get; } = method;

    /// <summary>The request headers, lowercase names.</summary>
    public IReadOnlyDictionary<string, string> RequestHeaders { get; } = headers;

    public byte[] RequestBody { get; } = body;

    public string RequestBodyText => Encoding.UTF8.GetString(RequestBody);

    public JsonNode? RequestBodyJson => JsonNode.Parse(RequestBodyText);

    /// <summary>The multipart parts in the order they were sent.</summary>
    public IReadOnlyList<MultipartPart> RequestBodyMultipart => ReadMultipart(contentType, RequestBody);

    private static List<MultipartPart> ReadMultipart(string? contentType, byte[] body)
    {
        var boundary = contentType?.Split("boundary=", 2)[1].Trim('"', ' ', ';');
        var delimiter = Encoding.UTF8.GetBytes("--" + boundary);
        var parts = new List<MultipartPart>();
        var positions = new List<int>();
        for (var i = 0; i <= body.Length - delimiter.Length; i++)
        {
            if (body.AsSpan(i, delimiter.Length).SequenceEqual(delimiter))
            {
                positions.Add(i);
            }
        }

        for (var p = 0; p < positions.Count - 1; p++)
        {
            // After the delimiter and its CRLF, the headers run to a blank line; the data ends before the CRLF that precedes the next delimiter.
            var start = positions[p] + delimiter.Length + 2;
            var end = positions[p + 1] - 2;
            var section = body.AsSpan(start, end - start).ToArray();
            var split = IndexOf(section, "\r\n\r\n"u8.ToArray());
            var headerText = Encoding.UTF8.GetString(section, 0, split);
            var data = section[(split + 4)..];
            string? name = null;
            string? fileName = null;
            string? partType = null;
            foreach (var line in headerText.Split("\r\n"))
            {
                var colon = line.IndexOf(':', StringComparison.Ordinal);
                var header = line[..colon].ToLowerInvariant();
                var value = line[(colon + 1)..].Trim();
                if (header == "content-type")
                {
                    partType = value;
                }
                else if (header == "content-disposition")
                {
                    name = Parameter(value, "name");
                    fileName = Parameter(value, "filename");
                }
            }

            parts.Add(new MultipartPart(name!, fileName, partType, data));
        }

        return parts;
    }

    private static string? Parameter(string header, string name)
    {
        foreach (var piece in header.Split(';'))
        {
            var trimmed = piece.Trim();
            if (trimmed.StartsWith(name + "=", StringComparison.Ordinal))
            {
                return trimmed[(name.Length + 1)..].Trim('"');
            }
        }

        return null;
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        for (var i = 0; i <= data.Length - pattern.Length; i++)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// .NET-only: upstream's <c>createTestServer</c> (<c>@ai-sdk/test-server</c>) as a message handler. It records every call and answers each URL
/// with the response set for it: a JSON body, or an error status.
/// </summary>
internal sealed class TestServer : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = new(StringComparer.Ordinal);
    private readonly List<TestServerCall> _calls = [];
    private readonly object _lock = new();

    public IReadOnlyList<TestServerCall> Calls
    {
        get
        {
            lock (_lock)
            {
                return _calls.ToList();
            }
        }
    }

    public HttpClient Client => new(this, disposeHandler: false);

    /// <summary>Answers <paramref name="url"/> with a JSON body (upstream's <c>{ type: 'json-value' }</c>).</summary>
    public void RespondJson(string url, object body, IReadOnlyDictionary<string, string>? headers = null) =>
        _responses[url] = () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    body is string text ? text : JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json"
                ),
            };
            foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            {
                response.Headers.TryAddWithoutValidation(name, value);
            }

            return response;
        };

    /// <summary>Answers <paramref name="url"/> with server-sent event chunks (upstream's <c>{ type: 'stream-chunks' }</c>).</summary>
    public void RespondStreamChunks(string url, IEnumerable<string> chunks)
    {
        var body = string.Concat(chunks);
        _responses[url] = () =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
            };
    }

    /// <summary>Answers <paramref name="url"/> with an error status and body (upstream's <c>{ type: 'error' }</c>).</summary>
    public void RespondError(string url, int status, string body) =>
        _responses[url] = () =>
            new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8) };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in request.Headers)
        {
            headers[name.ToLowerInvariant()] = string.Join(", ", values);
        }

        byte[] body = [];
        string? contentType = null;
        if (request.Content is not null)
        {
            foreach (var (name, values) in request.Content.Headers)
            {
                headers[name.ToLowerInvariant()] = string.Join(", ", values);
            }

            contentType = request.Content.Headers.ContentType?.ToString();
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        var url = request.RequestUri!.ToString();
        lock (_lock)
        {
            _calls.Add(new TestServerCall(url, headers, body, contentType, request.Method.Method));
        }

        // A URL that ends in "*" answers every URL that starts with the rest, as upstream's 'https://api.example.com/*'.
        var found = _responses.TryGetValue(url, out var respond);
        if (!found)
        {
            foreach (var (pattern, candidate) in _responses)
            {
                if (pattern.EndsWith('*') && url.StartsWith(pattern[..^1], StringComparison.Ordinal))
                {
                    respond = candidate;
                    found = true;
                    break;
                }
            }
        }

        return found
            ? respond!()
            : new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("no response set for " + url),
            };
    }
}
