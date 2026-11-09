---
title: "IDoctorEngine"
description: "An engine with upstream's optional doctor hook (declared in engine/index.ts): what e2e doctor asks once per target, instead of starting a run. Without it the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IDoctorEngine"
  hidden: true
---

<a id="E2E_Engine_IDoctorEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IDoctorEngine : IEngine
```


An engine with upstream's optional `doctor` hook (declared in `engine/index.ts`): what `e2e doctor` asks once per
target, instead of starting a run. Without it the target gets one `engine` check with status `skip`.
.NET-only name: upstream's `Engine` declares the member, and the port keeps its optional hooks in their own interfaces
([IPreparableEngine](/api/E2E.Engine/IPreparableEngine/)), so an engine without the hook compiles unchanged.

**Derived:** [WebEngine](/api/E2E.Engine/WebEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IDoctorEngine.cs)

## Methods

<a id="E2E_Engine_IDoctorEngine_DoctorAsync_"></a>
<a id="E2E_Engine_IDoctorEngine_DoctorAsync_E2E_Engine_EngineDoctorInfo_System_Threading_CancellationToken_"></a>

### DoctorAsync(EngineDoctorInfo, CancellationToken)


```csharp
Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken)
```


Checks this machine for what a run on the target needs. Read-only and quick: never install, launch, lease, or start anything a
check could instead look up, and never call `PrepareAsync`. A check that cannot tell reports nothing
rather than guess. A throw is reported as one failed `engine` check of the target. `e2e doctor` gives the call 30 seconds
and cancels &lt;code class="paramref"&gt;cancellationToken&lt;/code&gt; then, or when it is interrupted; return within 15 seconds of it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EngineDoctorInfo](/api/E2E.Engine/EngineDoctorInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;EngineDoctorCheck&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
