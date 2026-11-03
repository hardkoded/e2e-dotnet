---
title: "SetupStep"
description: "What one setup step is about (upstream's SetupStep, run/events.ts). collect loads the test files; filter is one test filter, by its name; prepare is one targ..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SetupStep"
  hidden: true
---

<a id="E2E_Reports_SetupStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public abstract record SetupStep : IEquatable<SetupStep>
```


What one setup step is about (upstream's `SetupStep`, `run/events.ts`). `collect` loads the test files; `filter` is one test
filter, by its name; `prepare` is one target's engine provisioning; `service` and `app` are the processes the run owns, labelled
the way the runner names them (`service "postgres"`, `target "web" command`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [AppSetupStep](/api/E2E.Reports/AppSetupStep/), [CollectSetupStep](/api/E2E.Reports/CollectSetupStep/), [FilterSetupStep](/api/E2E.Reports/FilterSetupStep/), [PrepareSetupStep](/api/E2E.Reports/PrepareSetupStep/), [ServiceSetupStep](/api/E2E.Reports/ServiceSetupStep/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)
