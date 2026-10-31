---
title: "FileStepCache"
description: "JSON files in a directory, one per cache key. Entries over 1 MiB are ignored."
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


JSON files in a directory, one per cache key. Entries over 1 MiB are ignored.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IStepCache](/api/E2E/IStepCache/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L224)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L233)

## Fields

<a id="E2E_FileStepCache_SchemaVersion"></a>

### SchemaVersion


```csharp
public const int SchemaVersion = 1
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L226)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L283)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L242)

<a id="E2E_FileStepCache_Write_"></a>
<a id="E2E_FileStepCache_Write_System_String_E2E_CacheEntry_"></a>

### Write(string, CacheEntry)


```csharp
public void Write(string key, CacheEntry entry)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `entry` | [CacheEntry](/api/E2E/CacheEntry/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L276)
