---
title: "RouteContinueOverrides"
description: "What ContinueAsync(RouteContinueOverrides) changes about the request before it goes to the network."
editUrl: false
prev: false
next: false
sidebar:
  label: "RouteContinueOverrides"
  hidden: true
---

<a id="E2E_Engine_RouteContinueOverrides"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record RouteContinueOverrides : IEquatable<RouteContinueOverrides>
```


What [ContinueAsync(RouteContinueOverrides)](/api/E2E/WebRoute/#E2E_WebRoute_ContinueAsync_E2E_Engine_RouteContinueOverrides_) changes about the request before it goes to the network.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;RouteContinueOverrides&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs)

## Properties

<a id="E2E_Engine_RouteContinueOverrides_DropHeaders_"></a>
<a id="E2E_Engine_RouteContinueOverrides_DropHeaders"></a>

### DropHeaders


```csharp
public IReadOnlyCollection<string>? DropHeaders { get; init; }
```


Names of request headers to send without (upstream sets such a header to `undefined` in `headers`). .NET-only name: a
`string?` value in [Headers](#E2E_Engine_RouteContinueOverrides_Headers) would make every caller that passes a `Dictionary<string, string>` a nullable
warning. It applies to the request's own headers when [Headers](#E2E_Engine_RouteContinueOverrides_Headers) is unset, and also on a redirect.

**Value:** [IReadOnlyCollection&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)

<a id="E2E_Engine_RouteContinueOverrides_Headers_"></a>
<a id="E2E_Engine_RouteContinueOverrides_Headers"></a>

### Headers


```csharp
public IReadOnlyDictionary<string, string>? Headers { get; init; }
```


Replaces every request header; the configured headers for an allowed origin are added on top.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_RouteContinueOverrides_Method_"></a>
<a id="E2E_Engine_RouteContinueOverrides_Method"></a>

### Method


```csharp
public string? Method { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_RouteContinueOverrides_PostData_"></a>
<a id="E2E_Engine_RouteContinueOverrides_PostData"></a>

### PostData


```csharp
public string? PostData { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_RouteContinueOverrides_Url_"></a>
<a id="E2E_Engine_RouteContinueOverrides_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Resolved against the base URL like `app.open`; must keep the request's scheme.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
