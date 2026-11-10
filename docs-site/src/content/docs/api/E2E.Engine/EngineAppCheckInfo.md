---
title: "EngineAppCheckInfo"
description: "Handed to ValidateApp beside the declaration (upstream's EngineAppCheckInfo)."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineAppCheckInfo"
  hidden: true
---

<a id="E2E_Engine_EngineAppCheckInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineAppCheckInfo : IEquatable<EngineAppCheckInfo>
```


Handed to `ValidateApp` beside the declaration (upstream's `EngineAppCheckInfo`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineAppCheckInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_EngineAppCheckInfo_TargetName_"></a>
<a id="E2E_Engine_EngineAppCheckInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target declaring the app, for the message of a refusal.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
