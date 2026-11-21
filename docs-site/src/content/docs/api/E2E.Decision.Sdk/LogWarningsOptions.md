---
title: "LogWarningsOptions"
description: "What the warning logger receives (the options of the AI SDK's LogWarningsFunction)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LogWarningsOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_LogWarningsOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record LogWarningsOptions : IEquatable<LogWarningsOptions>
```


What the warning logger receives (the options of the AI SDK's `LogWarningsFunction`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LogWarningsOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/LogWarnings.cs)

## Constructors

<a id="E2E_Decision_Sdk_LogWarningsOptions__ctor_"></a>
<a id="E2E_Decision_Sdk_LogWarningsOptions__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_"></a>

### `LogWarningsOptions(IReadOnlyList<string>, string?, string?)`


```csharp
public LogWarningsOptions(IReadOnlyList<string> Warnings, string? Provider, string? Model)
```


What the warning logger receives (the options of the AI SDK's `LogWarningsFunction`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Warnings` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The warnings the model provider returned. |
| `Provider` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The provider id used for the call. |
| `Model` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The model id used for the call. |

## Properties

<a id="E2E_Decision_Sdk_LogWarningsOptions_Model_"></a>
<a id="E2E_Decision_Sdk_LogWarningsOptions_Model"></a>

### Model


```csharp
public string? Model { get; init; }
```


The model id used for the call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LogWarningsOptions_Provider_"></a>
<a id="E2E_Decision_Sdk_LogWarningsOptions_Provider"></a>

### Provider


```csharp
public string? Provider { get; init; }
```


The provider id used for the call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_LogWarningsOptions_Warnings_"></a>
<a id="E2E_Decision_Sdk_LogWarningsOptions_Warnings"></a>

### Warnings


```csharp
public IReadOnlyList<string> Warnings { get; init; }
```


The warnings the model provider returned.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
