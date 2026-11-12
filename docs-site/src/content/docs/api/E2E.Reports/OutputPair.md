---
title: "OutputPair"
description: "The test executing when output was written (upstream's inline pair of the output fact): its id, agent, and repeat."
editUrl: false
prev: false
next: false
sidebar:
  label: "OutputPair"
  hidden: true
---

<a id="E2E_Reports_OutputPair"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record OutputPair : IEquatable<OutputPair>
```


The test executing when output was written (upstream's inline `pair` of the `output` fact): its id, agent, and repeat.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OutputPair&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_OutputPair__ctor_"></a>
<a id="E2E_Reports_OutputPair__ctor_System_String_System_String_System_Int32_"></a>

### OutputPair(string, string, int)


```csharp
public OutputPair(string TestId, string Agent, int Repeat)
```


The test executing when output was written (upstream's inline `pair` of the `output` fact): its id, agent, and repeat.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `TestId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Agent` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Repeat` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_Reports_OutputPair_Agent_"></a>
<a id="E2E_Reports_OutputPair_Agent"></a>

### Agent


```csharp
public string Agent { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_OutputPair_Repeat_"></a>
<a id="E2E_Reports_OutputPair_Repeat"></a>

### Repeat


```csharp
public int Repeat { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_OutputPair_TestId_"></a>
<a id="E2E_Reports_OutputPair_TestId"></a>

### TestId


```csharp
public string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
