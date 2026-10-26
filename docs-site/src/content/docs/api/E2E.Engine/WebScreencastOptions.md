---
title: "WebScreencastOptions"
description: "How the engine's own screencast of the page records an attempt that records video: the frame size and the JPEG quality of the frames it encodes. Which attemp..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebScreencastOptions"
  hidden: true
---

<a id="E2E_Engine_WebScreencastOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record WebScreencastOptions : IEquatable<WebScreencastOptions>
```


How the engine's own screencast of the page records an attempt that records video: the frame size and the JPEG quality
of the frames it encodes. Which attempts record is the config's `video`, not an engine option. Upstream's
`WebScreencastOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WebScreencastOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L26)

## Properties

<a id="E2E_Engine_WebScreencastOptions_Quality_"></a>
<a id="E2E_Engine_WebScreencastOptions_Quality"></a>

### Quality


```csharp
public int? Quality { get; init; }
```


The JPEG quality of the frames, 0 through 100. Defaults to Playwright's.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L32)

<a id="E2E_Engine_WebScreencastOptions_Size_"></a>
<a id="E2E_Engine_WebScreencastOptions_Size"></a>

### Size


```csharp
public WebViewport? Size { get; init; }
```


The frame size in whole pixels. Defaults to the page's viewport size when the segment starts.

**Value:** [WebViewport](/api/E2E.Engine/WebViewport/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L29)
