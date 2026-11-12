---
title: "AppEvent"
description: "What the app did on its own, as the session passes it on: a log line, or where it went."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppEvent"
  hidden: true
---

<a id="E2E_Engine_AppEvent"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public abstract record AppEvent : IEquatable<AppEvent>
```


What the app did on its own, as the session passes it on: a log line, or where it went.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;AppEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [AppLogEvent](/api/E2E.Engine/AppLogEvent/), [AppNavigationEvent](/api/E2E.Engine/AppNavigationEvent/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)
