---
title: "HistoryEntry"
description: "One step of history the decision model reads (upstream's HistoryEntry)."
editUrl: false
prev: false
next: false
sidebar:
  label: "HistoryEntry"
  hidden: true
---

<a id="E2E_Decision_HistoryEntry"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record HistoryEntry : IEquatable<HistoryEntry>
```


One step of history the decision model reads (upstream's `HistoryEntry`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;HistoryEntry&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Questions.cs)

## Properties

<a id="E2E_Decision_HistoryEntry_Action_"></a>
<a id="E2E_Decision_HistoryEntry_Action"></a>

### Action


```csharp
public required string Action { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_HistoryEntry_Error_"></a>
<a id="E2E_Decision_HistoryEntry_Error"></a>

### Error


```csharp
public string? Error { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_HistoryEntry_Note_"></a>
<a id="E2E_Decision_HistoryEntry_Note"></a>

### Note


```csharp
public string? Note { get; init; }
```


What the engine reported for the action, when that tells the model something.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_HistoryEntry_PageChanged_"></a>
<a id="E2E_Decision_HistoryEntry_PageChanged"></a>

### PageChanged


```csharp
public bool? PageChanged { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_HistoryEntry_Replayed_"></a>
<a id="E2E_Decision_HistoryEntry_Replayed"></a>

### Replayed


```csharp
public bool Replayed { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_HistoryEntry_Text_"></a>
<a id="E2E_Decision_HistoryEntry_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_HistoryEntry_Uncertain_"></a>
<a id="E2E_Decision_HistoryEntry_Uncertain"></a>

### Uncertain


```csharp
public bool Uncertain { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
