---
title: "EnginePrepareResult"
description: "What prepare learned that the run must honour. Upstream's EnginePrepareResult."
editUrl: false
prev: false
next: false
sidebar:
  label: "EnginePrepareResult"
  hidden: true
---

<a id="E2E_Engine_EnginePrepareResult"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EnginePrepareResult : IEquatable<EnginePrepareResult>
```


What prepare learned that the run must honour. Upstream's `EnginePrepareResult`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EnginePrepareResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L335)

## Properties

<a id="E2E_Engine_EnginePrepareResult_Env_"></a>
<a id="E2E_Engine_EnginePrepareResult_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string> Env { get; init; }
```


Variables every worker of this target is started with, on top of the run's environment: how prepare hands what it provisioned
(a device binding) to each worker's start, in [Env](/api/E2E.Engine/EngineStartOptions/#E2E_Engine_EngineStartOptions_Env). The run's own variables win on a clash.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L349)

<a id="E2E_Engine_EnginePrepareResult_Workers_"></a>
<a id="E2E_Engine_EnginePrepareResult_Workers"></a>

### Workers


```csharp
public int? Workers { get; init; }
```


Upstream's contract: lowers the target's worker cap for this run, `1` to [Slots](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_Slots): the surfaces the engine actually
provisioned, one per leased resource. Null when prepare bound nothing, so the declared cap stays. The standalone host of
`e2e explore` and `e2e mcp` starts one worker and reads no cap. The port has no test runner to apply it, and no host in
this repository reads it.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L343)
