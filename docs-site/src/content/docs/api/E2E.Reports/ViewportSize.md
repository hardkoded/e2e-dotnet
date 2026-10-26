---
title: "ViewportSize"
description: "The size of a viewport in pixels (upstream's inline FailureEvidence['viewport'])."
editUrl: false
prev: false
next: false
sidebar:
  label: "ViewportSize"
  hidden: true
---

<a id="E2E_Reports_ViewportSize"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ViewportSize : IEquatable<ViewportSize>
```


The size of a viewport in pixels (upstream's inline `FailureEvidence['viewport']`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ViewportSize&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L410)

## Properties

<a id="E2E_Reports_ViewportSize_Height_"></a>
<a id="E2E_Reports_ViewportSize_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L414)

<a id="E2E_Reports_ViewportSize_Width_"></a>
<a id="E2E_Reports_ViewportSize_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L412)
