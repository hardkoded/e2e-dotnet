---
title: "EngineFixtureContext"
description: "What an engine's fixture gets from the attempt, once per attempt. Upstream's EngineFixtureContext, of which the port has locator only: its fixtures take thei..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineFixtureContext"
  hidden: true
---

<a id="E2E_Engine_EngineFixtureContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class EngineFixtureContext
```


What an engine's fixture gets from the attempt, once per attempt. Upstream's `EngineFixtureContext`, of which the port has
`locator` only: its fixtures take their own cancellation tokens, and record their steps through `RunOperationAsync`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Methods

<a id="E2E_Engine_EngineFixtureContext_Locator_"></a>
<a id="E2E_Engine_EngineFixtureContext_Locator_E2E_LocatorExpression_"></a>

### Locator(LocatorExpression)


```csharp
public Locator Locator(LocatorExpression expression)
```


Mints a public locator from a raw expression (a platform selector, say). Upstream's `locator`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expression` | [LocatorExpression](/api/E2E/LocatorExpression/) |  |

**Returns:** [Locator](/api/E2E/Locator/)

<a id="E2E_Engine_EngineFixtureContext_Operation_"></a>
<a id="E2E_Engine_EngineFixtureContext_Operation"></a>

### Operation()


```csharp
public OperationContext Operation()
```


The operation context of one fixture call, read fresh on each call so a fixture follows the harness from the test body into its
teardown: its [Timeout](/api/E2E.Engine/OperationContext/#E2E_Engine_OperationContext_Timeout) and [Signal](/api/E2E.Engine/OperationContext/#E2E_Engine_OperationContext_Signal) are the ones in force now. Upstream's
`operation()`. A context built without one (a bare fixture) is [Test](/api/E2E.Engine/OperationContext/#E2E_Engine_OperationContext_Test).

**Returns:** [OperationContext](/api/E2E.Engine/OperationContext/)
