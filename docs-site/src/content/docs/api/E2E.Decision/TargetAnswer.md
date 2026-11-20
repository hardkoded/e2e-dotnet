---
title: "TargetAnswer"
description: "The answer of Target: a short visual description of the control."
editUrl: false
prev: false
next: false
sidebar:
  label: "TargetAnswer"
  hidden: true
---

<a id="E2E_Decision_TargetAnswer"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record TargetAnswer : IEquatable<TargetAnswer>
```


The answer of [Target](/api/E2E.Decision/Text/#E2E_Decision_Text_Target): a short visual description of the control.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TargetAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Constructors

<a id="E2E_Decision_TargetAnswer__ctor_"></a>
<a id="E2E_Decision_TargetAnswer__ctor_System_String_"></a>

### TargetAnswer(string)


```csharp
public TargetAnswer(string Target)
```


The answer of [Target](/api/E2E.Decision/Text/#E2E_Decision_Text_Target): a short visual description of the control.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_TargetAnswer_Target_"></a>
<a id="E2E_Decision_TargetAnswer_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
