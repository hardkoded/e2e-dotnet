// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests;

/// <summary>One request a <see cref="FetchMock"/> received, snapshotted when it was made (the request is disposed after the hop).</summary>
internal sealed record FetchCall(
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Redirect,
    CancellationToken Token
);

/// <summary>The C# counterpart of the <c>vi.fn().mockResolvedValueOnce(...)</c> fetch the provider-utils tests inject.</summary>
internal sealed class FetchMock
{
    private readonly Queue<Func<HttpResponseMessage>> _once = new();
    private Func<HttpResponseMessage>? _default;

    internal FetchMock()
    {
        Fetch = (request, token) =>
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            request.Options.TryGetValue(Sdk.FetchWithValidatedRedirects.RedirectOption, out var redirect);
            Calls.Add(new FetchCall(request.RequestUri!.OriginalString, headers, redirect, token));
            var factory = _once.Count > 0 ? _once.Dequeue() : _default;
            return Task.FromResult(
                factory is null ? throw new InvalidOperationException("Unexpected fetch call") : factory()
            );
        };
    }

    internal FetchFunction Fetch { get; }

    internal List<FetchCall> Calls { get; } = [];

    internal static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent([]) { Headers = { ContentType = new("image/png") } } };

    internal static HttpResponseMessage OpaqueRedirect() => Download.DownloadTestHelpers.OpaqueRedirect();

    internal static HttpResponseMessage Redirect(string location, int status = 302, HttpContent? body = null)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        if (body is not null)
        {
            response.Content = body;
        }

        return response;
    }

    internal FetchMock Once(Func<HttpResponseMessage> response)
    {
        _once.Enqueue(response);
        return this;
    }

    internal FetchMock Always(Func<HttpResponseMessage> response)
    {
        _default = response;
        return this;
    }
}

/// <summary>A response body that counts how often it was disposed, the counterpart of upstream's <c>body.cancel</c> spy.</summary>
internal sealed class TrackingContent : HttpContent
{
    internal int Disposed { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposed++;
        }

        base.Dispose(disposing);
    }
}
