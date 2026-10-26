---
title: "AgentToolContext"
description: "What a project tool reaches of the step it runs in (upstream's ToolContext): a look at the screen, and a screenshot kept as evidence of the step. A tool that..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentToolContext"
  hidden: true
---

<a id="E2E_AgentToolContext"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentToolContext
```


What a project tool reaches of the step it runs in (upstream's `ToolContext`): a look at the screen, and a screenshot kept
as evidence of the step. A tool that changes the app may not look: it observes in a separate call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L95)

## Constructors

<a id="E2E_AgentToolContext__ctor_"></a>
<a id="E2E_AgentToolContext__ctor_E2E_StepExecutorContext_System_Boolean_"></a>

### AgentToolContext(StepExecutorContext, bool)


```csharp
public AgentToolContext(StepExecutorContext context, bool mutates)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [StepExecutorContext](/api/E2E/StepExecutorContext/) | The context of the step the tool runs in. |
| `mutates` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether the tool changes the app. Such a tool cannot look at the screen. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L102)

## Methods

<a id="E2E_AgentToolContext_AttachScreenshotAsync_"></a>
<a id="E2E_AgentToolContext_AttachScreenshotAsync_E2E_ExecutorPixels_System_String_System_Threading_CancellationToken_"></a>

### AttachScreenshotAsync(ExecutorPixels, string, CancellationToken)


```csharp
public Task<string> AttachScreenshotAsync(ExecutorPixels pixels, string label, CancellationToken cancellationToken = default)
```


Upstream's `attachScreenshot`: keeps pixels the tool saw as a screenshot of the step, and returns where it landed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pixels` | [ExecutorPixels](/api/E2E/ExecutorPixels/) |  |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L128)

<a id="E2E_AgentToolContext_ObserveAsync_"></a>
<a id="E2E_AgentToolContext_ObserveAsync_E2E_ExecutorObserveOptions_System_Threading_CancellationToken_"></a>

### ObserveAsync(ExecutorObserveOptions?, CancellationToken)


```csharp
public Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options = null, CancellationToken cancellationToken = default)
```


Upstream's `observe`. A mutating tool is refused with `POLICY_DENIED`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [ExecutorObserveOptions](/api/E2E/ExecutorObserveOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ExecutorObservation&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L117)
