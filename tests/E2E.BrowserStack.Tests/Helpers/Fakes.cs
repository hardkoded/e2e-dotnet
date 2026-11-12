// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Mobile;
using E2E.Mobile.Lease;

namespace E2E.BrowserStack.Tests;

/// <summary>The lease helper, stubbed: it records what the provider asks it to lease and answers a lease that carries the selectors on its client.</summary>
internal sealed class FakeLeases : IAgentDeviceLeases
{
    private int _leases;

    public List<(DeviceRequest Request, AgentDeviceLeaseRequest Lease)> Acquired { get; } = [];

    public List<DeviceLease> Released { get; } = [];

    public Task<DeviceLease> AcquireAsync(DeviceRequest request, AgentDeviceLeaseRequest lease, CancellationToken cancellationToken)
    {
        Acquired.Add((request, lease));
        return Task.FromResult(new DeviceLease { Id = $"lease-{++_leases}", Client = lease.Selectors!.ToJson() });
    }

    public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
    {
        Released.Add(lease);
        return Task.CompletedTask;
    }

    public LeaseSelectors Selectors(int call = 0) => Acquired[call].Lease.Selectors!;
}

/// <summary>A stand-in for BrowserStack's App Automate API behind an <see cref="HttpClient"/>: build and session lists, and app uploads.</summary>
internal sealed class FakeAppAutomate : HttpMessageHandler
{
    public sealed record Call(Uri Url, string Method, string? Authorization, string Body);

    public List<Call> Calls { get; } = [];

    /// <summary>Every build, newest first, as the list returns them.</summary>
    public List<JsonObject> Builds { get; set; } = [];

    /// <summary>Each build's sessions by <c>hashed_id</c>, newest first.</summary>
    public Dictionary<string, List<JsonObject>> Sessions { get; set; } = [];

    /// <summary>Responses the next requests get instead of the fake's own, in order.</summary>
    public Queue<Func<HttpResponseMessage>> Overrides { get; } = [];

    /// <summary>The <c>app_url</c> the next uploads answer, in order; then <c>bs://UPLOADED</c>.</summary>
    public Queue<string> UploadIds { get; } = [];

    /// <summary>Runs on each upload before it answers.</summary>
    public Func<Task>? OnUpload { get; set; }

    public HttpClient Client() => new(this) { Timeout = Timeout.InfiniteTimeSpan };

    public IEnumerable<Call> Uploads => Calls.Where(call => call.Url.AbsolutePath.EndsWith("/upload", StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Calls)
        {
            Calls.Add(new Call(request.RequestUri!, request.Method.Method, request.Headers.Authorization?.ToString(), body));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Overrides.TryDequeue(out var over))
        {
            return over();
        }

        var url = request.RequestUri!;
        if (request.Method == HttpMethod.Post && url.AbsolutePath.EndsWith("/upload", StringComparison.Ordinal))
        {
            if (OnUpload is { } onUpload)
            {
                await onUpload();
            }

            return Json(new JsonObject { ["app_url"] = UploadIds.TryDequeue(out var id) ? id : "bs://UPLOADED" });
        }

        if (url.AbsolutePath.EndsWith("/builds.json", StringComparison.Ordinal))
        {
            return Json(Page(Builds, url));
        }

        var build = System.Text.RegularExpressions.Regex.Match(url.AbsolutePath, "/builds/([^/]+)/sessions\\.json$").Groups[1];
        if (build.Success)
        {
            return Json(Page(Sessions.GetValueOrDefault(build.Value) ?? [], url));
        }

        return Json(new JsonObject { ["message"] = "not found" }, HttpStatusCode.NotFound);
    }

    public static HttpResponseMessage Json(JsonNode body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static JsonArray Page(List<JsonObject> rows, Uri url)
    {
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        var limit = int.Parse(query["limit"]!, System.Globalization.CultureInfo.InvariantCulture);
        var offset = int.Parse(query["offset"]!, System.Globalization.CultureInfo.InvariantCulture);
        return new JsonArray([.. rows.Skip(offset).Take(limit).Select(row => (JsonNode?)row.DeepClone())]);
    }
}
