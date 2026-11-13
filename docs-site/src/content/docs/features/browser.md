---
title: Working with the browser
description: Recover a CDP connection and lease a hosted browser from your own provider.
sidebar:
  label: Working with the browser
---

## Reach into an iframe

`Browser.FrameLocator(css)` scopes queries to a frame, and a plain `Screen` query never enters one:

```csharp
var payment = Browser.FrameLocator("#payment-frame");
await payment.GetByLabel("Card number").FillAsync("4242424242424242");
```

Inside a frame, `Locator(css)` takes a CSS or XPath selector, and `FrameLocator(css)` enters a nested frame.

The agent's observation shows a frame from another site, such as a hosted card field, as an `iframe` node with none of its content. A frame is from another site when its document is not on the app's site (the registrable domain of the app's URL). Frames inside the app's own site, and `about:blank` and `srcdoc` documents, are read as part of the page. The agent reaches an off-site frame's content through script steps with `Browser.FrameLocator`, or through pixels: judge its content with `Vision = VisionMode.On`. A frame from another site does not count toward the observation's node budget, and its absence is not truncation. An `Agent.ActAsync` whose only change is in that frame records no replay, so it runs live on every run.

## Recovering a CDP transport

`WebConnectOptions.ReconnectEndpoint` opts into recovery of the same remote browser after its CDP transport disconnects. This mode uses the browser's persistent default context, because Chrome deletes ordinary Playwright contexts when their transport detaches.

```csharp
new WebEngine(new WebEngineOptions
{
    Connect = new WebConnectOptions
    {
        CdpEndpoint = ct => hosted.ProvisionAsync(ct),
        ReconnectEndpoint = ct => hosted.EndpointAsync(ct),
    },
});
```

In this mode, `CdpEndpoint` provisions a fresh, dedicated browser at every attempt start, including retries. The engine rejects a browser reused by a previous attempt of the same `WebEngine`. The remote must have only its default context, and the host owns the browser: disposing the session closes the connection, not the browser. The attempt's first page is the browser's own first tab, navigated to `about:blank`.

After a disconnect, the next operation calls `ReconnectEndpoint` once, bounded by the action timeout and its cancellation token. This resolver must return the existing browser's endpoint. The engine verifies the default context id and the active tab's target id; a URL match is not enough to pick a replacement tab. The attempt's other tabs are found by their target ids, and one that is gone is dropped. Recovery also requires the page's frames to keep the closed shadow root hook, which a document that navigated while disconnected lost. A different browser, a missing active tab, or a resolver that runs out of time fails the attempt, and every later operation fails the same way. Recovery never repeats an operation that was already dispatched. Route handlers and init scripts, configured and added, are reattached, and each document still runs every init script once. After a reconnect, `PerformAsync`, the agent's key press, and the viewport swipe fail with `NODE_STALE` until the screen is observed again; the `Browser` keyboard and mouse keep working. An observed node's element handle is made from the observation's in-page array the first time an action reaches for it, and a ref still names the element the walk read, not whatever sits at that position later; an element that is gone, a document replaced since the walk, a newer observation, an app restart, or a state reset makes the ref a retryable `NODE_STALE`.

Either resolver can return a `CdpTarget` (`new CdpTarget { Endpoint = ..., Headers = ... }`) to authenticate the CDP handshake; a string converts to one without headers. These headers are separate from `WebEngineOptions.Headers`, which sends headers to the app. A failed connection that carried handshake headers reports only that the connection failed, never the headers.

Persistent recovery does not support `Headers`, `BasicAuth`, `UserAgent`, `Locale`, or `TimezoneId` (`INVALID_CONFIG`), or `App.ClearStateAsync` (`UNSUPPORTED_CAPABILITY`). `App.RestartAsync` remains available. `ColorScheme` and `ReducedMotion` work in this mode: the engine sets them on each page it opens. Omit `ReconnectEndpoint` to keep a new, isolated context per attempt.

## Hosted browsers

`WebEngineOptions.Browser` takes an `IBrowserProvider`: an object that leases a browser from a service and hands back the endpoint to attach to. The engine attaches over CDP the way `Connect` does, or with Playwright's own protocol when the lease's `Protocol` is `BrowserProtocol.Playwright` (.NET-only; the TestMu grid needs it), opening its own browser context then, and adds the lifecycle `Connect` lacks: it asks for a browser when one is needed and gives it back when its scope ends, on every exit path, so a session billed by the minute stops when the run no longer uses it. The engine knows no vendor; for any service a provider is a few dozen lines in your project. A provider implies Chromium and cannot be combined with `Connect`. Add `Viewport = null` when the service shows the window in a live view, so the page fills the window.

```csharp
public interface IBrowserProvider
{
    string Name { get; }                                       // label in progress lines and error messages
    BrowserProviderScope Scope => BrowserProviderScope.Worker; // Worker: a browser per worker slot. Attempt: a fresh browser per attempt.
    Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken);
    Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SweepAsync(BrowserReleaseContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]); // optional: a worker that dies never releases what it acquired
    BrowserProviderDownloads? Downloads => null;    // optional: serves downloads of a browser on another machine
    Task<IReadOnlyDictionary<string, string>?> CdpHeadersAsync(BrowserLease lease, BrowserRequest context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>?>(null); // optional: CDP handshake headers, resolved in the worker
    Task<ProviderRecording?> RecordAsync(BrowserLease lease, ProviderRecordContext context, CancellationToken cancellationToken) =>
        Task.FromResult<ProviderRecording?>(null);  // optional: null lets the engine record the page
}

public class BrowserLease
{
    public string Id { get; init; }                 // the provider's handle (a session id), handed back to ReleaseAsync
    public string CdpEndpoint { get; init; }        // ws://, wss://, or http:// DevTools endpoint to attach to
    public BrowserProtocol Protocol { get; init; }  // what CdpEndpoint speaks; Playwright needs Worker scope and no reconnect
    public string? ReconnectEndpoint { get; init; } // to reattach to the same browser after a transport drop; per-attempt leases only
}
```

`AcquireAsync` receives the run id, target, worker slot and slot count, the attempt id for a per-attempt lease, the run's environment, and a `Log` that reaches the reporter (an engine started without a runner has no reporter and drops those lines). Read tokens from `request.Env`, not from the process environment. A lease is data: only `Id`, `CdpEndpoint`, `ReconnectEndpoint`, and `Protocol` travel to a worker, and `ReleaseAsync` gets back the object `AcquireAsync` returned, so a provider can derive from `BrowserLease` and keep its own fields on it. Every lease is released exactly once, by the side that acquired it.

For an authenticated CDP connection, implement `CdpHeadersAsync(lease, request, ct)` and read credentials from `request.Env` in the worker. Headers never travel in lease JSON and do not become page request headers.

`BrowserProviderScope.Worker`, the default, leases one browser per worker slot when the run starts, all slots at once, and each worker attaches to its own for every attempt it runs. A runner calls `WebEngine.PrepareAsync` once per target before any worker starts, `Init` in each worker, and `FinishAsync` after the last worker ended. Every lease granted is held, so a slot that fails to lease ends the run before any test and the leases that succeeded are still released. When a worker's browser drops, the worker leases a replacement itself at its next attempt and releases it when the worker is disposed (`DisposeAsync`); the dead lease stays with the runner, which releases it in `FinishAsync`. A worker that dies cannot release what it acquired. Implement `SweepAsync` to have `FinishAsync` release those too: it runs once per target after the leases the runner holds were released, finds every lease tagged with the run id and the target at the service, releases it, and returns the ids, which are logged. A sweep that fails is logged and fails nothing.

`BrowserProviderScope.Attempt` leases a fresh browser at every attempt start, retries included, and releases it when the session is disposed, also after a start that failed. A lease that arrives once the attempt was cancelled or ended is given straight back. A `Playwright` lease is refused in this scope, because its connection cannot be reattached. The attempt rides the browser's persistent default context, so the remote must contain only that context, and the limits of [`Connect.ReconnectEndpoint`](#recovering-a-cdp-transport) apply: no `Headers`, `BasicAuth`, `UserAgent`, `Locale`, or `TimezoneId` (`INVALID_CONFIG`), and no `App.ClearStateAsync` (`UNSUPPORTED_CAPABILITY`). After a transport drop the next operation reattaches through the lease's `ReconnectEndpoint`, or `CdpEndpoint` when it has none, and the engine checks that it is the same browser.

`Downloads` and `RecordAsync` are the two hooks for a service that holds the browser's files and screen. `Downloads` names a directory `Dir` on the browser's machine (an absolute POSIX or Windows path) and a `ReadAsync(lease, file, context, ct)` that returns the bytes of a finished file under it, so a download on a browser the runner cannot reach can be read back. `RecordAsync` starts the service's own recording of the leased browser and returns a `ProviderRecording`: its `StartedAt`, and `StopAsync`, which either writes a file into the directory it is given and returns `new ProviderRecordingResult { File = "replay.mp4" }`, or returns `{ Url, MediaType }` for a recording the service keeps. `ProviderRecordings.StopProviderRecordingAsync` ends a recording and checks it into a `VideoFile` (`video/<file>`) or a `VideoLink`, refusing a file that was not written, a non-`http(s)` URL, and a link without a media type. A provider that cannot record a browser returns null, and the engine records the screencast of the page instead. An attempt that records video calls `RecordAsync` once its page is open and stops the recording when the attempt ends. `Browser.WaitForDownloadAsync` calls `Downloads`: the browser saves each download under `Dir`, and the engine reads the finished file back through `ReadAsync` into the attempt's `downloads` folder. Without `Downloads`, a download on a browser on another machine fails with `ENGINE_FAILURE`, naming the provider.

The test base classes need no runner. A `WebEngine` whose `Init` never ran leases for the one attempt it starts, reading the process environment, and gives the lease back when the session ends:

```csharp
protected override IEngine CreateEngine() => new WebEngine(new WebEngineOptions
{
    Browser = new HostedBrowsers("https://browsers.example.com"),
    Viewport = null,
});
```

A provider against a typical session API has three requests: create a session, poll it until it exposes a DevTools endpoint, stop it.

```csharp
public sealed class HostedBrowsers(string serviceUrl) : IBrowserProvider
{
    private static readonly HttpClient Http = new();

    public string Name => "hosted-browsers";

    public async Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        var token = request.Env.GetValueOrDefault("BROWSER_SERVICE_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("BROWSER_SERVICE_TOKEN is not set");
        }

        // 1. Ask for a browser; name the session after the run so it can be found later.
        var name = $"e2e {request.RunId} {request.TargetName} {request.AttemptId ?? $"slot {request.Slot + 1} of {request.Slots}"}";
        using var created = await Http.SendAsync(Authorized(HttpMethod.Post, $"{serviceUrl}/sessions", token, JsonContent.Create(new { name })), cancellationToken);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Session>(cancellationToken))!.Id;

        // 2. Wait until the session exposes its DevTools endpoint. A lease the engine never receives is never released, so stop the session on any failure.
        try
        {
            while (true)
            {
                using var response = await Http.SendAsync(Authorized(HttpMethod.Get, $"{serviceUrl}/sessions/{id}", token), cancellationToken);
                response.EnsureSuccessStatusCode();
                var session = (await response.Content.ReadFromJsonAsync<Session>(cancellationToken))!;
                if (session.Status == "ready" && session.CdpUrl is not null)
                {
                    return new BrowserLease { Id = id, CdpEndpoint = session.CdpUrl };
                }

                if (session.Status != "starting")
                {
                    throw new InvalidOperationException($"browser service: session {id} is {session.Status}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch
        {
            await StopAsync(id, token);
            throw;
        }
    }

    public async Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken)
    {
        // 3. Stop the session; the run waits for this within the cleanup timeout.
        await StopAsync(lease.Id, context.Env.GetValueOrDefault("BROWSER_SERVICE_TOKEN") ?? "", cancellationToken);
    }

    private async Task StopAsync(string id, string token, CancellationToken cancellationToken = default)
    {
        using var response = await Http.SendAsync(Authorized(HttpMethod.Post, $"{serviceUrl}/sessions/{id}/stop", token), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, HttpContent? content = null) =>
        new(method, url) { Content = content, Headers = { Authorization = new("Bearer", token) } };

    private sealed record Session(string Id, string Status, string? CdpUrl);
}
```
