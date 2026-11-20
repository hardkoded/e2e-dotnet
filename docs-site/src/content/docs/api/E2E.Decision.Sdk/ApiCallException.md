---
title: "ApiCallException"
description: "The AI SDK's APICallError: a provider call that failed. RunAsync retries it when IsRetryable, waiting for the provider's retry-after-ms or retry-after header..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ApiCallException"
  hidden: true
---

<a id="E2E_Decision_Sdk_ApiCallException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class ApiCallException : Exception, ISerializable
```


The AI SDK's `APICallError`: a provider call that failed. `RunAsync` retries it when
[IsRetryable](#E2E_Decision_Sdk_ApiCallException_IsRetryable), waiting for the provider's `retry-after-ms` or `retry-after` header when it is reasonable.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_ApiCallException__ctor_"></a>
<a id="E2E_Decision_Sdk_ApiCallException__ctor_System_String_System_Nullable_System_Int32__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_Nullable_System_Boolean__System_Exception_"></a>

### `ApiCallException(string, int?, IReadOnlyDictionary<string, string>?, bool?, Exception?)`


```csharp
public ApiCallException(string message, int? statusCode = null, IReadOnlyDictionary<string, string>? responseHeaders = null, bool? isRetryable = null, Exception? innerException = null)
```


The AI SDK's `APICallError`: a provider call that failed. `RunAsync` retries it when
[IsRetryable](#E2E_Decision_Sdk_ApiCallException_IsRetryable), waiting for the provider's `retry-after-ms` or `retry-after` header when it is reasonable.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int?](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `responseHeaders` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `isRetryable` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `innerException` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

## Properties

<a id="E2E_Decision_Sdk_ApiCallException_IsRetryable_"></a>
<a id="E2E_Decision_Sdk_ApiCallException_IsRetryable"></a>

### IsRetryable


```csharp
public bool IsRetryable { get; }
```


Whether the call may be sent again. Upstream's default: no answer at all, or a 408, 409, 429, or 5xx.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_ApiCallException_ParsedData_"></a>
<a id="E2E_Decision_Sdk_ApiCallException_ParsedData"></a>

### ParsedData


```csharp
public JsonElement? ParsedData { get; init; }
```


The body the provider SDK already parsed, when it did (upstream `APICallError.data`; `Exception.Data` is taken). Null means there is none.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Sdk_ApiCallException_ResponseBody_"></a>
<a id="E2E_Decision_Sdk_ApiCallException_ResponseBody"></a>

### ResponseBody


```csharp
public string? ResponseBody { get; init; }
```


The body of the provider's answer, when it sent one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ApiCallException_ResponseHeaders_"></a>
<a id="E2E_Decision_Sdk_ApiCallException_ResponseHeaders"></a>

### ResponseHeaders


```csharp
public IReadOnlyDictionary<string, string>? ResponseHeaders { get; }
```


The provider's response headers, lowercase names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_ApiCallException_StatusCode_"></a>
<a id="E2E_Decision_Sdk_ApiCallException_StatusCode"></a>

### StatusCode


```csharp
public int? StatusCode { get; }
```


The HTTP status of the provider's answer; null when no answer came.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
