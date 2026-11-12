// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;

namespace E2E.BrowserStack.Tests.Recording;

/// <summary>Ports <c>describe('uploadAppFromUrl()')</c> of <c>packages/browserstack/tests/unit/recording.test.ts</c>.</summary>
public sealed class UploadAppFromUrlTests
{
    private const string Api = "https://api-cloud.browserstack.com/app-automate";

    private static readonly BrowserStackCredentials Credentials = new("ada", "bs-key");

    /// <summary>Answers every request with what the test says, remembering what was asked.</summary>
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(Uri Url, HttpMethod Method, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request.RequestUri!, request.Method, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return answer(request);
        }
    }

    private static Task<string> Upload(Handler handler, string url) =>
        BrowserStackSessions.UploadAppFromUrlAsync(new HttpClient(handler), Api, Credentials, url, CancellationToken.None);

    [Fact]
    public async Task Posts_the_URL_as_a_form_to_the_upload_endpoint_and_returns_the_bs_id()
    {
        var handler = new Handler(_ => FakeAppAutomate.Json(new JsonObject { ["app_url"] = "bs://NEW" }));

        var id = await Upload(handler, "https://example.com/app.apk");

        Assert.Equal("bs://NEW", id);
        var seen = Assert.Single(handler.Seen);
        Assert.Equal(($"{Api}/upload", HttpMethod.Post), (seen.Url.AbsoluteUri, seen.Method));
        Assert.Contains("name=url", seen.Body, StringComparison.Ordinal);
        Assert.Contains("https://example.com/app.apk", seen.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_rejected_upload_with_its_message_and_an_answer_without_a_bs_id()
    {
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Upload(new Handler(_ => FakeAppAutomate.Json(new JsonObject { ["error"] = "Invalid file format" }, HttpStatusCode.UnprocessableEntity)), "https://example.com/x"));
        Assert.Equal("BrowserStack app upload from a URL failed: HTTP 422 (Invalid file format)", rejected.Message);

        var noId = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Upload(new Handler(_ => FakeAppAutomate.Json(new JsonObject { ["app_url"] = "https://nope" })), "https://example.com/x"));
        Assert.Equal("BrowserStack app upload from a URL failed: no bs:// app id in the response", noId.Message);
    }

    [Fact]
    public async Task Never_repeats_a_signed_app_URL_the_API_echoes_in_an_error()
    {
        const string signed = "https://bucket.example.com/app.apk?X-Amz-Signature=secret-signature&X-Amz-Expires=600";
        var echoed = $"Could not download {signed} ({Uri.EscapeDataString(signed)})";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Upload(new Handler(_ => FakeAppAutomate.Json(new JsonObject { ["error"] = echoed }, HttpStatusCode.UnprocessableEntity)), signed));

        Assert.Equal("BrowserStack app upload from a URL failed: HTTP 422 (Could not download <app URL> (<app URL>))", error.Message);
        Assert.DoesNotContain("secret-signature", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_signed_app_URL_out_of_a_transport_error_and_its_cause()
    {
        const string signed = "https://bucket.example.com/app.apk?X-Amz-Signature=secret-signature";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Upload(new Handler(_ => throw new HttpRequestException($"fetch failed for {signed}")), signed));

        Assert.Equal("BrowserStack app upload from a URL failed: fetch failed for <app URL>", error.Message);
        Assert.Equal("fetch failed for <app URL>", error.InnerException!.Message);
        Assert.DoesNotContain("secret-signature", error.ToString(), StringComparison.Ordinal);
    }
}
