---
title: "TelemetryErrorEvent"
description: "The event a telemetry integration's OnErrorAsync(TelemetryErrorEvent) receives when the operation fails."
editUrl: false
prev: false
next: false
sidebar:
  label: "TelemetryErrorEvent"
  hidden: true
---

<a id="E2E_Decision_Sdk_TelemetryErrorEvent"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record TelemetryErrorEvent : TelemetryEvent, IEquatable<TelemetryEvent>, IEquatable<TelemetryErrorEvent>
```


The event a telemetry integration's [OnErrorAsync(TelemetryErrorEvent)](/api/E2E.Decision.Sdk/ITelemetry/#E2E_Decision_Sdk_ITelemetry_OnErrorAsync_E2E_Decision_Sdk_TelemetryErrorEvent_) receives when the operation fails.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [TelemetryEvent](/api/E2E.Decision.Sdk/TelemetryEvent/)

**Implements:** [IEquatable&lt;TelemetryEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;TelemetryErrorEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecideEvents.cs)

## Properties

<a id="E2E_Decision_Sdk_TelemetryErrorEvent_CallId_"></a>
<a id="E2E_Decision_Sdk_TelemetryErrorEvent_CallId"></a>

### CallId


```csharp
public required string CallId { get; init; }
```


Unique identifier for the decision call.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TelemetryErrorEvent_Error_"></a>
<a id="E2E_Decision_Sdk_TelemetryErrorEvent_Error"></a>

### Error


```csharp
public required Exception Error { get; init; }
```


The error the call failed with.

**Value:** [Exception](https://learn.microsoft.com/dotnet/api/system.exception)
