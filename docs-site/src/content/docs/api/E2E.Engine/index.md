---
title: "E2E.Engine"
description: "Public types in the `E2E.Engine` namespace."
editUrl: false
sidebar:
  label: "E2E.Engine"
---

[API reference](/api/)

Public types in the `E2E.Engine` namespace.

## Classes

| Type | Summary |
| --- | --- |
| [AppLogEntry](/api/E2E.Engine/AppLogEntry/) | One line of what the app did on its own, as an engine reports it to AppLog (upstream's AppLogEntry): console output, an exception nothing caught, a failed request. The harness redacts and bounds it. |
| [BoundingBox](/api/E2E.Engine/BoundingBox/) | A viewport-relative rectangle in CSS pixels. |
| [BrowserCookie](/api/E2E.Engine/BrowserCookie/) | A browser cookie. When setting one, give either Url, or Domain with an optional Path, never both. |
| [BrowserDownloadContext](/api/E2E.Engine/BrowserDownloadContext/) | Handed to ReadAsync, once per file. |
| [BrowserLease](/api/E2E.Engine/BrowserLease/) | One leased browser, as CancellationToken) returns it and CancellationToken) gets it back. A provider may derive from it and keep further fields: release gets that same object, while only the declared fields travel from the runner to the worker that attaches to it. |
| [BrowserProviderDownloads](/api/E2E.Engine/BrowserProviderDownloads/) | How a download reaches the runner from a browser on another machine. The browser saves every file to its own disk, where the runner cannot open it, so the engine points the browser's downloads at Dir there and reads each finished file back through ReadAsync. |
| [BrowserReleaseContext](/api/E2E.Engine/BrowserReleaseContext/) | Handed to release, once per lease. |
| [BrowserRequest](/api/E2E.Engine/BrowserRequest/) | What the engine asks a provider for: one browser for one worker slot, or for one attempt, of a run. |
| [BrowserResponse](/api/E2E.Engine/BrowserResponse/) | A response an engine observed, with its body still being read. |
| [DocumentElement](/api/E2E.Engine/DocumentElement/) | A mutable control on a DocumentPage. |
| [DocumentEngine](/api/E2E.Engine/DocumentEngine/) | In-memory page engine for samples and tests that do not need a browser. Each open builds a fresh page from the route mapped on DocumentWorld. Actions mutate that page until the next open. app.back rebuilds the previous route, and app.restart and app.clearState drop the history and reopen the base URL. The document engine has no browser, so the Browser fixture is unsupported. |
| [DocumentPage](/api/E2E.Engine/DocumentPage/) | One in-memory screen. Builder methods append controls in reading order. |
| [DocumentWorld](/api/E2E.Engine/DocumentWorld/) | Routes a DocumentEngine can open. Each open builds a new page from the mapped factory. |
| [EngineFinishInfo](/api/E2E.Engine/EngineFinishInfo/) | The facts handed to an engine's finish (CancellationToken)), once per run and target after the last worker is gone, with the cleanup budget of one hook. Upstream's EngineFinishInfo. |
| [EngineInitInfo](/api/E2E.Engine/EngineInitInfo/) | What a worker's init is given, once per worker, before its first attempt. Upstream's EngineInitInfo. |
| [EnginePrepareInfo](/api/E2E.Engine/EnginePrepareInfo/) | The facts handed to an engine's prepare (CancellationToken)), once per run and target in the runner process, before any worker exists. Upstream's EnginePrepareInfo. |
| [EnginePrepareResult](/api/E2E.Engine/EnginePrepareResult/) | What prepare learned that the run must honour. Upstream's EnginePrepareResult. |
| [EngineScreenshot](/api/E2E.Engine/EngineScreenshot/) | A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes. |
| [EngineStartOptions](/api/E2E.Engine/EngineStartOptions/) | Passed to CancellationToken) once per test attempt. |
| [KeyModifiers](/api/E2E.Engine/KeyModifiers/) | The names of KeyModifier, which a recorded action and an MCP argument spell. Upstream's KEYMODIFIERS and isKeyModifier. |
| [LocatorAction](/api/E2E.Engine/LocatorAction/) | A deterministic action CancellationToken) carries out on one node. |
| [LocatorAction.Check](/api/E2E.Engine/LocatorAction.Check/) |  |
| [LocatorAction.Clear](/api/E2E.Engine/LocatorAction.Clear/) |  |
| [LocatorAction.DoubleTap](/api/E2E.Engine/LocatorAction.DoubleTap/) | A double tap. <code class="paramref">Modifiers</code> are held for both clicks, as for LocatorAction.Tap. |
| [LocatorAction.Fill](/api/E2E.Engine/LocatorAction.Fill/) |  |
| [LocatorAction.Focus](/api/E2E.Engine/LocatorAction.Focus/) |  |
| [LocatorAction.Press](/api/E2E.Engine/LocatorAction.Press/) |  |
| [LocatorAction.PressSequentially](/api/E2E.Engine/LocatorAction.PressSequentially/) | Focuses the node and types <code class="paramref">Text</code> one character at a time. |
| [LocatorAction.ScrollIntoView](/api/E2E.Engine/LocatorAction.ScrollIntoView/) | Scrolls the node's nearest scrollable container until the node is in the viewport. |
| [LocatorAction.Select](/api/E2E.Engine/LocatorAction.Select/) |  |
| [LocatorAction.Swipe](/api/E2E.Engine/LocatorAction.Swipe/) | Scrolls a scrollable node about three quarters of its height or width. The viewport swipe is CancellationToken). |
| [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/) | A tap. <code class="paramref">Modifiers</code> are held for the click, and only an engine that declares TapModifiers receives them. |
| [LocatorAction.Uncheck](/api/E2E.Engine/LocatorAction.Uncheck/) |  |
| [NodeStates](/api/E2E.Engine/NodeStates/) | Boolean states a platform can report for one node. |
| [Observation](/api/E2E.Engine/Observation/) | A redacted semantic snapshot of the current screen. |
| [ObservationLimits](/api/E2E.Engine/ObservationLimits/) | Limits from the engine contract. Engines cut names and text before they reach the model. |
| [ProviderRecordContext](/api/E2E.Engine/ProviderRecordContext/) | Handed to a provider's RecordAsync, once per attempt that records video. |
| [ProviderRecording](/api/E2E.Engine/ProviderRecording/) | A recording a provider's service makes of the browser or device it leased, as RecordAsync started it. |
| [ProviderRecordingResult](/api/E2E.Engine/ProviderRecordingResult/) | What a stopped recording became: a file the provider wrote into the directory it was given, by name (File), or a recording its service keeps, by http(s) URL and media type (Url and MediaType). |
| [ProviderRecordings](/api/E2E.Engine/ProviderRecordings/) | The recording seam every engine's provider shares (upstream recording.ts): a hosted browser or device service recording the attempt itself, in place of the engine's own capture. A provider's RecordAsync starts a ProviderRecording for the lease the attempt rides; CancellationToken) ends it and checks what the provider handed back, so every engine turns a recording into a VideoSegment the same way. |
| [ProviderRecordingStopContext](/api/E2E.Engine/ProviderRecordingStopContext/) | Handed to CancellationToken), once per recording. |
| [ProviderRecordingTarget](/api/E2E.Engine/ProviderRecordingTarget/) | Where the attempt's recording lands and who made it, for CancellationToken). |
| [RouteContinueOverrides](/api/E2E.Engine/RouteContinueOverrides/) | What ContinueAsync(RouteContinueOverrides) changes about the request before it goes to the network. |
| [RouteFulfillResponse](/api/E2E.Engine/RouteFulfillResponse/) | How FulfillAsync(RouteFulfillResponse) answers a request. Give at most one of Json, Body, or Path; none is an empty body. |
| [SemanticNode](/api/E2E.Engine/SemanticNode/) | One node of an observation tree. Ref is valid only for the observation it came from. |
| [VideoFile](/api/E2E.Engine/VideoFile/) | A video file in the attempt artifact directory. |
| [VideoLink](/api/E2E.Engine/VideoLink/) | A recording a hosted service keeps, by URL: the report links to it and the runner never downloads it. |
| [VideoSegment](/api/E2E.Engine/VideoSegment/) | One recorded video of an attempt: a file the engine wrote into the attempt artifact directory (VideoFile), or a recording a hosted service keeps and links to (VideoLink). |
| [WebBasicAuth](/api/E2E.Engine/WebBasicAuth/) | HTTP basic authentication the browser answers a 401 challenge with. |
| [WebConnectOptions](/api/E2E.Engine/WebConnectOptions/) | Attach to a remote Chromium over CDP instead of launching one. Supplying ReconnectEndpoint opts into a persistent context instead. |
| [WebEngine](/api/E2E.Engine/WebEngine/) | Browser engine for the web target. It drives Chromium through Playwright, reads a semantic tree (role, name, text, test id, state), and performs the same node actions the document engine does. Install browsers once with playwright.ps1 install chromium from this project's build output. |
| [WebEngineOptions](/api/E2E.Engine/WebEngineOptions/) | Options of the browser engine: how it drives the app. |
| [WebInitScript](/api/E2E.Engine/WebInitScript/) | A script every document runs before the page's own, in every tab and frame: JavaScript source, a file of it, or a function source the page calls. A string converts to source. |
| [WebMedia](/api/E2E.Engine/WebMedia/) | User preferences a page reads through @media rules and matchMedia, as Playwright's emulateMedia sets them. A feature left <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> keeps the browser's own value, or the one an earlier call set. |
| [WebRouteRequest](/api/E2E.Engine/WebRouteRequest/) | The request a route intercepted. |
| [WebScreencastOptions](/api/E2E.Engine/WebScreencastOptions/) | How the engine's own screencast of the page records an attempt that records video: the frame size and the JPEG quality of the frames it encodes. Which attempts record is the config's video, not an engine option. Upstream's WebScreencastOptions. |
| [WebViewport](/api/E2E.Engine/WebViewport/) | A page size in CSS pixels. |

