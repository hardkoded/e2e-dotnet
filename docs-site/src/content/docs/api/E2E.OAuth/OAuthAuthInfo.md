---
title: "OAuthAuthInfo"
description: "Where the user is sent and, for device flows, the code to enter there."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthAuthInfo"
  hidden: true
---

<a id="E2E_OAuth_OAuthAuthInfo"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed record OAuthAuthInfo : IEquatable<OAuthAuthInfo>
```


Where the user is sent and, for device flows, the code to enter there.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OAuthAuthInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L51)

## Constructors

<a id="E2E_OAuth_OAuthAuthInfo__ctor_"></a>
<a id="E2E_OAuth_OAuthAuthInfo__ctor_System_String_System_String_System_String_"></a>

### OAuthAuthInfo(string, string, string?)


```csharp
public OAuthAuthInfo(string Url, string Instructions, string? UserCode = null)
```


Where the user is sent and, for device flows, the code to enter there.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Url` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Instructions` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `UserCode` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L51)

## Properties

<a id="E2E_OAuth_OAuthAuthInfo_Instructions_"></a>
<a id="E2E_OAuth_OAuthAuthInfo_Instructions"></a>

### Instructions


```csharp
public string Instructions { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L51)

<a id="E2E_OAuth_OAuthAuthInfo_Url_"></a>
<a id="E2E_OAuth_OAuthAuthInfo_Url"></a>

### Url


```csharp
public string Url { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L51)

<a id="E2E_OAuth_OAuthAuthInfo_UserCode_"></a>
<a id="E2E_OAuth_OAuthAuthInfo_UserCode"></a>

### UserCode


```csharp
public string? UserCode { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L51)
