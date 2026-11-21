---
title: "LanguageModelV4StreamResult"
description: "The AI SDK's LanguageModelV4StreamResult (language-model-v4-stream-result.ts). The stream owns the HTTP response: it is released when the stream is read to i..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LanguageModelV4StreamResult"
  hidden: true
---

<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class LanguageModelV4StreamResult
```


The AI SDK's `LanguageModelV4StreamResult` (`language-model-v4-stream-result.ts`). The stream owns the HTTP response: it is
released when the stream is read to its end, when its enumeration is disposed, or when it fails.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LanguageModelV4.cs)

## Properties

<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Request_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Request"></a>

### Request


```csharp
public LanguageModelV4RequestMetadata? Request { get; init; }
```


The request information, for telemetry and debugging.

**Value:** [LanguageModelV4RequestMetadata](/api/E2E.Decision.Sdk/LanguageModelV4RequestMetadata/)

<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Response_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Response"></a>

### Response


```csharp
public LanguageModelV4ResponseMetadata? Response { get; init; }
```


The response data: its headers.

**Value:** [LanguageModelV4ResponseMetadata](/api/E2E.Decision.Sdk/LanguageModelV4ResponseMetadata/)

<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Stream_"></a>
<a id="E2E_Decision_Sdk_LanguageModelV4StreamResult_Stream"></a>

### Stream


```csharp
public required IAsyncEnumerable<LanguageModelV4StreamPart> Stream { get; init; }
```


The stream.

**Value:** [IAsyncEnumerable&lt;LanguageModelV4StreamPart&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)
