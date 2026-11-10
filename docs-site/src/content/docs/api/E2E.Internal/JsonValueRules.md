---
title: "JsonValueRules"
description: "Upstream's JsonValueRules (internal/json-value.ts): what the JSON-safety walk refuses beyond the defaults."
editUrl: false
prev: false
next: false
sidebar:
  label: "JsonValueRules"
  hidden: true
---

<a id="E2E_Internal_JsonValueRules"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public sealed record JsonValueRules : IEquatable<JsonValueRules>
```


Upstream's `JsonValueRules` (`internal/json-value.ts`): what the JSON-safety walk refuses beyond the defaults.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;JsonValueRules&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/JsonValueValidation.cs)

## Properties

<a id="E2E_Internal_JsonValueRules_MaxDepth_"></a>
<a id="E2E_Internal_JsonValueRules_MaxDepth"></a>

### MaxDepth


```csharp
public int? MaxDepth { get; init; }
```


Maximum object or array nesting depth; unlimited when unset.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Internal_JsonValueRules_OnSecret_"></a>
<a id="E2E_Internal_JsonValueRules_OnSecret"></a>

### OnSecret


```csharp
public Action<string>? OnSecret { get; init; }
```


Raised on a [Secret](/api/E2E/Secret/) value, with the label of the value. The default throws `POLICY_DENIED`.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)
