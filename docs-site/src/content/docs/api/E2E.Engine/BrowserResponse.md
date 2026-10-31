---
title: "BrowserResponse"
description: "A response an engine observed, with its body still being read."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserResponse"
  hidden: true
---

<a id="E2E_Engine_BrowserResponse"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserResponse : IEquatable<BrowserResponse>
```


A response an engine observed, with its body still being read.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserResponse&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L191)

## Properties

<a id="E2E_Engine_BrowserResponse_Body_"></a>
<a id="E2E_Engine_BrowserResponse_Body"></a>

### Body


```csharp
public required Task<string> Body { get; init; }
```


The body as text. Faults with `ACTION_FAILED` when the browser could not read it.

**Value:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L201)

<a id="E2E_Engine_BrowserResponse_Headers_"></a>
<a id="E2E_Engine_BrowserResponse_Headers"></a>

### Headers


```csharp
public required IReadOnlyDictionary<string, string> Headers { get; init; }
```


Response headers, with lower-cased names.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L198)

<a id="E2E_Engine_BrowserResponse_Status_"></a>
<a id="E2E_Engine_BrowserResponse_Status"></a>

### Status


```csharp
public required int Status { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L195)

<a id="E2E_Engine_BrowserResponse_Url_"></a>
<a id="E2E_Engine_BrowserResponse_Url"></a>

### Url


```csharp
public required string Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IBrowserSession.cs#L193)
