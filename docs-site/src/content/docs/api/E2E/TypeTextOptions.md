---
title: "TypeTextOptions"
description: "What TypeTextAsync is asked beside the value: upstream's typing argument of typeText, an inline object type there (.NET-only name)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeTextOptions"
  hidden: true
---

<a id="E2E_TypeTextOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TypeTextOptions
```


What `TypeTextAsync` is asked beside the value: upstream's `typing` argument of `typeText`, an
inline object type there (.NET-only name).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Properties

<a id="E2E_TypeTextOptions_Replace_"></a>
<a id="E2E_TypeTextOptions_Replace"></a>

### Replace


```csharp
public bool Replace { get; init; }
```


Select all and delete before typing, for a field that visibly holds text to remove. Off, the value goes in at the caret.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
