---
title: "NoSuchProviderException"
description: "The AI SDK's NoSuchProviderError: the registry has no provider with that id."
editUrl: false
prev: false
next: false
sidebar:
  label: "NoSuchProviderException"
  hidden: true
---

<a id="E2E_Decision_Sdk_NoSuchProviderException"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class NoSuchProviderException : NoSuchModelException, ISerializable
```


The AI SDK's `NoSuchProviderError`: the registry has no provider with that id.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [NoSuchModelException](/api/E2E.Decision.Sdk/NoSuchModelException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Errors.cs)

## Constructors

<a id="E2E_Decision_Sdk_NoSuchProviderException__ctor_"></a>
<a id="E2E_Decision_Sdk_NoSuchProviderException__ctor_System_String_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `NoSuchProviderException(string, string, string, IReadOnlyList<string>)`


```csharp
public NoSuchProviderException(string modelId, string modelType, string providerId, IReadOnlyList<string> availableProviders)
```


The AI SDK's `NoSuchProviderError`: the registry has no provider with that id.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modelType` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `providerId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `availableProviders` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_Sdk_NoSuchProviderException_AvailableProviders_"></a>
<a id="E2E_Decision_Sdk_NoSuchProviderException_AvailableProviders"></a>

### AvailableProviders


```csharp
public IReadOnlyList<string> AvailableProviders { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_NoSuchProviderException_ProviderId_"></a>
<a id="E2E_Decision_Sdk_NoSuchProviderException_ProviderId"></a>

### ProviderId


```csharp
public string ProviderId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
