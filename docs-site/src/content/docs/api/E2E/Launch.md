---
title: "Launch"
description: "launch from @e2e-dev/core: drive a browser or a device from plain code, with no test runner, config file, or report. A host is one worker on one engine insta..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Launch"
  hidden: true
---

<a id="E2E_Launch"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class Launch
```


`launch` from `@e2e-dev/core`: drive a browser or a device from plain code, with no test runner, config file, or report. A host is
one worker on one engine instance: it prepares the engine, initializes it, and opens sessions on it one at a time. Parallel work is more hosts.
Upstream's `core/launch.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Methods

<a id="E2E_Launch_LaunchAsync_"></a>
<a id="E2E_Launch_LaunchAsync_E2E_Engine_IEngine_E2E_LaunchOptions_System_Threading_CancellationToken_"></a>

### LaunchAsync(IEngine, LaunchOptions?, CancellationToken)


```csharp
public static Task<Host> LaunchAsync(IEngine engine, LaunchOptions? options = null, CancellationToken cancellationToken = default)
```


Launches &lt;code class="paramref"&gt;engine&lt;/code&gt;: prepares it (a first-run browser download, a device lease), initializes one worker, and returns the host that
opens sessions on it. Each launch needs its own engine instance.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [IEngine](/api/E2E.Engine/IEngine/) |  |
| `options` | [LaunchOptions](/api/E2E/LaunchOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;Host&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
