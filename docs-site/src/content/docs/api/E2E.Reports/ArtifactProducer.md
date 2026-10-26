---
title: "ArtifactProducer"
description: "Who made an artifact (upstream's ArtifactRecord['producer'])."
editUrl: false
prev: false
next: false
sidebar:
  label: "ArtifactProducer"
  hidden: true
---

<a id="E2E_Reports_ArtifactProducer"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ArtifactProducer : IEquatable<ArtifactProducer>
```


Who made an artifact (upstream's `ArtifactRecord['producer']`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ArtifactProducer&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L442)

## Properties

<a id="E2E_Reports_ArtifactProducer_Kind_"></a>
<a id="E2E_Reports_ArtifactProducer_Kind"></a>

### Kind


```csharp
public ArtifactProducerKind Kind { get; init; }
```


**Value:** [ArtifactProducerKind](/api/E2E.Reports/ArtifactProducerKind/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L444)

<a id="E2E_Reports_ArtifactProducer_StepId_"></a>
<a id="E2E_Reports_ArtifactProducer_StepId"></a>

### StepId


```csharp
public string? StepId { get; init; }
```


The step's id, when a step made it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L447)
