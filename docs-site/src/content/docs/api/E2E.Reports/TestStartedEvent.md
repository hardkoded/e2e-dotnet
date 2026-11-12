---
title: "TestStartedEvent"
description: "Upstream's test-started fact: a test began executing. A test that runs as several agents, on several targets, or under repeat-each starts once per pair. Seri..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TestStartedEvent"
  hidden: true
---

<a id="E2E_Reports_TestStartedEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record TestStartedEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<TestStartedEvent>
```


Upstream's `test-started` fact: a test began executing. A test that runs as several agents, on several targets, or under repeat-each
starts once per pair. `SerialId` is the test's serial group; the port has no serial groups, so the list reporter does not read it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;TestStartedEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_TestStartedEvent__ctor_"></a>
<a id="E2E_Reports_TestStartedEvent__ctor_System_String_System_String_System_Int32_System_String_System_String_System_String_System_String_"></a>

### TestStartedEvent(string, string, int, string, string, string?, string)


```csharp
public TestStartedEvent(string TestId, string Agent, int Repeat, string Title, string File, string? SerialId, string Target)
```


Upstream's `test-started` fact: a test began executing. A test that runs as several agents, on several targets, or under repeat-each
starts once per pair. `SerialId` is the test's serial group; the port has no serial groups, so the list reporter does not read it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `TestId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Agent` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Repeat` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Title` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `File` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `SerialId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_TestStartedEvent_Agent_"></a>
<a id="E2E_Reports_TestStartedEvent_Agent"></a>

### Agent


```csharp
public string Agent { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestStartedEvent_File_"></a>
<a id="E2E_Reports_TestStartedEvent_File"></a>

### File


```csharp
public string File { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestStartedEvent_Repeat_"></a>
<a id="E2E_Reports_TestStartedEvent_Repeat"></a>

### Repeat


```csharp
public int Repeat { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Reports_TestStartedEvent_SerialId_"></a>
<a id="E2E_Reports_TestStartedEvent_SerialId"></a>

### SerialId


```csharp
public string? SerialId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestStartedEvent_Target_"></a>
<a id="E2E_Reports_TestStartedEvent_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestStartedEvent_TestId_"></a>
<a id="E2E_Reports_TestStartedEvent_TestId"></a>

### TestId


```csharp
public string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_TestStartedEvent_Title_"></a>
<a id="E2E_Reports_TestStartedEvent_Title"></a>

### Title


```csharp
public string Title { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
