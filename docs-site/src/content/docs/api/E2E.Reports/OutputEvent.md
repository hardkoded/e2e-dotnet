---
title: "OutputEvent"
description: "Upstream's output fact: text a test wrote to stdout or stderr, as one write on Target. Pair is the test executing at the time, or null for output between tes..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OutputEvent"
  hidden: true
---

<a id="E2E_Reports_OutputEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record OutputEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<OutputEvent>
```


Upstream's `output` fact: text a test wrote to stdout or stderr, as one write on &lt;code class="paramref"&gt;Target&lt;/code&gt;. &lt;code class="paramref"&gt;Pair&lt;/code&gt; is the
test executing at the time, or null for output between tests. The list reporter prints it above its live window. The port's test framework
captures stdout, so nothing in the port emits it yet.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;OutputEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_OutputEvent__ctor_"></a>
<a id="E2E_Reports_OutputEvent__ctor_System_String_E2E_Reports_OutputPair_E2E_Reports_OutputStream_System_String_"></a>

### OutputEvent(string, OutputPair?, OutputStream, string)


```csharp
public OutputEvent(string Target, OutputPair? Pair, OutputStream Stream, string Text)
```


Upstream's `output` fact: text a test wrote to stdout or stderr, as one write on &lt;code class="paramref"&gt;Target&lt;/code&gt;. &lt;code class="paramref"&gt;Pair&lt;/code&gt; is the
test executing at the time, or null for output between tests. The list reporter prints it above its live window. The port's test framework
captures stdout, so nothing in the port emits it yet.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Pair` | [OutputPair](/api/E2E.Reports/OutputPair/) |  |
| `Stream` | [OutputStream](/api/E2E.Reports/OutputStream/) |  |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_OutputEvent_Pair_"></a>
<a id="E2E_Reports_OutputEvent_Pair"></a>

### Pair


```csharp
public OutputPair? Pair { get; init; }
```


**Value:** [OutputPair](/api/E2E.Reports/OutputPair/)

<a id="E2E_Reports_OutputEvent_Stream_"></a>
<a id="E2E_Reports_OutputEvent_Stream"></a>

### Stream


```csharp
public OutputStream Stream { get; init; }
```


**Value:** [OutputStream](/api/E2E.Reports/OutputStream/)

<a id="E2E_Reports_OutputEvent_Target_"></a>
<a id="E2E_Reports_OutputEvent_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_OutputEvent_Text_"></a>
<a id="E2E_Reports_OutputEvent_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
