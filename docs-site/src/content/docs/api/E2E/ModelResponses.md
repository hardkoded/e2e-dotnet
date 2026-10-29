---
title: "ModelResponses"
description: "Builds the tool calls a ScriptedModel (or a test) returns."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelResponses"
  hidden: true
---

<a id="E2E_ModelResponses"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class ModelResponses
```


Builds the tool calls a [ScriptedModel](/api/E2E/ScriptedModel/) (or a test) returns.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L135)

## Methods

<a id="E2E_ModelResponses_Call_"></a>
<a id="E2E_ModelResponses_Call_System_String_System_Object_"></a>

### Call(string, object)


```csharp
public static ModelResponse Call(string name, object arguments)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `arguments` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

**Returns:** [ModelResponse](/api/E2E/ModelResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L166)

<a id="E2E_ModelResponses_Done_"></a>
<a id="E2E_ModelResponses_Done_System_String_System_String_System_String_"></a>

### Done(string, string, string?)


```csharp
public static ModelResponse Done(string status, string summary, string? code = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `status` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `summary` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelResponse](/api/E2E/ModelResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L137)

<a id="E2E_ModelResponses_Fill_"></a>
<a id="E2E_ModelResponses_Fill_System_String_System_String_System_String_"></a>

### Fill(string, string, string)


```csharp
public static ModelResponse Fill(string role, string name, string value)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelResponse](/api/E2E/ModelResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L161)

<a id="E2E_ModelResponses_Judgment_"></a>
<a id="E2E_ModelResponses_Judgment_System_String_System_String_"></a>

### Judgment(string, string)


```csharp
public static ModelResponse Judgment(string verdict, string explanation)
```


A judge's answer: one `agent-judgment-2` object as the content of the turn. &lt;code class="paramref"&gt;verdict&lt;/code&gt; is
`holds`, `fails`, or `inconclusive`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `verdict` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `explanation` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelResponse](/api/E2E/ModelResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L148)

<a id="E2E_ModelResponses_Tap_"></a>
<a id="E2E_ModelResponses_Tap_System_String_System_String_"></a>

### Tap(string, string)


```csharp
public static ModelResponse Tap(string role, string name)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `role` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelResponse](/api/E2E/ModelResponse/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L156)
