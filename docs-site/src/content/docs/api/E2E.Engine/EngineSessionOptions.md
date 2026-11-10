---
title: "EngineSessionOptions"
description: "Upstream's createEngineSession options (engine/session.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineSessionOptions"
  hidden: true
---

<a id="E2E_Engine_EngineSessionOptions"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineSessionOptions : IEquatable<EngineSessionOptions>
```


Upstream's `createEngineSession` options (`engine/session.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineSessionOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Session.cs)

## Properties

<a id="E2E_Engine_EngineSessionOptions_Engine_"></a>
<a id="E2E_Engine_EngineSessionOptions_Engine"></a>

### Engine


```csharp
public EngineHandle? Engine { get; init; }
```


The engine handle, or null for a target that declares none.

**Value:** [EngineHandle](/api/E2E.Engine/EngineHandle/)

<a id="E2E_Engine_EngineSessionOptions_TargetName_"></a>
<a id="E2E_Engine_EngineSessionOptions_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target's name, for error messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineSessionOptions_Traced_"></a>
<a id="E2E_Engine_EngineSessionOptions_Traced"></a>

### Traced


```csharp
public bool Traced { get; init; }
```


Whether the attempt keeps a trace; without one, no screen is taken for it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
