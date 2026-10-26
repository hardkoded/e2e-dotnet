---
title: "RoleOptions"
description: "Role query options. A null state is not checked."
editUrl: false
prev: false
next: false
sidebar:
  label: "RoleOptions"
  hidden: true
---

<a id="E2E_RoleOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RoleOptions : TextMatchOptions
```


Role query options. A null state is not checked.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TextMatchOptions](/api/E2E/TextMatchOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L130)

## Properties

<a id="E2E_RoleOptions_Checked_"></a>
<a id="E2E_RoleOptions_Checked"></a>

### Checked


```csharp
public bool? Checked { get; init; }
```


Requires the checked state.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L136)

<a id="E2E_RoleOptions_Disabled_"></a>
<a id="E2E_RoleOptions_Disabled"></a>

### Disabled


```csharp
public bool? Disabled { get; init; }
```


Requires the disabled state.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L139)

<a id="E2E_RoleOptions_Expanded_"></a>
<a id="E2E_RoleOptions_Expanded"></a>

### Expanded


```csharp
public bool? Expanded { get; init; }
```


Requires the expanded state.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L145)

<a id="E2E_RoleOptions_Level_"></a>
<a id="E2E_RoleOptions_Level"></a>

### Level


```csharp
public int? Level { get; init; }
```


Requires a heading level, 1 through 6.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L151)

<a id="E2E_RoleOptions_Name_"></a>
<a id="E2E_RoleOptions_Name"></a>

### Name


```csharp
public TextMatch? Name { get; init; }
```


Accessible name filter.

**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L133)

<a id="E2E_RoleOptions_Pressed_"></a>
<a id="E2E_RoleOptions_Pressed"></a>

### Pressed


```csharp
public bool? Pressed { get; init; }
```


Requires the pressed state of a toggle button.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L148)

<a id="E2E_RoleOptions_Selected_"></a>
<a id="E2E_RoleOptions_Selected"></a>

### Selected


```csharp
public bool? Selected { get; init; }
```


Requires the selected state.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L142)
