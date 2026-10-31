---
title: "BrowserDownload"
description: "A download the browser saved as an artifact of the attempt (upstream's waitForDownload result, { path, absolutePath, suggestedFilename })."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserDownload"
  hidden: true
---

<a id="E2E_Engine_BrowserDownload"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserDownload : IEquatable<BrowserDownload>
```


A download the browser saved as an artifact of the attempt (upstream's `waitForDownload` result,
`{ path, absolutePath, suggestedFilename }`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserDownload&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L178)

## Properties

<a id="E2E_Engine_BrowserDownload_AbsolutePath_"></a>
<a id="E2E_Engine_BrowserDownload_AbsolutePath"></a>

### AbsolutePath


```csharp
public required string AbsolutePath { get; init; }
```


Where the file is on this machine.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L184)

<a id="E2E_Engine_BrowserDownload_Path_"></a>
<a id="E2E_Engine_BrowserDownload_Path"></a>

### Path


```csharp
public required string Path { get; init; }
```


Relative to the attempt's artifact directory, with `/` separators: `downloads/001-report.csv`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L181)

<a id="E2E_Engine_BrowserDownload_SuggestedFilename_"></a>
<a id="E2E_Engine_BrowserDownload_SuggestedFilename"></a>

### SuggestedFilename


```csharp
public required string SuggestedFilename { get; init; }
```


The filename the page or the server suggested, as the browser saw it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L187)
