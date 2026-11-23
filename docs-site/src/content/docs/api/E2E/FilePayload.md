---
title: "FilePayload"
description: "An in-memory file to attach without writing it to disk (upstream's FilePayload)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FilePayload"
  hidden: true
---

<a id="E2E_FilePayload"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record FilePayload : IEquatable<FilePayload>
```


An in-memory file to attach without writing it to disk (upstream's `FilePayload`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;FilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/FilePayload.cs)

## Constructors

<a id="E2E_FilePayload__ctor_"></a>
<a id="E2E_FilePayload__ctor_System_String_System_String_System_Byte___"></a>

### FilePayload(string, string, byte[])


```csharp
public FilePayload(string Name, string MimeType, byte[] Buffer)
```


An in-memory file to attach without writing it to disk (upstream's `FilePayload`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The file name the page sees. Not blank. |
| `MimeType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The media type the page sees, such as `text/plain`. |
| `Buffer` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) | The file's bytes. May be empty. |

## Properties

<a id="E2E_FilePayload_Buffer_"></a>
<a id="E2E_FilePayload_Buffer"></a>

### Buffer


```csharp
public byte[] Buffer { get; init; }
```


The file's bytes. May be empty.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_FilePayload_MimeType_"></a>
<a id="E2E_FilePayload_MimeType"></a>

### MimeType


```csharp
public string MimeType { get; init; }
```


The media type the page sees, such as `text/plain`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_FilePayload_Name_"></a>
<a id="E2E_FilePayload_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


The file name the page sees. Not blank.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
