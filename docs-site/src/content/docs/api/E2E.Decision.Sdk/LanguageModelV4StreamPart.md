---
title: "LanguageModelV4StreamPart"
description: "The AI SDK's LanguageModelV4StreamPart (language-model-v4-stream-part.ts) of the Gateway. The Gateway passes each part through z.any(), so Json is the part a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4StreamPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class LanguageModelV4StreamPart
```


The AI SDK's `LanguageModelV4StreamPart` (`language-model-v4-stream-part.ts`) of the Gateway. The Gateway passes each part
through `z.any()`, so [Json](#E2E_Decision_Sdk_LanguageModelV4StreamPart_Json) is the part as the Gateway sent it, unvalidated.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Json_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Json"></a>

### Json


```csharp
public required JsonElement Json { get; init; }
```


The part as the Gateway sent it. A `response-metadata` timestamp is still its ISO string here.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Timestamp_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Timestamp"></a>

### Timestamp


```csharp
public DateTimeOffset? Timestamp { get; init; }
```


The `timestamp` of a `response-metadata` part whose timestamp the Gateway sent as a string (upstream's `Date`).

**Value:** [DateTimeOffset?](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Type_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


The `type` of the part, such as `text-delta`, `finish`, or `raw`; empty when the part has none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Warnings_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamPart_Warnings"></a>

### Warnings


```csharp
public IReadOnlyList<SharedV4Warning>? Warnings { get; init; }
```


The warnings of a `stream-start` part.

**Value:** [IReadOnlyList&lt;SharedV4Warning&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
