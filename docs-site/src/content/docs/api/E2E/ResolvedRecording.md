---
title: "ResolvedRecording"
description: "One effective mode and where it came from. Upstream ResolvedRecording."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolvedRecording"
  hidden: true
---

<a id="E2E_ResolvedRecording"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ResolvedRecording : IEquatable<ResolvedRecording>
```


One effective mode and where it came from. Upstream `ResolvedRecording`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ResolvedRecording&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L53)

## Constructors

<a id="E2E_ResolvedRecording__ctor_"></a>
<a id="E2E_ResolvedRecording__ctor_E2E_RecordingMode_E2E_RecordingSource_"></a>

### ResolvedRecording(RecordingMode, RecordingSource)


```csharp
public ResolvedRecording(RecordingMode Mode, RecordingSource Source)
```


One effective mode and where it came from. Upstream `ResolvedRecording`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Mode` | [RecordingMode](/api/E2E/RecordingMode/) | Which attempts record. |
| `Source` | [RecordingSource](/api/E2E/RecordingSource/) | Where &lt;code class="paramref"&gt;Mode&lt;/code&gt; was set. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L53)

## Properties

<a id="E2E_ResolvedRecording_Mode_"></a>
<a id="E2E_ResolvedRecording_Mode"></a>

### Mode


```csharp
public RecordingMode Mode { get; init; }
```


Which attempts record.

**Value:** [RecordingMode](/api/E2E/RecordingMode/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L53)

<a id="E2E_ResolvedRecording_Source_"></a>
<a id="E2E_ResolvedRecording_Source"></a>

### Source


```csharp
public RecordingSource Source { get; init; }
```


Where &lt;code class="paramref"&gt;Mode&lt;/code&gt; was set.

**Value:** [RecordingSource](/api/E2E/RecordingSource/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/RecordingMode.cs#L53)
