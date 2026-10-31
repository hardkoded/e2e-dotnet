---
title: "WebBasicAuth"
description: "HTTP basic authentication the browser answers a 401 challenge with."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebBasicAuth"
  hidden: true
---

<a id="E2E_Engine_WebBasicAuth"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebBasicAuth
```


HTTP basic authentication the browser answers a `401` challenge with.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L37)

## Constructors

<a id="E2E_Engine_WebBasicAuth__ctor_"></a>
<a id="E2E_Engine_WebBasicAuth__ctor_System_String_E2E_Secret_"></a>

### WebBasicAuth(string, Secret)


```csharp
public WebBasicAuth(string username, Secret password)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `username` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `password` | [Secret](/api/E2E/Secret/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L47)

<a id="E2E_Engine_WebBasicAuth__ctor_System_String_System_String_"></a>

### WebBasicAuth(string, string)


```csharp
public WebBasicAuth(string username, string password)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `username` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `password` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L39)

## Properties

<a id="E2E_Engine_WebBasicAuth_Username_"></a>
<a id="E2E_Engine_WebBasicAuth_Username"></a>

### Username


```csharp
public string Username { get; }
```


The user name. RFC 7617 does not allow `:` in one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebEngine.cs#L56)
