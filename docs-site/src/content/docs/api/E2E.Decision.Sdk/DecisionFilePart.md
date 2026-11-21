---
title: "DecisionFilePart"
description: "A file, such as a screenshot, as bytes with its media type (the AI SDK's file part). The data is the bytes, or the same bytes as base64 text in Base64; DataT..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionFilePart"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionFilePart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionFilePart : DecisionStatePart, IEquatable<DecisionStatePart>, IEquatable<DecisionFilePart>
```


A file, such as a screenshot, as bytes with its media type (the AI SDK's `file` part). The data is the bytes, or the same bytes as
base64 text in [Base64](#E2E_Decision_Sdk_DecisionFilePart_Base64); [DataType](#E2E_Decision_Sdk_DecisionFilePart_DataType) names another kind (`url`, `reference`, or `text`) a provider may reject.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DecisionStatePart](/api/E2E.Decision.Sdk/DecisionStatePart/)

**Implements:** [IEquatable&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionFilePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionFilePart__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart__ctor_System_String_System_Byte___"></a>

### DecisionFilePart(string, byte[])


```csharp
public DecisionFilePart(string MediaType, byte[] Data)
```


A file, such as a screenshot, as bytes with its media type (the AI SDK's `file` part). The data is the bytes, or the same bytes as
base64 text in [Base64](#E2E_Decision_Sdk_DecisionFilePart_Base64); [DataType](#E2E_Decision_Sdk_DecisionFilePart_DataType) names another kind (`url`, `reference`, or `text`) a provider may reject.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `MediaType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionFilePart_Base64_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Base64"></a>

### Base64


```csharp
public string? Base64 { get; init; }
```


The data as base64 text, when it came that way; [Data](#E2E_Decision_Sdk_DecisionFilePart_Data) is then ignored.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_Data_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Data"></a>

### Data


```csharp
public byte[] Data { get; init; }
```


**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)

<a id="E2E_Decision_Sdk_DecisionFilePart_DataType_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_DataType"></a>

### DataType


```csharp
public string DataType { get; init; }
```


The kind of data the part carries: `data`, `url`, `reference`, or `text`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_Filename_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Filename"></a>

### Filename


```csharp
public string? Filename { get; init; }
```


The file's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_MediaType_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_MediaType"></a>

### MediaType


```csharp
public string MediaType { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The options for this part, keyed by provider name. Each value is a JSON value.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_DecisionFilePart_Reference_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Reference"></a>

### Reference


```csharp
public JsonElement? Reference { get; init; }
```


The provider references of a `reference` part.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_DecisionFilePart_Text_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


The text of a `text` part.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_Url_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_Url"></a>

### Url


```csharp
public Uri? Url { get; init; }
```


The URL of a `url` part.

**Value:** [Uri](https://learn.microsoft.com/dotnet/api/system.uri)

## Methods

<a id="E2E_Decision_Sdk_DecisionFilePart_GetBase64_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_GetBase64"></a>

### GetBase64()


```csharp
public string GetBase64()
```


The file's bytes as base64 text.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_DecisionFilePart_GetBytes_"></a>
<a id="E2E_Decision_Sdk_DecisionFilePart_GetBytes"></a>

### GetBytes()


```csharp
public byte[] GetBytes()
```


The file's bytes.

**Returns:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)
