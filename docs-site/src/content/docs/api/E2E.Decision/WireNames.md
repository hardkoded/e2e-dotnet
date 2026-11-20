---
title: "WireNames"
description: "The names an operation, a control, and a terminal carry in a question and an answer. .NET-only: upstream's are string unions."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireNames"
  hidden: true
---

<a id="E2E_Decision_WireNames"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class WireNames
```


The names an operation, a control, and a terminal carry in a question and an answer. .NET-only: upstream's are string unions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Methods

<a id="E2E_Decision_WireNames_ControlNamed_"></a>
<a id="E2E_Decision_WireNames_ControlNamed_System_String_"></a>

### ControlNamed(string)


```csharp
public static Control? ControlNamed(string name)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `Control?`

<a id="E2E_Decision_WireNames_Of_"></a>
<a id="E2E_Decision_WireNames_Of_E2E_Decision_Control_"></a>

### Of(Control)


```csharp
public static string Of(Control control)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `control` | [Control](/api/E2E.Decision/Control/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_WireNames_Of_E2E_Decision_Operation_"></a>

### Of(Operation)


```csharp
public static string Of(Operation operation)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `operation` | [Operation](/api/E2E.Decision/Operation/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_WireNames_Of_E2E_Decision_Terminal_"></a>

### Of(Terminal)


```csharp
public static string Of(Terminal terminal)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `terminal` | [Terminal](/api/E2E.Decision/Terminal/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_WireNames_OperationNamed_"></a>
<a id="E2E_Decision_WireNames_OperationNamed_System_String_"></a>

### OperationNamed(string)


```csharp
public static Operation? OperationNamed(string name)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `Operation?`
