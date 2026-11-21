---
title: "PathsAnswer"
description: "The answer of Paths: the project-relative paths to attach."
editUrl: false
prev: false
next: false
sidebar:
  label: "PathsAnswer"
  hidden: true
---

<a id="E2E_Decision_PathsAnswer"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record PathsAnswer : IEquatable<PathsAnswer>
```


The answer of [Paths](/api/E2E.Decision/Text/#E2E_Decision_Text_Paths): the project-relative paths to attach.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;PathsAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Constructors

<a id="E2E_Decision_PathsAnswer__ctor_"></a>
<a id="E2E_Decision_PathsAnswer__ctor_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `PathsAnswer(IReadOnlyList<string>)`


```csharp
public PathsAnswer(IReadOnlyList<string> Paths)
```


The answer of [Paths](/api/E2E.Decision/Text/#E2E_Decision_Text_Paths): the project-relative paths to attach.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Paths` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_PathsAnswer_Paths_"></a>
<a id="E2E_Decision_PathsAnswer_Paths"></a>

### Paths


```csharp
public IReadOnlyList<string> Paths { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
