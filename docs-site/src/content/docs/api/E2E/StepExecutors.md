---
title: "StepExecutors"
description: "Upstream's RUNTIMECODES, BLOCKABLECODES and isRuntimeHardStop (agent/executor.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepExecutors"
  hidden: true
---

<a id="E2E_StepExecutors"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class StepExecutors
```


Upstream's `RUNTIME_CODES`, `BLOCKABLE_CODES` and `isRuntimeHardStop` (`agent/executor.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L559)

## Properties

<a id="E2E_StepExecutors_BlockableCodes_"></a>
<a id="E2E_StepExecutors_BlockableCodes"></a>

### BlockableCodes


```csharp
public static IReadOnlySet<string> BlockableCodes { get; }
```


The codes a [Blocked](/api/E2E/StepVerdictStatus/#E2E_StepVerdictStatus_Blocked) verdict may carry: credentials, the environment, seed data, and test
setup have outside owners, and `automation` means the executor ran out of room. Every other code describes the app
and belongs to a failed verdict.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L572)

<a id="E2E_StepExecutors_RuntimeCodes_"></a>
<a id="E2E_StepExecutors_RuntimeCodes"></a>

### RuntimeCodes


```csharp
public static IReadOnlySet<string> RuntimeCodes { get; }
```


The codes only the runtime may assign. An executor can carry one it was given, in a hard-stop error the context raised,
but cannot invent one: the runtime rejects a runtime code it did not record.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L565)

## Methods

<a id="E2E_StepExecutors_IsRuntimeHardStop_"></a>
<a id="E2E_StepExecutors_IsRuntimeHardStop_E2E_E2EException_"></a>

### IsRuntimeHardStop(E2EException)


```csharp
public static bool IsRuntimeHardStop(E2EException error)
```


Whether an error is one of the runtime's own hard stops. Such an error is the step's truth wherever it lands, inside a
tool call or a replayed action, and is surfaced untouched rather than absorbed as a divergence or an action failure.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [E2EException](/api/E2E/E2EException/) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L615)
