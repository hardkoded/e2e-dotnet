---
title: "LocatorAction.Select"
description: "Selects one option: the one whose value or label is Text, or the one Option names by its label, its value, or its index. Exactly one of the two is set."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Select"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Select"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Select : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Select>
```


Selects one option: the one whose value or label is &lt;code class="paramref"&gt;Text&lt;/code&gt;, or the one &lt;code class="paramref"&gt;Option&lt;/code&gt; names by its label,
its value, or its index. Exactly one of the two is set.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Select&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs)

## Constructors

<a id="E2E_Engine_LocatorAction_Select__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Select__ctor_E2E_SelectOption_"></a>

### Select(SelectOption)


```csharp
public Select(SelectOption option)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `option` | [SelectOption](/api/E2E/SelectOption/) |  |

<a id="E2E_Engine_LocatorAction_Select__ctor_System_String_E2E_SelectOption_"></a>

### Select(string?, SelectOption?)


```csharp
public Select(string? Text, SelectOption? Option = null)
```


Selects one option: the one whose value or label is &lt;code class="paramref"&gt;Text&lt;/code&gt;, or the one &lt;code class="paramref"&gt;Option&lt;/code&gt; names by its label,
its value, or its index. Exactly one of the two is set.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Option` | [SelectOption](/api/E2E/SelectOption/) |  |

## Properties

<a id="E2E_Engine_LocatorAction_Select_Option_"></a>
<a id="E2E_Engine_LocatorAction_Select_Option"></a>

### Option


```csharp
public SelectOption? Option { get; init; }
```


**Value:** [SelectOption](/api/E2E/SelectOption/)

<a id="E2E_Engine_LocatorAction_Select_Text_"></a>
<a id="E2E_Engine_LocatorAction_Select_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
