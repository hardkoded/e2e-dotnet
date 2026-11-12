---
title: "EngineHandle"
description: "Upstream's engine handle (defineEngine's result, engine/manifest.ts): the validated engine with its capability set. A handle is only built by DefineEngine(En..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineHandle"
  hidden: true
---

<a id="E2E_Engine_EngineHandle"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class EngineHandle
```


Upstream's engine handle (`defineEngine`'s result, `engine/manifest.ts`): the validated engine with its capability set. A handle
is only built by [DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineHandle_Actions_"></a>
<a id="E2E_Engine_EngineHandle_Actions"></a>

### Actions


```csharp
public IReadOnlyList<string>? Actions { get; }
```


The action kinds [Perform](#E2E_Engine_EngineHandle_Perform) honors, or null without the actions capability.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineHandle_Artifacts_"></a>
<a id="E2E_Engine_EngineHandle_Artifacts"></a>

### Artifacts


```csharp
public EngineArtifactsSpec? Artifacts { get; }
```


**Value:** [EngineArtifactsSpec](/api/E2E.Engine/EngineArtifactsSpec/)

<a id="E2E_Engine_EngineHandle_Capabilities_"></a>
<a id="E2E_Engine_EngineHandle_Capabilities"></a>

### Capabilities


```csharp
public IReadOnlySet<string> Capabilities { get; }
```


The capabilities the spec declares: `observation`, `actions`, `location`, `pointer`, `keyboard`, `state`, `artifacts`, and each fixture name.

**Value:** [IReadOnlySet&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

<a id="E2E_Engine_EngineHandle_Dispose_"></a>
<a id="E2E_Engine_EngineHandle_Dispose"></a>

### Dispose


```csharp
public Func<EngineCleanupContext, Task>? Dispose { get; }
```


**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_Doctor_"></a>
<a id="E2E_Engine_EngineHandle_Doctor"></a>

### Doctor


```csharp
public Func<EngineDoctorInfo, Task<IReadOnlyList<EngineDoctorCheck>>>? Doctor { get; }
```


**Value:** [Func&lt;EngineDoctorInfo, Task&lt;IReadOnlyList&lt;EngineDoctorCheck&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_EndAttempt_"></a>
<a id="E2E_Engine_EngineHandle_EndAttempt"></a>

### EndAttempt


```csharp
public Func<EngineCleanupContext, Task>? EndAttempt { get; }
```


**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_Finish_"></a>
<a id="E2E_Engine_EngineHandle_Finish"></a>

### Finish


```csharp
public Func<EngineFinishInfo, Task>? Finish { get; }
```


**Value:** [Func&lt;EngineFinishInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_Fixtures_"></a>
<a id="E2E_Engine_EngineHandle_Fixtures"></a>

### Fixtures


```csharp
public IReadOnlyDictionary<string, Delegate>? Fixtures { get; }
```


**Value:** [IReadOnlyDictionary&lt;string, Delegate&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineHandle_Init_"></a>
<a id="E2E_Engine_EngineHandle_Init"></a>

### Init


```csharp
public Func<EngineInitInfo, Task>? Init { get; }
```


**Value:** [Func&lt;EngineInitInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_Keyboard_"></a>
<a id="E2E_Engine_EngineHandle_Keyboard"></a>

### Keyboard


```csharp
public EngineKeyboardSpec? Keyboard { get; }
```


**Value:** [EngineKeyboardSpec](/api/E2E.Engine/EngineKeyboardSpec/)

<a id="E2E_Engine_EngineHandle_Locate_"></a>
<a id="E2E_Engine_EngineHandle_Locate"></a>

### Locate


```csharp
public Func<LocatorExpression, OperationContext, Task<IReadOnlyList<EngineNode>>>? Locate { get; }
```


**Value:** [Func&lt;LocatorExpression, OperationContext, Task&lt;IReadOnlyList&lt;EngineNode&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineHandle_Name_"></a>
<a id="E2E_Engine_EngineHandle_Name"></a>

### Name


```csharp
public string Name { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineHandle_Observe_"></a>
<a id="E2E_Engine_EngineHandle_Observe"></a>

### Observe


```csharp
public Func<OperationContext, ObserveOptions?, Task<EngineSnapshot>>? Observe { get; }
```


**Value:** [Func&lt;OperationContext, ObserveOptions, Task&lt;EngineSnapshot&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineHandle_Perform_"></a>
<a id="E2E_Engine_EngineHandle_Perform"></a>

### Perform


```csharp
public Func<NodeRef, LocatorAction, OperationContext, Task>? Perform { get; }
```


**Value:** [Func&lt;NodeRef, LocatorAction, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-4)

<a id="E2E_Engine_EngineHandle_PerformAt_"></a>
<a id="E2E_Engine_EngineHandle_PerformAt"></a>

### PerformAt


```csharp
public Func<Point, PointerAction, OperationContext, Task>? PerformAt { get; }
```


**Value:** [Func&lt;Point, PointerAction, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-4)

<a id="E2E_Engine_EngineHandle_Platform_"></a>
<a id="E2E_Engine_EngineHandle_Platform"></a>

### Platform


```csharp
public string? Platform { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineHandle_PointerActions_"></a>
<a id="E2E_Engine_EngineHandle_PointerActions"></a>

### PointerActions


```csharp
public IReadOnlyList<string>? PointerActions { get; }
```


The pointer action kinds [PerformAt](#E2E_Engine_EngineHandle_PerformAt) honors, or null without the pointer capability.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineHandle_Prepare_"></a>
<a id="E2E_Engine_EngineHandle_Prepare"></a>

### Prepare


```csharp
public Func<EnginePrepareInfo, Task>? Prepare { get; }
```


**Value:** [Func&lt;EnginePrepareInfo, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_Secrets_"></a>
<a id="E2E_Engine_EngineHandle_Secrets"></a>

### Secrets


```csharp
public IReadOnlyList<Secret>? Secrets { get; }
```


The secrets the engine holds, each name once; null when the spec declares none.

**Value:** [IReadOnlyList&lt;Secret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineHandle_Session_"></a>
<a id="E2E_Engine_EngineHandle_Session"></a>

### Session


```csharp
public EngineSessionSpec? Session { get; }
```


**Value:** [EngineSessionSpec](/api/E2E.Engine/EngineSessionSpec/)

<a id="E2E_Engine_EngineHandle_SettleAttempt_"></a>
<a id="E2E_Engine_EngineHandle_SettleAttempt"></a>

### SettleAttempt


```csharp
public Func<EngineCleanupContext, Task>? SettleAttempt { get; }
```


**Value:** [Func&lt;EngineCleanupContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_SpiVersion_"></a>
<a id="E2E_Engine_EngineHandle_SpiVersion"></a>

### SpiVersion


```csharp
public int SpiVersion { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_EngineHandle_StartAttempt_"></a>
<a id="E2E_Engine_EngineHandle_StartAttempt"></a>

### StartAttempt


```csharp
public Func<OperationContext, Task>? StartAttempt { get; }
```


**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineHandle_State_"></a>
<a id="E2E_Engine_EngineHandle_State"></a>

### State


```csharp
public EngineStateSpec? State { get; }
```


**Value:** [EngineStateSpec](/api/E2E.Engine/EngineStateSpec/)

<a id="E2E_Engine_EngineHandle_TapModifiers_"></a>
<a id="E2E_Engine_EngineHandle_TapModifiers"></a>

### TapModifiers


```csharp
public bool? TapModifiers { get; }
```


Whether the tap actions take modifier keys; null when the spec does not declare it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineHandle_ValidateApp_"></a>
<a id="E2E_Engine_EngineHandle_ValidateApp"></a>

### ValidateApp


```csharp
public Action<EngineAppDeclaration, EngineAppCheckInfo>? ValidateApp { get; }
```


**Value:** [Action&lt;EngineAppDeclaration, EngineAppCheckInfo&gt;](https://learn.microsoft.com/dotnet/api/system.action-2)

<a id="E2E_Engine_EngineHandle_Version_"></a>
<a id="E2E_Engine_EngineHandle_Version"></a>

### Version


```csharp
public string Version { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineHandle_Workers_"></a>
<a id="E2E_Engine_EngineHandle_Workers"></a>

### Workers


```csharp
public int? Workers { get; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
