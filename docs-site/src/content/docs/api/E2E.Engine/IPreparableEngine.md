---
title: "IPreparableEngine"
description: "An engine with upstream's optional prepare and finish hooks (declared in engine/index.ts): what a run provisions once per target before any worker starts, su..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IPreparableEngine"
  hidden: true
---

<a id="E2E_Engine_IPreparableEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IPreparableEngine : IEngine
```


An engine with upstream's optional `prepare` and `finish` hooks (declared in `engine/index.ts`): what a run provisions
once per target before any worker starts, such as a hosted device, and gives back after the last worker ended, so the provisioning
happens once per run instead of per worker. The port has no test runner that calls them: a host that starts the engine on its own, as
`e2e explore` and `e2e mcp` do, calls them around the one attempt (upstream's `openStandaloneAttempt`), prepare when
the session opens and finish when it closes, on every path; the mobile adapters' `MobileAppTest` runs them once per test process.
.NET-only name: upstream tests the engine handle for the members. [WebEngine](/api/E2E.Engine/WebEngine/) does not implement it, because it leases
for the attempt it starts.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IPreparableEngine.cs#L16)
