---
title: "DownloadedFile"
description: "A file a DownloadFunction downloaded."
editUrl: false
prev: false
next: false
sidebar:
  label: "DownloadedFile"
  hidden: true
---

<a id="E2E_Decision_Sdk_DownloadedFile"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DownloadedFile : IEquatable<DownloadedFile>
```


A file a [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/) downloaded.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DownloadedFile&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DownloadFunction.cs)

## Constructors

<a id="E2E_Decision_Sdk_DownloadedFile__ctor_"></a>
<a id="E2E_Decision_Sdk_DownloadedFile__ctor_System_Byte___System_String_"></a>

### DownloadedFile(byte[], string?)


```csharp
public DownloadedFile(byte[] Data, string? MediaType)
```


A file a [DownloadFunction](/api/E2E.Decision.Sdk/DownloadFunction/) downloaded.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The bytes of the file. |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The media type of the file, when the server named one. |

## Properties

<a id="E2E_Decision_Sdk_DownloadedFile_Data_"></a>
<a id="E2E_Decision_Sdk_DownloadedFile_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


The bytes of the file.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_Decision_Sdk_DownloadedFile_MediaType_"></a>
<a id="E2E_Decision_Sdk_DownloadedFile_MediaType"></a>

### MediaType


```csharp
public string? MediaType { get; init; }
```


The media type of the file, when the server named one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
