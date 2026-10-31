---
title: "WebInitScript"
description: "A script every document runs before the page's own, in every tab and frame: JavaScript source, a file of it, or a function source the page calls. A string co..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebInitScript"
  hidden: true
---

<a id="E2E_Engine_WebInitScript"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebInitScript
```


A script every document runs before the page's own, in every tab and
frame: JavaScript source, a file of it, or a function source the page
calls. A string converts to source.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L67)

## Properties

<a id="E2E_Engine_WebInitScript_Function_"></a>
<a id="E2E_Engine_WebInitScript_Function"></a>

### Function


```csharp
public string? Function { get; }
```


The source of a function the page calls, such as `() => { ... }`,
or &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for source or a file. It cannot close over test
variables; `Browser.AddInitScriptAsync` passes it one JSON argument.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L87)

<a id="E2E_Engine_WebInitScript_Path_"></a>
<a id="E2E_Engine_WebInitScript_Path"></a>

### Path


```csharp
public string? Path { get; }
```


The file of JavaScript source, relative to the project root, or &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for source or a function.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L80)

<a id="E2E_Engine_WebInitScript_Source_"></a>
<a id="E2E_Engine_WebInitScript_Source"></a>

### Source


```csharp
public string? Source { get; }
```


The JavaScript source, or &lt;a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null"&gt;null&lt;/a&gt; for a file or a function.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L77)

## Methods

<a id="E2E_Engine_WebInitScript_FromFunction_"></a>
<a id="E2E_Engine_WebInitScript_FromFunction_System_String_"></a>

### FromFunction(string)


```csharp
public static WebInitScript FromFunction(string function)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `function` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [WebInitScript](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L101)

<a id="E2E_Engine_WebInitScript_FromPath_"></a>
<a id="E2E_Engine_WebInitScript_FromPath_System_String_"></a>

### FromPath(string)


```csharp
public static WebInitScript FromPath(string path)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [WebInitScript](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L95)

<a id="E2E_Engine_WebInitScript_FromSource_"></a>
<a id="E2E_Engine_WebInitScript_FromSource_System_String_"></a>

### FromSource(string)


```csharp
public static WebInitScript FromSource(string source)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `source` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [WebInitScript](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L89)

## Operators

<a id="E2E_Engine_WebInitScript_op_Implicit_"></a>
<a id="E2E_Engine_WebInitScript_op_Implicit_System_String__E2E_Engine_WebInitScript"></a>

### implicit operator WebInitScript(string)


```csharp
public static implicit operator WebInitScript(string source)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `source` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [WebInitScript](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L107)
