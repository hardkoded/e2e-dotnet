---
title: "GatewayAuthToken"
description: "The token and method GetGatewayAuthTokenAsync gives."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayAuthToken"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayAuthToken : IEquatable<GatewayAuthToken>
```


The token and method `GetGatewayAuthTokenAsync` gives.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayAuthToken&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayProvider.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken__ctor_System_String_System_String_"></a>

### GatewayAuthToken(string, string)


```csharp
public GatewayAuthToken(string Token, string AuthMethod)
```


The token and method `GetGatewayAuthTokenAsync` gives.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Token` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `AuthMethod` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken_AuthMethod_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken_AuthMethod"></a>

### AuthMethod


```csharp
public string AuthMethod { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken_Token_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthToken_Token"></a>

### Token


```csharp
public string Token { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
