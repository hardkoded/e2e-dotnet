---
title: "BrowserDownloadContext"
description: "Handed to ReadAsync, once per file."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserDownloadContext"
  hidden: true
---

<a id="E2E_Engine_BrowserDownloadContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserDownloadContext : IEquatable<BrowserDownloadContext>
```


Handed to [ReadAsync](/api/E2E.Engine/BrowserProviderDownloads/#E2E_Engine_BrowserProviderDownloads_ReadAsync), once per file.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserDownloadContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L105)

## Properties

<a id="E2E_Engine_BrowserDownloadContext_Env_"></a>
<a id="E2E_Engine_BrowserDownloadContext_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment, the same acquire saw.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L112)

<a id="E2E_Engine_BrowserDownloadContext_RunId_"></a>
<a id="E2E_Engine_BrowserDownloadContext_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L107)

<a id="E2E_Engine_BrowserDownloadContext_TargetName_"></a>
<a id="E2E_Engine_BrowserDownloadContext_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L109)
