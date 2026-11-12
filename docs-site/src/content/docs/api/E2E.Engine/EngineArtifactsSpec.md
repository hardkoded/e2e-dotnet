---
title: "EngineArtifactsSpec"
description: "Upstream's artifacts hooks of an engine: the screenshot, and video started and stopped together."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineArtifactsSpec"
  hidden: true
---

<a id="E2E_Engine_EngineArtifactsSpec"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineArtifactsSpec : IEquatable<EngineArtifactsSpec>
```


Upstream's `artifacts` hooks of an engine: the screenshot, and video started and stopped together.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineArtifactsSpec&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineArtifactsSpec_Screenshot_"></a>
<a id="E2E_Engine_EngineArtifactsSpec_Screenshot"></a>

### Screenshot


```csharp
public Func<string?, OperationContext, Task<string>>? Screenshot { get; init; }
```


**Value:** [Func&lt;string, OperationContext, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineArtifactsSpec_StartVideo_"></a>
<a id="E2E_Engine_EngineArtifactsSpec_StartVideo"></a>

### StartVideo


```csharp
public Func<OperationContext, Task>? StartVideo { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineArtifactsSpec_StopVideo_"></a>
<a id="E2E_Engine_EngineArtifactsSpec_StopVideo"></a>

### StopVideo


```csharp
public Func<OperationContext, Task<IReadOnlyList<VideoSegment>>>? StopVideo { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&lt;IReadOnlyList&lt;VideoSegment&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)
