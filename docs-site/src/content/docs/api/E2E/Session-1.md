---
title: "Session<TFixtures>"
description: "A session on an engine that contributes fixtures of its own: Fixtures holds them, such as Device on a mobile engine. Upstream's Session<Fixtures> (core/launc..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Session<TFixtures>"
  hidden: true
---

<a id="E2E_Session_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Session<TFixtures> : Session, IAsyncDisposable where TFixtures : class
```


A session on an engine that contributes fixtures of its own: `Fixtures` holds them, such as `Device` on a mobile engine. Upstream's
`Session<Fixtures>` (`core/launch.ts`), the session type intersected with the engine's fixtures; C# has no intersection, so the
engine's fixtures are one member.

**Type parameters**

| Name | Description |
| --- | --- |
| `TFixtures` | What the engine contributes, as `IEngine&lt;T&gt;` declares. |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Session](/api/E2E/Session/)

**Implements:** [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_Session_1_Fixtures_"></a>
<a id="E2E_Session_1_Fixtures"></a>

### Fixtures


```csharp
public TFixtures Fixtures { get; }
```


What the engine contributes beside [Screen](/api/E2E/Session/#E2E_Session_Screen), [App](/api/E2E/Session/#E2E_Session_App), and [Platform](/api/E2E/Session/#E2E_Session_Platform).

**Value:** `TFixtures`
