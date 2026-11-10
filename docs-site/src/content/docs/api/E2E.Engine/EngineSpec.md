---
title: "EngineSpec"
description: "Upstream's Engine (engine/index.ts): what an engine author declares, handed to DefineEngine(EngineSpec). Every member is optional; DefineEngine(EngineSpec) v..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineSpec"
  hidden: true
---

<a id="E2E_Engine_EngineSpec"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineSpec : IEquatable<EngineSpec>
```


Upstream's `Engine` (`engine/index.ts`): what an engine author declares, handed to [DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_).
Every member is optional; [DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_) validates the combination and computes the capability set.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineSpec&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineSpec_Actions_"></a>
<a id="E2E_Engine_EngineSpec_Actions"></a>

### Actions


```csharp
public IReadOnlyList<string>? Actions { get; init; }
```


The action kinds [Perform](#E2E_Engine_EngineSpec_Perform) honors, each once; required with [Perform](#E2E_Engine_EngineSpec_Perform).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineSpec_Artifacts_"></a>
<a id="E2E_Engine_EngineSpec_Artifacts"></a>

### Artifacts


```csharp
public EngineArtifactsSpec? Artifacts { get; init; }
```


The app's artifact hooks.

**Value:** [EngineArtifactsSpec](/api/E2E.Engine/EngineArtifactsSpec/)

<a id="E2E_Engine_EngineSpec_Dispose_"></a>
<a id="E2E_Engine_EngineSpec_Dispose"></a>

### Dispose


```csharp
public Func<EngineCleanupContext, Task>? Dispose { get; init; }
```


Disposes the engine's worker.

**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_Doctor_"></a>
<a id="E2E_Engine_EngineSpec_Doctor"></a>

### Doctor


```csharp
public Func<EngineDoctorInfo, Task<IReadOnlyList<EngineDoctorCheck>>>? Doctor { get; init; }
```


Reports the engine's readiness checks.

**Value:** [Func&lt;EngineDoctorInfo, Task&lt;IReadOnlyList&lt;EngineDoctorCheck&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_EndAttempt_"></a>
<a id="E2E_Engine_EngineSpec_EndAttempt"></a>

### EndAttempt


```csharp
public Func<EngineCleanupContext, Task>? EndAttempt { get; init; }
```


Ends an attempt's isolation, within the cleanup budget.

**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_Finish_"></a>
<a id="E2E_Engine_EngineSpec_Finish"></a>

### Finish


```csharp
public Func<EngineFinishInfo, Task>? Finish { get; init; }
```


Finishes a run.

**Value:** [Func&lt;EngineFinishInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_Fixtures_"></a>
<a id="E2E_Engine_EngineSpec_Fixtures"></a>

### Fixtures


```csharp
public IReadOnlyDictionary<string, Delegate>? Fixtures { get; init; }
```


Fixture factories, by name. A name is lower-camel and not one a universal fixture uses (`agent`, `app`, `screen`,
`platform`, `session`).

**Value:** [IReadOnlyDictionary&lt;string, Delegate&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineSpec_Init_"></a>
<a id="E2E_Engine_EngineSpec_Init"></a>

### Init


```csharp
public Func<EngineInitInfo, Task>? Init { get; init; }
```


Initializes a worker.

**Value:** [Func&lt;EngineInitInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_Keyboard_"></a>
<a id="E2E_Engine_EngineSpec_Keyboard"></a>

### Keyboard


```csharp
public EngineKeyboardSpec? Keyboard { get; init; }
```


Input to the focused field; needs [Observe](#E2E_Engine_EngineSpec_Observe).

**Value:** [EngineKeyboardSpec](/api/E2E.Engine/EngineKeyboardSpec/)

<a id="E2E_Engine_EngineSpec_Locate_"></a>
<a id="E2E_Engine_EngineSpec_Locate"></a>

### Locate


```csharp
public Func<LocatorExpression, OperationContext, Task<IReadOnlyList<EngineNode>>>? Locate { get; init; }
```


Resolves a query against the surface, returning the nodes it matches.

**Value:** [Func&lt;LocatorExpression, OperationContext, Task&lt;IReadOnlyList&lt;EngineNode&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineSpec_Name_"></a>
<a id="E2E_Engine_EngineSpec_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


The engine's name, shown in reports and in error messages.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineSpec_Observe_"></a>
<a id="E2E_Engine_EngineSpec_Observe"></a>

### Observe


```csharp
public Func<OperationContext, ObserveOptions?, Task<EngineSnapshot>>? Observe { get; init; }
```


Takes one fresh observation of the surface under test.

**Value:** [Func&lt;OperationContext, ObserveOptions, Task&lt;EngineSnapshot&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineSpec_Perform_"></a>
<a id="E2E_Engine_EngineSpec_Perform"></a>

### Perform


```csharp
public Func<NodeRef, LocatorAction, OperationContext, Task>? Perform { get; init; }
```


Performs one locator action on a node of the observation or of a resolution.

**Value:** [Func&lt;NodeRef, LocatorAction, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-4)

<a id="E2E_Engine_EngineSpec_PerformAt_"></a>
<a id="E2E_Engine_EngineSpec_PerformAt"></a>

### PerformAt


```csharp
public Func<Point, PointerAction, OperationContext, Task>? PerformAt { get; init; }
```


Performs one pointer action at a viewport point, with no node behind it.

**Value:** [Func&lt;Point, PointerAction, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-4)

<a id="E2E_Engine_EngineSpec_Platform_"></a>
<a id="E2E_Engine_EngineSpec_Platform"></a>

### Platform


```csharp
public string? Platform { get; init; }
```


The platform the engine drives, when it has one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineSpec_PointerActions_"></a>
<a id="E2E_Engine_EngineSpec_PointerActions"></a>

### PointerActions


```csharp
public IReadOnlyList<string>? PointerActions { get; init; }
```


The pointer action kinds [PerformAt](#E2E_Engine_EngineSpec_PerformAt) honors, each once; required with [PerformAt](#E2E_Engine_EngineSpec_PerformAt).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineSpec_Prepare_"></a>
<a id="E2E_Engine_EngineSpec_Prepare"></a>

### Prepare


```csharp
public Func<EnginePrepareInfo, Task>? Prepare { get; init; }
```


Prepares the engine for a run.

**Value:** [Func&lt;EnginePrepareInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_Secrets_"></a>
<a id="E2E_Engine_EngineSpec_Secrets"></a>

### Secrets


```csharp
public IReadOnlyList<Secret>? Secrets { get; init; }
```


The secrets the engine's options hold, as handles; each name is kept once.

**Value:** [IReadOnlyList&lt;Secret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineSpec_Session_"></a>
<a id="E2E_Engine_EngineSpec_Session"></a>

### Session


```csharp
public EngineSessionSpec? Session { get; init; }
```


The app's session hooks.

**Value:** [EngineSessionSpec](/api/E2E.Engine/EngineSessionSpec/)

<a id="E2E_Engine_EngineSpec_SettleAttempt_"></a>
<a id="E2E_Engine_EngineSpec_SettleAttempt"></a>

### SettleAttempt


```csharp
public Func<EngineCleanupContext, Task>? SettleAttempt { get; init; }
```


Settles an attempt, rethrowing a failure no step awaited.

**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_SpiVersion_"></a>
<a id="E2E_Engine_EngineSpec_SpiVersion"></a>

### SpiVersion


```csharp
public int? SpiVersion { get; init; }
```


The engine contract version the engine was written against; this runner supports [SpiVersion](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_SpiVersion).

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineSpec_StartAttempt_"></a>
<a id="E2E_Engine_EngineSpec_StartAttempt"></a>

### StartAttempt


```csharp
public Func<OperationContext, Task>? StartAttempt { get; init; }
```


Starts an attempt on the engine.

**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSpec_State_"></a>
<a id="E2E_Engine_EngineSpec_State"></a>

### State


```csharp
public EngineStateSpec? State { get; init; }
```


The app's state hooks.

**Value:** [EngineStateSpec](/api/E2E.Engine/EngineStateSpec/)

<a id="E2E_Engine_EngineSpec_TapModifiers_"></a>
<a id="E2E_Engine_EngineSpec_TapModifiers"></a>

### TapModifiers


```csharp
public bool? TapModifiers { get; init; }
```


Whether the tap actions accept modifier keys; only with [Perform](#E2E_Engine_EngineSpec_Perform).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineSpec_ValidateApp_"></a>
<a id="E2E_Engine_EngineSpec_ValidateApp"></a>

### ValidateApp


```csharp
public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp { get; init; }
```


Checks the app a target declares against what the engine can drive, at config load.

**Value:** [Action&lt;EngineAppDeclaration, EngineAppCheckInfo&gt;](https://learn.microsoft.com/dotnet/api/system.action-2)

<a id="E2E_Engine_EngineSpec_Version_"></a>
<a id="E2E_Engine_EngineSpec_Version"></a>

### Version


```csharp
public string? Version { get; init; }
```


The engine's version, recorded in reports and used to key saved sessions.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineSpec_Workers_"></a>
<a id="E2E_Engine_EngineSpec_Workers"></a>

### Workers


```csharp
public int? Workers { get; init; }
```


The most workers the engine serves per target.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
