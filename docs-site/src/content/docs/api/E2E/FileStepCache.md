---
title: "FileStepCache"
description: "JSON files in a directory, one per cache key. An entry over the byte cap is ignored on read and not written."
editUrl: false
prev: false
next: false
sidebar:
  label: "FileStepCache"
  hidden: true
---

<a id="E2E_FileStepCache"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class FileStepCache : IStepCache
```


JSON files in a directory, one per cache key. An entry over the byte cap is ignored on read and not written.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IStepCache](/api/E2E/IStepCache/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Constructors

<a id="E2E_FileStepCache__ctor_"></a>
<a id="E2E_FileStepCache__ctor_System_String_"></a>

### FileStepCache(string)


```csharp
public FileStepCache(string directory)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `directory` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_FileStepCache__ctor_System_String_System_Int32_System_Boolean_"></a>

### FileStepCache(string, int, bool)


```csharp
public FileStepCache(string directory, int maxBytes, bool writable)
```


Upstream's `FileCacheStoreOptions` as arguments: &lt;code class="paramref"&gt;maxBytes&lt;/code&gt; is the entry size cap for tests (production passes
the wire ceiling, which clamps it); a store that is not &lt;code class="paramref"&gt;writable&lt;/code&gt; never writes and never deletes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `directory` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `maxBytes` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `writable` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Fields

<a id="E2E_FileStepCache_MaxCacheWireBytes"></a>

### MaxCacheWireBytes


```csharp
public const int MaxCacheWireBytes = 1048576
```


Hard wire ceiling on one entry, on both the read and the write path (upstream's `MAX_CACHE_WIRE_BYTES`).

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_FileStepCache_SchemaVersion"></a>

### SchemaVersion


```csharp
public const int SchemaVersion = 1
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Properties

<a id="E2E_FileStepCache_Writable_"></a>
<a id="E2E_FileStepCache_Writable"></a>

### Writable


```csharp
public bool Writable { get; }
```


False when the store never writes or deletes (upstream's `CacheStore.writable`; [IStepCache](/api/E2E/IStepCache/) has no such member, the mode decides).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_FileStepCache_Delete_"></a>
<a id="E2E_FileStepCache_Delete_System_String_"></a>

### Delete(string)


```csharp
public void Delete(string key)
```


Returns when the entry is absent; throws when deletion fails, so the best-effort caller can avoid reporting a successful eviction.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_FileStepCache_Read_"></a>
<a id="E2E_FileStepCache_Read_System_String_"></a>

### Read(string)


```csharp
public CacheLookup Read(string key)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [CacheLookup](/api/E2E/CacheLookup/)

<a id="E2E_FileStepCache_Write_"></a>
<a id="E2E_FileStepCache_Write_System_String_E2E_CacheEntry_"></a>

### Write(string, CacheEntry)


```csharp
public int? Write(string key, CacheEntry entry)
```


Persists one entry, returning its size in bytes, or null when not written: the store is not writable, or the entry is over the byte cap.
(.NET-only shape: `Write` returns nothing, so this overload is public beside it; upstream's `write` returns `{ bytes } | undefined`.)

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `entry` | [CacheEntry](/api/E2E/CacheEntry/) |  |

**Returns:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
