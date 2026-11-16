---
title: "IEngine<TFixtures>"
description: "An engine that contributes fixtures to the sessions opened on it, which LaunchAsync<T> types a host and its sessions by. Upstream's EngineHandle<Fixtures> (d..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IEngine<TFixtures>"
  hidden: true
---

<a id="E2E_Engine_IEngine_1"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IEngine<out TFixtures> : IEngine where TFixtures : class
```


An engine that contributes fixtures to the sessions opened on it, which `LaunchAsync&lt;T&gt;` types a host and its sessions by.
Upstream's `EngineHandle<Fixtures>` (`defineEngine<Fixtures>`).

**Type parameters**

| Name | Description |
| --- | --- |
| `TFixtures` | What a session on this engine holds beside `Screen` and `App`, such as `Device` on a mobile engine. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Methods

<a id="E2E_Engine_IEngine_1_CreateFixtures_"></a>
<a id="E2E_Engine_IEngine_1_CreateFixtures_E2E_Engine_IEngineSession_"></a>

### CreateFixtures(IEngineSession)


```csharp
TFixtures CreateFixtures(IEngineSession session)
```


The fixtures of one session, over the engine's own &lt;code class="paramref"&gt;session&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `session` | [IEngineSession](/api/E2E.Engine/IEngineSession/) |  |

**Returns:** `TFixtures`
