---
title: "LaunchArgumentValueKind"
description: "What a LaunchArgumentValue holds."
editUrl: false
prev: false
next: false
sidebar:
  label: "LaunchArgumentValueKind"
  hidden: true
---

<a id="E2E_LaunchArgumentValueKind"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum LaunchArgumentValueKind
```


What a [LaunchArgumentValue](/api/E2E/LaunchArgumentValue/) holds.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/LaunchArgumentValue.cs)

## Fields

<a id="E2E_LaunchArgumentValueKind_Boolean"></a>

### Boolean


```csharp
Boolean = 2
```


A boolean.

**Value:** [LaunchArgumentValueKind](./)

<a id="E2E_LaunchArgumentValueKind_List"></a>

### List


```csharp
List = 3
```


A list of strings.

**Value:** [LaunchArgumentValueKind](./)

<a id="E2E_LaunchArgumentValueKind_Number"></a>

### Number


```csharp
Number = 1
```


A finite number, an integer below 2^53 in magnitude or a fraction.

**Value:** [LaunchArgumentValueKind](./)

<a id="E2E_LaunchArgumentValueKind_String"></a>

### String


```csharp
String = 0
```


A string.

**Value:** [LaunchArgumentValueKind](./)
