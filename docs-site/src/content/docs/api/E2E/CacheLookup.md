---
title: "CacheLookup"
description: "CacheLookup in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheLookup"
  hidden: true
---

<a id="E2E_CacheLookup"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CacheLookup
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_CacheLookup_Entry_"></a>
<a id="E2E_CacheLookup_Entry"></a>

### Entry


```csharp
public CacheEntry? Entry { get; init; }
```


**Value:** [CacheEntry](/api/E2E/CacheEntry/)

<a id="E2E_CacheLookup_Reason_"></a>
<a id="E2E_CacheLookup_Reason"></a>

### Reason


```csharp
public string? Reason { get; init; }
```


`no-entry` or `invalid-entry` (an unreadable or oversized entry, or a key that is not a SHA-256 digest) when [Entry](#E2E_CacheLookup_Entry) is null.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
