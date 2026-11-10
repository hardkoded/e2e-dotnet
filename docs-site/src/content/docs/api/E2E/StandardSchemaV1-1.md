---
title: "StandardSchemaV1<TOutput>"
description: ".NET-only stand-in for a Standard Schema v1 (Zod, Valibot, ArkType, ...): validates a value synchronously and returns the output or the issues. Adapt a schem..."
editUrl: false
prev: false
next: false
sidebar:
  label: "StandardSchemaV1<TOutput>"
  hidden: true
---

<a id="E2E_StandardSchemaV1_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public delegate StandardSchemaV1Result<TOutput> StandardSchemaV1<TOutput>(object? value)
```


.NET-only stand-in for a Standard Schema v1 (Zod, Valibot, ArkType, ...): validates a value synchronously and returns the output or the
issues. Adapt a schema library with a lambda. A schema that validates asynchronously cannot be passed, because the return type is
synchronous.

**Type parameters**

| Name | Description |
| --- | --- |
| `TOutput` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [object](https://learn.microsoft.com/dotnet/api/system.object) | The value under test, as `ValueExpectation&lt;T&gt;` holds it. |

**Returns:** `StandardSchemaV1Result&lt;TOutput&gt;`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Values.cs)
