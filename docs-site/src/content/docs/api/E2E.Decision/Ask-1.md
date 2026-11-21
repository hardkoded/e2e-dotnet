---
title: "Ask<T>"
description: "One structured ask (upstream's Ask): the system prompt, how the input is introduced, the answer's shape, and how far back it reads. Schema is the closed JSON..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Ask<T>"
  hidden: true
---

<a id="E2E_Decision_Ask_1"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class Ask<T>
```


One structured ask (upstream's `Ask`): the system prompt, how the input is introduced, the answer's shape, and how far back it
reads. `Schema` is the closed JSON schema the provider is asked to fill, and `Parse` is what upstream's zod
schema does with the answer: it returns null for one that does not match.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Properties

<a id="E2E_Decision_Ask_1_History_"></a>
<a id="E2E_Decision_Ask_1_History"></a>

### History


```csharp
public required int History { get; init; }
```


Recent actions the ask sees: a few for a field value, the whole step for a drawn control the model must count taps to.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Ask_1_Lead_"></a>
<a id="E2E_Decision_Ask_1_Lead"></a>

### Lead


```csharp
public required string Lead { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Ask_1_Parse_"></a>
<a id="E2E_Decision_Ask_1_Parse"></a>

### Parse


```csharp
public required Func<JsonElement, T?> Parse { get; init; }
```


**Value:** [Func&lt;JsonElement, T&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Decision_Ask_1_Schema_"></a>
<a id="E2E_Decision_Ask_1_Schema"></a>

### Schema


```csharp
public required JsonElement Schema { get; init; }
```


**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_Decision_Ask_1_System_"></a>
<a id="E2E_Decision_Ask_1_System"></a>

### System


```csharp
public required string System { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
