---
title: "WebRequest"
description: "A request the page made: what a route handler reads as Request and WaitForRequestAsync returns."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebRequest"
  hidden: true
---

<a id="E2E_Engine_WebRequest"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record WebRequest : IEquatable<WebRequest>
```


A request the page made: what a route handler reads as [Request](/api/E2E.Engine/IBrowserRoute/#E2E_Engine_IBrowserRoute_Request) and `WaitForRequestAsync` returns.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WebRequest&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs)

## Properties

<a id="E2E_Engine_WebRequest_Headers_"></a>
<a id="E2E_Engine_WebRequest_Headers"></a>

### Headers


```csharp
public required IReadOnlyDictionary<string, string> Headers { get; init; }
```


Request headers, with lower-cased names. The cookie headers the browser adds on its own are there only when a route
intercepts the request, so always in [Request](/api/E2E.Engine/IBrowserRoute/#E2E_Engine_IBrowserRoute_Request).

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_WebRequest_Method_"></a>
<a id="E2E_Engine_WebRequest_Method"></a>

### Method


```csharp
public required string Method { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_WebRequest_PostData_"></a>
<a id="E2E_Engine_WebRequest_PostData"></a>

### PostData


```csharp
public string? PostData { get; init; }
```


Request body as text; `null` when the request has none.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_WebRequest_Url_"></a>
<a id="E2E_Engine_WebRequest_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
