---
title: "HookScope"
description: "The tests a hook failure leaves unverified (upstream's HookScope)."
editUrl: false
prev: false
next: false
sidebar:
  label: "HookScope"
  hidden: true
---

<a id="E2E_Reports_HookScope"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record HookScope : IEquatable<HookScope>
```


The tests a hook failure leaves unverified (upstream's `HookScope`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;HookScope&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L869)

## Properties

<a id="E2E_Reports_HookScope_File_"></a>
<a id="E2E_Reports_HookScope_File"></a>

### File


```csharp
public string File { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L871)

<a id="E2E_Reports_HookScope_TargetId_"></a>
<a id="E2E_Reports_HookScope_TargetId"></a>

### TargetId


```csharp
public string TargetId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L873)

<a id="E2E_Reports_HookScope_TitlePath_"></a>
<a id="E2E_Reports_HookScope_TitlePath"></a>

### TitlePath


```csharp
public IReadOnlyList<string> TitlePath { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L875)
