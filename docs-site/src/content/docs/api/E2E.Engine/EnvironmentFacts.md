---
title: "EnvironmentFacts"
description: "Upstream's EnvironmentFacts (engine/session.ts): what the engine said the attempt runs on, bounded to eight facts, with each name and value redacted before i..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EnvironmentFacts"
  hidden: true
---

<a id="E2E_Engine_EnvironmentFacts"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class EnvironmentFacts
```


Upstream's `EnvironmentFacts` (`engine/session.ts`): what the engine said the attempt runs on, bounded to eight facts, with each name
and value redacted before it is clipped.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Fields

<a id="E2E_Engine_EnvironmentFacts_MaxFacts"></a>

### MaxFacts


```csharp
public const int MaxFacts = 8
```


The most facts kept.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EnvironmentFacts_MaxKeyChars"></a>

### MaxKeyChars


```csharp
public const int MaxKeyChars = 40
```


The most characters of a fact's name, after redaction.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EnvironmentFacts_MaxValueChars"></a>

### MaxValueChars


```csharp
public const int MaxValueChars = 200
```


The most characters of a fact's value, after redaction.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_Engine_EnvironmentFacts_Push_"></a>
<a id="E2E_Engine_EnvironmentFacts_Push_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__"></a>

### `Push(IReadOnlyDictionary<string, string>)`


```csharp
public void Push(IReadOnlyDictionary<string, string> facts)
```


Merges facts in, checked: a blank name or value is dropped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `facts` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |

<a id="E2E_Engine_EnvironmentFacts_Read_"></a>
<a id="E2E_Engine_EnvironmentFacts_Read_System_Func_System_String_System_String__"></a>

### `Read(Func<string, string>)`


```csharp
public IReadOnlyDictionary<string, string>? Read(Func<string, string> redact)
```


The facts so far, each name and value redacted with &lt;code class="paramref"&gt;redact&lt;/code&gt;, then clipped; null when there are none.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `redact` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
