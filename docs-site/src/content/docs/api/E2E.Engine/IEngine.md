---
title: "IEngine"
description: "The body of one target. Core knows this contract and never an engine's internals. Version is the engine version. It is not part of the replay cache key: a re..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngine"
  hidden: true
---

<a id="E2E_Engine_IEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngine
```


The body of one target. Core knows this contract and never an engine's internals.
[Version](#E2E_Engine_IEngine_Version) is the engine version. It is not part of the replay cache key:
a recording re-finds its nodes at replay.

**Derived:** [DocumentEngine](/api/E2E.Engine/DocumentEngine/), [WebEngine](/api/E2E.Engine/WebEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L145)
