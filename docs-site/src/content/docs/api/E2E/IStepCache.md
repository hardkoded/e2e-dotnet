---
title: "IStepCache"
description: "Where the replay cache keeps its entries: the file store by default, or a host's own through cache.store (upstream's CacheStore, cache/store.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "IStepCache"
  hidden: true
---

<a id="E2E_IStepCache"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IStepCache
```


Where the replay cache keeps its entries: the file store by default, or a host's own through `cache.store` (upstream's `CacheStore`,
`cache/store.ts`).

**Derived:** [FileStepCache](/api/E2E/FileStepCache/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_IStepCache_Writable_"></a>
<a id="E2E_IStepCache_Writable"></a>

### Writable


```csharp
bool Writable { get; }
```


Upstream's `CacheStore.writable`: false when the store never writes or deletes. Its digest in the config is this flag.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_IStepCache_Delete_"></a>
<a id="E2E_IStepCache_Delete_System_String_"></a>

### Delete(string)


```csharp
void Delete(string key)
```


Returns when the entry is absent; throws when deletion fails, so the best-effort caller can avoid reporting a successful eviction.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_IStepCache_Read_"></a>
<a id="E2E_IStepCache_Read_System_String_"></a>

### Read(string)


```csharp
CacheLookup Read(string key)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [CacheLookup](/api/E2E/CacheLookup/)

<a id="E2E_IStepCache_Write_"></a>
<a id="E2E_IStepCache_Write_System_String_E2E_CacheEntry_"></a>

### Write(string, CacheEntry)


```csharp
int? Write(string key, CacheEntry entry)
```


Persists one trace, returning its size in bytes, or null when not written (upstream's `write` returns `{ bytes } | undefined`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `entry` | [CacheEntry](/api/E2E/CacheEntry/) |  |

**Returns:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
