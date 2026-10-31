---
title: "WebRouteRequest"
description: "The request a route intercepted."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebRouteRequest"
  hidden: true
---

<a id="E2E_Engine_WebRouteRequest"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record WebRouteRequest : IEquatable<WebRouteRequest>
```


The request a route intercepted.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WebRouteRequest&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L231)

## Properties

<a id="E2E_Engine_WebRouteRequest_Headers_"></a>
<a id="E2E_Engine_WebRouteRequest_Headers"></a>

### Headers


```csharp
public required IReadOnlyDictionary<string, string> Headers { get; init; }
```


Request headers, with lower-cased names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L238)

<a id="E2E_Engine_WebRouteRequest_Method_"></a>
<a id="E2E_Engine_WebRouteRequest_Method"></a>

### Method


```csharp
public required string Method { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L235)

<a id="E2E_Engine_WebRouteRequest_PostData_"></a>
<a id="E2E_Engine_WebRouteRequest_PostData"></a>

### PostData


```csharp
public string? PostData { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L240)

<a id="E2E_Engine_WebRouteRequest_Url_"></a>
<a id="E2E_Engine_WebRouteRequest_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L233)
