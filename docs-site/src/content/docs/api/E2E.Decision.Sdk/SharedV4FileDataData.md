---
title: "SharedV4FileDataData"
description: "File data holding raw bytes (upstream's Uint8Array) or a base64-encoded string."
editUrl: false
prev: false
next: false
sidebar:
  label: "SharedV4FileDataData"
  hidden: true
---

<a id="E2E_Decision_Sdk_SharedV4FileDataData"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record SharedV4FileDataData : SharedV4FileData, IEquatable<SharedV4FileData>, IEquatable<SharedV4FileDataData>
```


File data holding raw bytes (upstream's `Uint8Array`) or a base64-encoded string.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

**Implements:** [IEquatable&lt;SharedV4FileData&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;SharedV4FileDataData&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/SharedV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_SharedV4FileDataData__ctor_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataData__ctor_System_Byte___"></a>

### SharedV4FileDataData(byte[])


```csharp
public SharedV4FileDataData(byte[] data)
```


Raw bytes.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) |  |

<a id="E2E_Decision_Sdk_SharedV4FileDataData__ctor_System_String_"></a>

### SharedV4FileDataData(string)


```csharp
public SharedV4FileDataData(string base64)
```


A base64-encoded string.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `base64` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_SharedV4FileDataData_Base64_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataData_Base64"></a>

### Base64


```csharp
public string? Base64 { get; }
```


The base64 string, when the data is a string.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4FileDataData_Bytes_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataData_Bytes"></a>

### Bytes


```csharp
public byte[]? Bytes { get; }
```


The raw bytes, when the data is bytes.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)
