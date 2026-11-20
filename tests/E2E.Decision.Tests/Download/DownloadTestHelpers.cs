// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.Download;

/// <summary>The fetch stub and response builders the download tests share. Stands in for upstream's <c>vi.fn()</c> on <c>globalThis.fetch</c>.</summary>
internal static class DownloadTestHelpers
{
    internal static HttpResponseMessage Ok(byte[] body, string? mediaType = null, HttpContent? content = null)
    {
        content ??= new ByteArrayContent(body);
        if (mediaType is not null)
        {
            content.Headers.ContentType = new(mediaType);
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    /// <summary>The counterpart of upstream's <c>{ type: 'opaqueredirect', status: 0, ok: false }</c> response.</summary>
    internal static HttpResponseMessage OpaqueRedirect()
    {
        var request = new HttpRequestMessage();
        request.Options.Set(
            Sdk.FetchWithValidatedRedirects.ResponseTypeOption,
            Sdk.FetchWithValidatedRedirects.OpaqueRedirectType
        );
        return new HttpResponseMessage(0) { RequestMessage = request };
    }

    internal static HttpResponseMessage Redirect(HttpStatusCode status, string location, HttpContent? content = null)
    {
        var response = new HttpResponseMessage(status) { Content = content ?? new ByteArrayContent([]) };
        response.Headers.Location = new Uri(location);
        return response;
    }

    /// <summary>A response body that records whether the download released it (upstream's <c>onCancel</c>).</summary>
    internal sealed class TrackedContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    internal sealed record FetchCall(string Url, IReadOnlyList<string> HeaderNames, CancellationToken Token);

    /// <summary>Answers each call with the next handler; the last one answers every call after it.</summary>
    internal sealed class RecordingFetch(params Func<HttpResponseMessage>[] handlers)
    {
        private readonly List<FetchCall> _calls = [];

        public IReadOnlyList<FetchCall> Calls => _calls;

        public FetchFunction Fetch =>
            (request, token) =>
            {
                _calls.Add(
                    new FetchCall(request.RequestUri!.ToString(), [.. request.Headers.Select(h => h.Key)], token)
                );
                return Task.FromResult(handlers[Math.Min(_calls.Count, handlers.Length) - 1]());
            };
    }
}
