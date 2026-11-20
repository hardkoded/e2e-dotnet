---
title: "FieldInput"
description: "What the text helpers read: the goal, the page, recent actions, and for a fill, the field (upstream's FieldInput)."
editUrl: false
prev: false
next: false
sidebar:
  label: "FieldInput"
  hidden: true
---

<a id="E2E_Decision_FieldInput"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class FieldInput
```


What the text helpers read: the goal, the page, recent actions, and for a fill, the field (upstream's `FieldInput`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Properties

<a id="E2E_Decision_FieldInput_Context_"></a>
<a id="E2E_Decision_FieldInput_Context"></a>

### Context


```csharp
public string? Context { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_FieldInput_Field_"></a>
<a id="E2E_Decision_FieldInput_Field"></a>

### Field


```csharp
public FieldDescription? Field { get; init; }
```


**Value:** [FieldDescription](/api/E2E.Decision/FieldDescription/)

<a id="E2E_Decision_FieldInput_Goal_"></a>
<a id="E2E_Decision_FieldInput_Goal"></a>

### Goal


```csharp
public required string Goal { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_FieldInput_Page_"></a>
<a id="E2E_Decision_FieldInput_Page"></a>

### Page


```csharp
public required string Page { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_FieldInput_Params_"></a>
<a id="E2E_Decision_FieldInput_Params"></a>

### Params


```csharp
public required JsonObject Params { get; init; }
```


**Value:** [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject)

<a id="E2E_Decision_FieldInput_RecentActions_"></a>
<a id="E2E_Decision_FieldInput_RecentActions"></a>

### RecentActions


```csharp
public required IReadOnlyList<HistoryEntry> RecentActions { get; init; }
```


**Value:** [IReadOnlyList&lt;HistoryEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
