---
title: "ReportExploreChange"
description: "The change since --changed-since an exploration planned from, by counts."
editUrl: false
prev: false
next: false
sidebar:
  label: "ReportExploreChange"
  hidden: true
---

<a id="E2E_ReportExploreChange"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ReportExploreChange : IEquatable<ReportExploreChange>
```


The change since `--changed-since` an exploration planned from, by counts.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ReportExploreChange&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/ReportExplore.cs)

## Constructors

<a id="E2E_ReportExploreChange__ctor_"></a>
<a id="E2E_ReportExploreChange__ctor_System_String_System_Int32_"></a>

### ReportExploreChange(string, int)


```csharp
public ReportExploreChange(string Base, int Files)
```


The change since `--changed-since` an exploration planned from, by counts.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Base` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The merge base the change is measured from. |
| `Files` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | How many files changed since &lt;code class="paramref"&gt;Base&lt;/code&gt;; 0 when nothing did. |

## Properties

<a id="E2E_ReportExploreChange_Base_"></a>
<a id="E2E_ReportExploreChange_Base"></a>

### Base


```csharp
public string Base { get; init; }
```


The merge base the change is measured from.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ReportExploreChange_Files_"></a>
<a id="E2E_ReportExploreChange_Files"></a>

### Files


```csharp
public int Files { get; init; }
```


How many files changed since &lt;code class="paramref"&gt;Base&lt;/code&gt;; 0 when nothing did.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
