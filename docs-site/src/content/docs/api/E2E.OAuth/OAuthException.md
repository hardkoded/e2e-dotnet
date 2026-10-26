---
title: "OAuthException"
description: "Why a subscription login or a request made with it failed. Code is one of the constants."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthException"
  hidden: true
---

<a id="E2E_OAuth_OAuthException"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OAuthException : E2EException, ISerializable
```


Why a subscription login or a request made with it failed. [Code](/api/E2E/E2EException/#E2E_E2EException_Code) is one of the constants.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [E2EException](/api/E2E/E2EException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L11)

## Constructors

<a id="E2E_OAuth_OAuthException__ctor_"></a>
<a id="E2E_OAuth_OAuthException__ctor_System_String_System_String_"></a>

### OAuthException(string, string)


```csharp
public OAuthException(string code, string message)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L31)

<a id="E2E_OAuth_OAuthException__ctor_System_String_System_String_System_Exception_"></a>

### OAuthException(string, string, Exception)


```csharp
public OAuthException(string code, string message, Exception inner)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `code` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `inner` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L36)

## Fields

<a id="E2E_OAuth_OAuthException_Cancelled"></a>

### Cancelled


```csharp
public const string Cancelled = "CANCELLED"
```


The user or the caller cancelled the flow.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L23)

<a id="E2E_OAuth_OAuthException_FlowFailed"></a>

### FlowFailed


```csharp
public const string FlowFailed = "FLOW_FAILED"
```


The authorization server or the vendor API answered with an error during a flow.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L20)

<a id="E2E_OAuth_OAuthException_LoginRequired"></a>

### LoginRequired


```csharp
public const string LoginRequired = "LOGIN_REQUIRED"
```


The token or refresh token was rejected; the user signs in again.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L17)

<a id="E2E_OAuth_OAuthException_Misconfigured"></a>

### Misconfigured


```csharp
public const string Misconfigured = "MISCONFIGURED"
```


The flow needs an option the caller did not give (a client id, a CLI).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L29)

<a id="E2E_OAuth_OAuthException_NotLoggedIn"></a>

### NotLoggedIn


```csharp
public const string NotLoggedIn = "NOT_LOGGED_IN"
```


No stored credentials for the provider.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L14)

<a id="E2E_OAuth_OAuthException_Timeout"></a>

### Timeout


```csharp
public const string Timeout = "TIMEOUT"
```


The flow ran out of time waiting for the user.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthCredentials.cs#L26)
