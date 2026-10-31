---
title: "WebViewport"
description: "A page size in CSS pixels."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebViewport"
  hidden: true
---

<a id="E2E_Engine_WebViewport"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record WebViewport : IEquatable<WebViewport>
```


A page size in CSS pixels.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WebViewport&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L15)

## Constructors

<a id="E2E_Engine_WebViewport__ctor_"></a>
<a id="E2E_Engine_WebViewport__ctor_System_Int32_System_Int32_"></a>

### WebViewport(int, int)


```csharp
public WebViewport(int Width, int Height)
```


A page size in CSS pixels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Width` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Height` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L15)

## Properties

<a id="E2E_Engine_WebViewport_Default_"></a>
<a id="E2E_Engine_WebViewport_Default"></a>

### Default


```csharp
public static WebViewport Default { get; }
```


The size every attempt's page starts with unless the options say otherwise: 1280 by 720.

**Value:** [WebViewport](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L18)

<a id="E2E_Engine_WebViewport_Height_"></a>
<a id="E2E_Engine_WebViewport_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L15)

<a id="E2E_Engine_WebViewport_Width_"></a>
<a id="E2E_Engine_WebViewport_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L15)
