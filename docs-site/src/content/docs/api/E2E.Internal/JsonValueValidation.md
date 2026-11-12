---
title: "JsonValueValidation"
description: "The one JSON-safety walker (upstream's validateJsonValue in internal/json-value.ts). Anything crossing a boundary that demands plain JSON validates here, so..."
editUrl: false
prev: false
next: false
sidebar:
  label: "JsonValueValidation"
  hidden: true
---

<a id="E2E_Internal_JsonValueValidation"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public static class JsonValueValidation
```


The one JSON-safety walker (upstream's `validateJsonValue` in `internal/json-value.ts`). Anything crossing a boundary that demands
plain JSON validates here, so cycle detection, plain-container checks, and secret rejection cannot drift between call sites. .NET-only
holder name: C# has no free functions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/JsonValueValidation.cs)

## Methods

<a id="E2E_Internal_JsonValueValidation_ValidateJsonValue_"></a>
<a id="E2E_Internal_JsonValueValidation_ValidateJsonValue_System_Object_System_String_E2E_Internal_JsonValueRules_"></a>

### ValidateJsonValue(object?, string, JsonValueRules?)


```csharp
public static void ValidateJsonValue(object? value, string label, JsonValueRules? rules = null)
```


Validates that &lt;code class="paramref"&gt;value&lt;/code&gt; is JSON-safe, throwing a labeled error otherwise: a non-finite number, a cycle, a container
that is not a plain dictionary or list, a value of any other type, or a [Secret](/api/E2E/Secret/).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `rules` | [JsonValueRules](/api/E2E.Internal/JsonValueRules/) |  |
