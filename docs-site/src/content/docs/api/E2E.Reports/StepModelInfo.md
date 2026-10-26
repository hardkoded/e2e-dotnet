---
title: "StepModelInfo"
description: "The model an agent step called and what it cost (upstream's StepModelInfo)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepModelInfo"
  hidden: true
---

<a id="E2E_Reports_StepModelInfo"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepModelInfo : IEquatable<StepModelInfo>
```


The model an agent step called and what it cost (upstream's `StepModelInfo`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepModelInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L744)

## Properties

<a id="E2E_Reports_StepModelInfo_AdapterVersion_"></a>
<a id="E2E_Reports_StepModelInfo_AdapterVersion"></a>

### AdapterVersion


```csharp
public string AdapterVersion { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L753)

<a id="E2E_Reports_StepModelInfo_CacheReadTokens_"></a>
<a id="E2E_Reports_StepModelInfo_CacheReadTokens"></a>

### CacheReadTokens


```csharp
public long? CacheReadTokens { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L767)

<a id="E2E_Reports_StepModelInfo_CacheWriteTokens_"></a>
<a id="E2E_Reports_StepModelInfo_CacheWriteTokens"></a>

### CacheWriteTokens


```csharp
public long? CacheWriteTokens { get; init; }
```


**Value:** [long?](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L769)

<a id="E2E_Reports_StepModelInfo_Calls_"></a>
<a id="E2E_Reports_StepModelInfo_Calls"></a>

### Calls


```csharp
public int Calls { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L757)

<a id="E2E_Reports_StepModelInfo_Endpoint_"></a>
<a id="E2E_Reports_StepModelInfo_Endpoint"></a>

### Endpoint


```csharp
public string Endpoint { get; init; }
```


`local`, `provider-default`, or a URI.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L751)

<a id="E2E_Reports_StepModelInfo_EstimatedCostUsd_"></a>
<a id="E2E_Reports_StepModelInfo_EstimatedCostUsd"></a>

### EstimatedCostUsd


```csharp
public double? EstimatedCostUsd { get; init; }
```


**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L771)

<a id="E2E_Reports_StepModelInfo_InputTokens_"></a>
<a id="E2E_Reports_StepModelInfo_InputTokens"></a>

### InputTokens


```csharp
public long InputTokens { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L763)

<a id="E2E_Reports_StepModelInfo_Model_"></a>
<a id="E2E_Reports_StepModelInfo_Model"></a>

### Model


```csharp
public string Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L748)

<a id="E2E_Reports_StepModelInfo_OutputTokens_"></a>
<a id="E2E_Reports_StepModelInfo_OutputTokens"></a>

### OutputTokens


```csharp
public long OutputTokens { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L765)

<a id="E2E_Reports_StepModelInfo_PeakTokensPerCall_"></a>
<a id="E2E_Reports_StepModelInfo_PeakTokensPerCall"></a>

### PeakTokensPerCall


```csharp
public int PeakTokensPerCall { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L761)

<a id="E2E_Reports_StepModelInfo_PolicyVersion_"></a>
<a id="E2E_Reports_StepModelInfo_PolicyVersion"></a>

### PolicyVersion


```csharp
public string PolicyVersion { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L755)

<a id="E2E_Reports_StepModelInfo_Provider_"></a>
<a id="E2E_Reports_StepModelInfo_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L746)

<a id="E2E_Reports_StepModelInfo_TokenAccounting_"></a>
<a id="E2E_Reports_StepModelInfo_TokenAccounting"></a>

### TokenAccounting


```csharp
public TokenAccounting TokenAccounting { get; init; }
```


**Value:** [TokenAccounting](/api/E2E.Reports/TokenAccounting/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L759)
