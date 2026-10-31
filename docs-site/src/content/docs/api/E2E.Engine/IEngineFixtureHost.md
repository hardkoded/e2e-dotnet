---
title: "IEngineFixtureHost"
description: "An engine session that contributes a fixture, which the harness tells about the attempt when it starts. .NET-only: upstream's engine declares fixtures, facto..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngineFixtureHost"
  hidden: true
---

<a id="E2E_Engine_IEngineFixtureHost"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngineFixtureHost
```


An engine session that contributes a fixture, which the harness tells about the attempt when it starts. .NET-only: upstream's engine
declares `fixtures`, factories the harness calls with an [EngineFixtureContext](/api/E2E.Engine/EngineFixtureContext/); the port's session builds its own
fixture (`Device`), so this hands it the context.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L72)
