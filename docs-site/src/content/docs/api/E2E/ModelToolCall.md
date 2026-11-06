---
title: "ModelToolCall"
description: "ModelToolCall in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelToolCall"
  hidden: true
---

<a id="E2E_ModelToolCall"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ModelToolCall
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Properties

<a id="E2E_ModelToolCall_Arguments_"></a>
<a id="E2E_ModelToolCall_Arguments"></a>

### Arguments


```csharp
public required JsonElement Arguments { get; init; }
```


**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_ModelToolCall_Id_"></a>
<a id="E2E_ModelToolCall_Id"></a>

### Id


```csharp
public required string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelToolCall_Name_"></a>
<a id="E2E_ModelToolCall_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelToolCall_ProviderMetadata_"></a>
<a id="E2E_ModelToolCall_ProviderMetadata"></a>

### ProviderMetadata


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }
```


What the provider attached to the call, keyed by provider (such as `google`: `{ "thoughtSignature": "..." }`), which the
call carries back on the next turn (upstream's `providerMetadata` of a tool call part). null when it attached none.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
