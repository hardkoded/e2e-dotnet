---
title: "ISessionArtifacts"
description: "Upstream's SessionArtifacts: the screenshot, and video started and stopped together when the engine declares them."
editUrl: false
prev: false
next: false
sidebar:
  label: "ISessionArtifacts"
  hidden: true
---

<a id="E2E_Engine_ISessionArtifacts"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface ISessionArtifacts
```


Upstream's `SessionArtifacts`: the screenshot, and video started and stopped together when the engine declares them.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Properties

<a id="E2E_Engine_ISessionArtifacts_StartVideo_"></a>
<a id="E2E_Engine_ISessionArtifacts_StartVideo"></a>

### StartVideo


```csharp
Func<OperationContext, Task>? StartVideo { get; }
```


Starts video recording; null without the video hooks.

**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_ISessionArtifacts_StopVideo_"></a>
<a id="E2E_Engine_ISessionArtifacts_StopVideo"></a>

### StopVideo


```csharp
Func<OperationContext, Task<IReadOnlyList<VideoSegment>>>? StopVideo { get; }
```


Stops video recording and returns the segments written, in order; null without the video hooks.

**Value:** [Func&lt;OperationContext, Task&lt;IReadOnlyList&lt;VideoSegment&gt;&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

## Methods

<a id="E2E_Engine_ISessionArtifacts_ScreenshotAsync_"></a>
<a id="E2E_Engine_ISessionArtifacts_ScreenshotAsync_System_String_E2E_Engine_OperationContext_"></a>

### ScreenshotAsync(string?, OperationContext)


```csharp
Task<string> ScreenshotAsync(string? label, OperationContext operation)
```


Captures a redacted screenshot and returns an artifact-relative path.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `operation` | [OperationContext](/api/E2E.Engine/OperationContext/) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