## Interfaces

| Type | Summary |
| --- | --- |
| [IBrowserProvider](/api/E2E.Engine/IBrowserProvider/) | A source of hosted browsers: the seam a hosted browser service plugs into (upstream BrowserProvider). In Worker scope CancellationToken) is called once per worker slot, for every slot at once, in the runner; a worker whose browser drops calls it again, from its own process, for a replacement. In Attempt scope every attempt start calls it from the worker. CancellationToken) is called exactly once per lease, by the process that acquired it, also after an acquire of another slot failed; a worker that dies first never calls it, and CancellationToken) releases what it left. A provider must not rely on runner-process state to serve a request from a worker. Nothing here knows any vendor. |
| [IBrowserRoute](/api/E2E.Engine/IBrowserRoute/) | One intercepted request, as an engine hands it to a route handler. The harness has already validated each decision and takes at most one. |
| [IBrowserSession](/api/E2E.Engine/IBrowserSession/) | The browser-shaped surface an engine session can add on top of IEngineSession. The Browser fixture uses it, and fails with UNSUPPORTEDCAPABILITY when the session does not implement it. The harness has already validated every argument. |
| [IEngine](/api/E2E.Engine/IEngine/) | The body of one target. Core knows this contract and never an engine's internals. Version is the engine version. It is not part of the replay cache key: a recording re-finds its nodes at replay. |
| [IEngineSession](/api/E2E.Engine/IEngineSession/) | One attempt's session with the app. Disposed at the end of the attempt, after afterEach. |
| [IPreparableEngine](/api/E2E.Engine/IPreparableEngine/) | An engine with upstream's optional prepare and finish hooks (declared in engine/index.ts): what a run provisions once per target before any worker starts, such as a hosted device, and gives back after the last worker ended, so the provisioning happens once per run instead of per worker. The port has no test runner that calls them: a host that starts the engine on its own, as e2e explore and e2e mcp do, calls them around the one attempt (upstream's openStandaloneAttempt), prepare when the session opens and finish when it closes, on every path; the mobile adapters' MobileAppTest runs them once per test process. .NET-only name: upstream tests the engine handle for the members. WebEngine does not implement it, because it leases for the attempt it starts. |

