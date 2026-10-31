---
title: "DocumentElement"
description: "A mutable control on a DocumentPage."
editUrl: false
prev: false
next: false
sidebar:
  label: "DocumentElement"
  hidden: true
---

<a id="E2E_Engine_DocumentElement"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class DocumentElement
```


A mutable control on a [DocumentPage](/api/E2E.Engine/DocumentPage/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L459)

## Properties

<a id="E2E_Engine_DocumentElement_Attributes_"></a>
<a id="E2E_Engine_DocumentElement_Attributes"></a>

### Attributes


```csharp
public Dictionary<string, string> Attributes { get; }
```


Attributes `getAttribute` and `toHaveAttribute` read, such as `href` or `aria-label`.

**Value:** [Dictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L496)

<a id="E2E_Engine_DocumentElement_Checked_"></a>
<a id="E2E_Engine_DocumentElement_Checked"></a>

### Checked


```csharp
public bool Checked { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L479)

<a id="E2E_Engine_DocumentElement_Children_"></a>
<a id="E2E_Engine_DocumentElement_Children"></a>

### Children


```csharp
public List<DocumentElement> Children { get; }
```


**Value:** [List&lt;DocumentElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L508)

<a id="E2E_Engine_DocumentElement_Disabled_"></a>
<a id="E2E_Engine_DocumentElement_Disabled"></a>

### Disabled


```csharp
public bool Disabled { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L477)

<a id="E2E_Engine_DocumentElement_Expanded_"></a>
<a id="E2E_Engine_DocumentElement_Expanded"></a>

### Expanded


```csharp
public bool Expanded { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L485)

<a id="E2E_Engine_DocumentElement_Focused_"></a>
<a id="E2E_Engine_DocumentElement_Focused"></a>

### Focused


```csharp
public bool Focused { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L491)

<a id="E2E_Engine_DocumentElement_Hidden_"></a>
<a id="E2E_Engine_DocumentElement_Hidden"></a>

### Hidden


```csharp
public bool Hidden { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L481)

<a id="E2E_Engine_DocumentElement_InputPurpose_"></a>
<a id="E2E_Engine_DocumentElement_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L473)

<a id="E2E_Engine_DocumentElement_Level_"></a>
<a id="E2E_Engine_DocumentElement_Level"></a>

### Level


```csharp
public int? Level { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L475)

<a id="E2E_Engine_DocumentElement_Name_"></a>
<a id="E2E_Engine_DocumentElement_Name"></a>

### Name


```csharp
public string? Name { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L463)

<a id="E2E_Engine_DocumentElement_NavigateTo_"></a>
<a id="E2E_Engine_DocumentElement_NavigateTo"></a>

### NavigateTo


```csharp
public string? NavigateTo { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L493)

<a id="E2E_Engine_DocumentElement_OnFill_"></a>
<a id="E2E_Engine_DocumentElement_OnFill"></a>

### OnFill


```csharp
public Action<string>? OnFill { get; init; }
```


**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L503)

<a id="E2E_Engine_DocumentElement_OnScroll_"></a>
<a id="E2E_Engine_DocumentElement_OnScroll"></a>

### OnScroll


```csharp
public Action<ScrollDirection>? OnScroll { get; set; }
```


Runs when this element is scrolled with [LocatorAction.Swipe](/api/E2E.Engine/LocatorAction.Swipe/).

**Value:** [Action&lt;ScrollDirection&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L506)

<a id="E2E_Engine_DocumentElement_OnTap_"></a>
<a id="E2E_Engine_DocumentElement_OnTap"></a>

### OnTap


```csharp
public Action? OnTap { get; init; }
```


**Value:** [Action](https://learn.microsoft.com/dotnet/api/system.action)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L501)

<a id="E2E_Engine_DocumentElement_Placeholder_"></a>
<a id="E2E_Engine_DocumentElement_Placeholder"></a>

### Placeholder


```csharp
public string? Placeholder { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L471)

<a id="E2E_Engine_DocumentElement_Pressed_"></a>
<a id="E2E_Engine_DocumentElement_Pressed"></a>

### Pressed


```csharp
public bool Pressed { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L487)

<a id="E2E_Engine_DocumentElement_Rect_"></a>
<a id="E2E_Engine_DocumentElement_Rect"></a>

### Rect


```csharp
public BoundingBox? Rect { get; set; }
```


The box `boundingBox` reports. Null when the page does not lay it out.

**Value:** [BoundingBox](/api/E2E.Engine/BoundingBox/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L499)

<a id="E2E_Engine_DocumentElement_Role_"></a>
<a id="E2E_Engine_DocumentElement_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L461)

<a id="E2E_Engine_DocumentElement_Secure_"></a>
<a id="E2E_Engine_DocumentElement_Secure"></a>

### Secure


```csharp
public bool Secure { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L489)

<a id="E2E_Engine_DocumentElement_Selected_"></a>
<a id="E2E_Engine_DocumentElement_Selected"></a>

### Selected


```csharp
public bool Selected { get; set; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L483)

<a id="E2E_Engine_DocumentElement_TestId_"></a>
<a id="E2E_Engine_DocumentElement_TestId"></a>

### TestId


```csharp
public string? TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L469)

<a id="E2E_Engine_DocumentElement_Text_"></a>
<a id="E2E_Engine_DocumentElement_Text"></a>

### Text


```csharp
public string? Text { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L465)

<a id="E2E_Engine_DocumentElement_Value_"></a>
<a id="E2E_Engine_DocumentElement_Value"></a>

### Value


```csharp
public string? Value { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L467)
