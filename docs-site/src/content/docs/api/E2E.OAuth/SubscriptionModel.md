---
title: "SubscriptionModel"
description: "One model a subscription serves: the id a config passes, and how the vendor describes it."
editUrl: false
prev: false
next: false
sidebar:
  label: "SubscriptionModel"
  hidden: true
---

<a id="E2E_OAuth_SubscriptionModel"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed record SubscriptionModel : IEquatable<SubscriptionModel>
```


One model a subscription serves: the id a config passes, and how the vendor describes it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SubscriptionModel&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L70)

## Constructors

<a id="E2E_OAuth_SubscriptionModel__ctor_"></a>
<a id="E2E_OAuth_SubscriptionModel__ctor_System_String_System_String_System_String_"></a>

### SubscriptionModel(string, string?, string?)


```csharp
public SubscriptionModel(string Id, string? Name = null, string? Detail = null)
```


One model a subscription serves: the id a config passes, and how the vendor describes it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Detail` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L70)

## Properties

<a id="E2E_OAuth_SubscriptionModel_Detail_"></a>
<a id="E2E_OAuth_SubscriptionModel_Detail"></a>

### Detail


```csharp
public string? Detail { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L70)

<a id="E2E_OAuth_SubscriptionModel_Id_"></a>
<a id="E2E_OAuth_SubscriptionModel_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L70)

<a id="E2E_OAuth_SubscriptionModel_Name_"></a>
<a id="E2E_OAuth_SubscriptionModel_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L70)