## Enums

| Type | Summary |
| --- | --- |
| [BrowserProtocol](/api/E2E.Engine/BrowserProtocol/) | The protocol a leased browser's endpoint speaks. .NET-only: upstream's lease always carries a CDP endpoint. |
| [BrowserProviderScope](/api/E2E.Engine/BrowserProviderScope/) | How long a lease lives. Worker (the default) is one browser per worker slot, acquired in prepare and released in finish. Attempt is a fresh browser per attempt, acquired when the attempt starts and released when it ends, reconnected through the lease's ReconnectEndpoint after a transport drop. |
| [EngineCapabilities](/api/E2E.Engine/EngineCapabilities/) | Capabilities the harness reads off an engine. An engine never calls a model. |
| [KeyModifier](/api/E2E.Engine/KeyModifier/) | A key held for a click, as a Shift-click extends a selection. Upstream's KeyModifier (engine/contract.ts). ControlOrMeta is Control, or Meta on macOS. |
| [Momentum](/api/E2E.Engine/Momentum/) | Fling strength of a swipe: how far the gesture travels. Upstream's Momentum (none, slow, fast). |
| [ScrollDirection](/api/E2E.Engine/ScrollDirection/) | Which way a scroll moves the content into view. Down reveals what is below. |
| [WebColorScheme](/api/E2E.Engine/WebColorScheme/) | The prefers-color-scheme a page reports. |
| [WebReducedMotion](/api/E2E.Engine/WebReducedMotion/) | The prefers-reduced-motion a page reports. |
