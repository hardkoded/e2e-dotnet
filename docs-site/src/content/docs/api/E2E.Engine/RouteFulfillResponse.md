---
title: "RouteFulfillResponse"
description: "How FulfillAsync(RouteFulfillResponse) answers a request. Give at most one of Json, Body, or Path; none is an empty body."
editUrl: false
prev: false
next: false
sidebar:
  label: "RouteFulfillResponse"
  hidden: true
---

<a id="E2E_Engine_RouteFulfillResponse"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record RouteFulfillResponse : IEquatable<RouteFulfillResponse>
```


How [FulfillAsync(RouteFulfillResponse)](/api/E2E/WebRoute/#E2E_WebRoute_FulfillAsync_E2E_Engine_RouteFulfillResponse_) answers a request. Give at most one of
[Json](#E2E_Engine_RouteFulfillResponse_Json), [Body](#E2E_Engine_RouteFulfillResponse_Body), or [Path](#E2E_Engine_RouteFulfillResponse_Path); none is an empty body.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RouteFulfillResponse&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L247)

## Properties

<a id="E2E_Engine_RouteFulfillResponse_Body_"></a>
<a id="E2E_Engine_RouteFulfillResponse_Body"></a>

### Body


```csharp
public string? Body { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L260)

<a id="E2E_Engine_RouteFulfillResponse_ContentType_"></a>
<a id="E2E_Engine_RouteFulfillResponse_ContentType"></a>

### ContentType


```csharp
public string? ContentType { get; init; }
```


Sets the `Content-Type` response header.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L255)

<a id="E2E_Engine_RouteFulfillResponse_Headers_"></a>
<a id="E2E_Engine_RouteFulfillResponse_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L252)

<a id="E2E_Engine_RouteFulfillResponse_Json_"></a>
<a id="E2E_Engine_RouteFulfillResponse_Json"></a>

### Json


```csharp
public object? Json { get; init; }
```


A JSON-safe value, sent serialized as `application/json` unless [ContentType](#E2E_Engine_RouteFulfillResponse_ContentType) says otherwise.

**Value:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L258)

<a id="E2E_Engine_RouteFulfillResponse_Path_"></a>
<a id="E2E_Engine_RouteFulfillResponse_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


A file relative to the project root. Its `Content-Type` follows its extension unless [ContentType](#E2E_Engine_RouteFulfillResponse_ContentType) or a `content-type` header sets one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L263)

<a id="E2E_Engine_RouteFulfillResponse_Status_"></a>
<a id="E2E_Engine_RouteFulfillResponse_Status"></a>

### Status


```csharp
public int? Status { get; init; }
```


HTTP status from 100 to 599. Defaults to 200.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L250)
