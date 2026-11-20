---
title: "TelemetryOptions"
description: "Telemetry configuration (the AI SDK's TelemetryOptions, telemetry/telemetry-options.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "TelemetryOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_TelemetryOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class TelemetryOptions
```


Telemetry configuration (the AI SDK's `TelemetryOptions`, `telemetry/telemetry-options.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TelemetryOptions.cs)

## Properties

<a id="E2E_Decision_Sdk_TelemetryOptions_FunctionId_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_FunctionId"></a>

### FunctionId


```csharp
public string? FunctionId { get; init; }
```


Identifier for this function. Used to group telemetry data by function.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_TelemetryOptions_IncludeRuntimeContext_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_IncludeRuntimeContext"></a>

### IncludeRuntimeContext


```csharp
public IReadOnlyDictionary<string, bool>? IncludeRuntimeContext { get; init; }
```


Top-level runtime context properties that should be included in telemetry. Runtime context properties are excluded unless they are
explicitly set to `true`.

**Value:** [IReadOnlyDictionary&lt;string, bool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Decision_Sdk_TelemetryOptions_Integrations_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_Integrations"></a>

### Integrations


```csharp
public IReadOnlyList<ITelemetry>? Integrations { get; init; }
```


Per-call telemetry integrations that receive lifecycle events during the call.

**Value:** [IReadOnlyList&lt;ITelemetry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_TelemetryOptions_IsEnabled_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_IsEnabled"></a>

### IsEnabled


```csharp
public bool? IsEnabled { get; init; }
```


Enable or disable telemetry. Enabled by default. Set to `false` to opt out.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_TelemetryOptions_RecordInputs_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_RecordInputs"></a>

### RecordInputs


```csharp
public bool? RecordInputs { get; init; }
```


Enable or disable input recording. Enabled by default. You might want to disable input recording to avoid recording sensitive
information, to reduce data transfers, or to increase performance.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_TelemetryOptions_RecordOutputs_"></a>
<a id="E2E_Decision_Sdk_TelemetryOptions_RecordOutputs"></a>

### RecordOutputs


```csharp
public bool? RecordOutputs { get; init; }
```


Enable or disable output recording. Enabled by default. You might want to disable output recording to avoid recording sensitive
information, to reduce data transfers, or to increase performance.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)
