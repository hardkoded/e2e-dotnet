---
title: "Picks"
description: "Upstream's picks.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "Picks"
  hidden: true
---

<a id="E2E_Decision_Picks"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public static class Picks
```


Upstream's `picks.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Picks.cs)

## Methods

<a id="E2E_Decision_Picks_Describe_"></a>
<a id="E2E_Decision_Picks_Describe_E2E_Decision_Decision_"></a>

### Describe(Decision)


```csharp
public static string Describe(Decision decision)
```


Upstream's `describe`. How an answer reads in a verdict or a turn: `p=0.950, confidence 0.930`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `decision` | [Decision](/api/E2E.Decision/Decision/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Picks_Gated_"></a>
<a id="E2E_Decision_Picks_Gated_E2E_Decision_Decision_E2E_Decision_Gates_"></a>

### Gated(Decision, Gates)


```csharp
public static bool Gated(Decision decision, Gates gates)
```


Upstream's `gated`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `decision` | [Decision](/api/E2E.Decision/Decision/) |  |
| `gates` | [Gates](/api/E2E.Decision/Gates/) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Picks_Invalid_"></a>
<a id="E2E_Decision_Picks_Invalid_System_String_"></a>

### Invalid(string)


```csharp
public static AgentException Invalid(string message)
```


Upstream's `invalid`. A model answer the request never offered: our validation, never the provider's text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [AgentException](/api/E2E/AgentException/)

<a id="E2E_Decision_Picks_Need_"></a>
<a id="E2E_Decision_Picks_Need_E2E_Decision_Decision_System_String_"></a>

### Need(Decision?, string)


```csharp
public static Decision Need(Decision? answer, string what)
```


Upstream's `need`. Requires an answer the model returned; its absence is our bug or a broken transport.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `answer` | [Decision](/api/E2E.Decision/Decision/) |  |
| `what` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Decision](/api/E2E.Decision/Decision/)

<a id="E2E_Decision_Picks_Pick_"></a>
<a id="E2E_Decision_Picks_Pick__1_E2E_Decision_Decision_System_Collections_Generic_IReadOnlyDictionary_System_String___0__E2E_Decision_Gates_System_String_System_String_"></a>

### `Pick<T>(Decision?, IReadOnlyDictionary<string, T>, Gates, string, string)`


```csharp
public static Chosen<T> Pick<T>(Decision? answer, IReadOnlyDictionary<string, T> offered, Gates gates, string what, string operation)
```


Upstream's `pick`. Resolves a target answer against what was offered. A lone offer needs no answer: the request never asked. The
model's `none`, and an answer under the gates, block the step. Anything the request never offered is `MODEL_OUTPUT_INVALID`:
our validation, never the provider's text.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `answer` | [Decision](/api/E2E.Decision/Decision/) |  |
| `offered` | [IReadOnlyDictionary&lt;string, T&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `gates` | [Gates](/api/E2E.Decision/Gates/) |  |
| `what` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `operation` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** `Chosen&lt;T&gt;`

<a id="E2E_Decision_Picks_VerdictOf_"></a>
<a id="E2E_Decision_Picks_VerdictOf_E2E_Decision_Decision_E2E_Decision_Gates_"></a>

### VerdictOf(Decision?, Gates)


```csharp
public static Verdict VerdictOf(Decision? answer, Gates gates)
```


Upstream's `verdictOf`. The verdict answer, typed, or [BelowGate](/api/E2E.Decision/Verdict/#E2E_Decision_Verdict_BelowGate) when it fails the gates; a verdict never offered is `MODEL_OUTPUT_INVALID`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `answer` | [Decision](/api/E2E.Decision/Decision/) |  |
| `gates` | [Gates](/api/E2E.Decision/Gates/) |  |

**Returns:** [Verdict](/api/E2E.Decision/Verdict/)
