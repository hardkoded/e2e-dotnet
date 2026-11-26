---
title: "WireFilePayload"
description: "One file of WireSetInputFilePayloads: Buffer is the file bytes as base64."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireFilePayload"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireFilePayload"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireFilePayload : IEquatable<WireFilePayload>
```


One file of [WireSetInputFilePayloads](/api/E2E.Engine.Protocol/WireSetInputFilePayloads/): &lt;code class="paramref"&gt;Buffer&lt;/code&gt; is the file bytes as base64.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireFilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireFilePayload__ctor_"></a>
<a id="E2E_Engine_Protocol_WireFilePayload__ctor_System_String_System_String_System_String_"></a>

### WireFilePayload(string, string, string)


```csharp
public WireFilePayload(string Name, string MimeType, string Buffer)
```


One file of [WireSetInputFilePayloads](/api/E2E.Engine.Protocol/WireSetInputFilePayloads/): &lt;code class="paramref"&gt;Buffer&lt;/code&gt; is the file bytes as base64.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `MimeType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Buffer` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_Protocol_WireFilePayload_Buffer_"></a>
<a id="E2E_Engine_Protocol_WireFilePayload_Buffer"></a>

### Buffer


```csharp
public string Buffer { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireFilePayload_MimeType_"></a>
<a id="E2E_Engine_Protocol_WireFilePayload_MimeType"></a>

### MimeType


```csharp
public string MimeType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_WireFilePayload_Name_"></a>
<a id="E2E_Engine_Protocol_WireFilePayload_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
