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
