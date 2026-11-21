---
title: "ITelemetryRedactable"
description: "A value that carries a provider-owned telemetry view (the AI SDK's withTelemetryRedaction, provider-utils/src/telemetry-redaction.ts). .NET-only name: upstre..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ITelemetryRedactable"
  hidden: true
---

<a id="E2E_Decision_Sdk_ITelemetryRedactable"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public interface ITelemetryRedactable
```


A value that carries a provider-owned telemetry view (the AI SDK's `withTelemetryRedaction`, `provider-utils/src/telemetry-redaction.ts`).
.NET-only name: upstream attaches the view to an object with a symbol; a .NET type implements this interface instead. The original value is
never changed.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TelemetryRedaction.cs)

## Methods

<a id="E2E_Decision_Sdk_ITelemetryRedactable_RedactForTelemetry_"></a>
<a id="E2E_Decision_Sdk_ITelemetryRedactable_RedactForTelemetry"></a>

### RedactForTelemetry()


```csharp
object RedactForTelemetry()
```


The copy of this value an integration receives, without credentials.

**Returns:** [object](https://learn.microsoft.com/dotnet/api/system.object)
